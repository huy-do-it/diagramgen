using UMLForge.Contracts.Grpc;
using UMLForge.DiagramEngine.Grpc.Models;

namespace UMLForge.DiagramEngine.Grpc.Validators;

/// <summary>Kiểm tra cú pháp + luật UML (BR-18…25) của 1 sơ đồ.</summary>
public interface IUmlRuleValidator
{
    DiagramValidationResponse Validate(DiagramValidationRequest request);
}

/// <summary>
/// Luật UML chép nguyên từ <c>DiagramEngineServiceImpl</c> của v1 (logic giữ nguyên),
/// tách khỏi lớp gRPC để service gRPC (B-04) và test dùng chung.
/// </summary>
public class UmlRuleValidator : IUmlRuleValidator
{
    public DiagramValidationResponse Validate(DiagramValidationRequest request)
    {
        var response = new DiagramValidationResponse { IsValid = true };
        var diagramType = (DiagramType)request.DiagramType;

        if (!DiagramModelParser.TryParse(diagramType, request.ModelJson, out var model, out var parseError))
        {
            response.IsValid = false;
            response.Errors.Add($"Structural syntax error: {parseError}");
            return response;
        }

        if (model == null)
        {
            response.IsValid = false;
            response.Errors.Add("Diagram model is null.");
            return response;
        }

        // Semantic UML validation
        switch (model)
        {
            case UseCaseModel uc:
                ValidateUseCase(uc, response);
                break;
            case SequenceModel seq:
                ValidateSequence(seq, response);
                break;
            case BusinessContextModel bc:
                ValidateBusinessContext(bc, response);
                break;
            case ActivityModel act:
                ValidateActivity(act, response);
                break;
            case StateModel st:
                ValidateState(st, response);
                break;
            default:
                response.IsValid = false;
                response.Errors.Add($"Unknown diagram model type: {model.GetType().Name}");
                break;
        }

        response.IsValid = response.Errors.Count == 0;
        return response;
    }

    private static void ValidateUseCase(UseCaseModel uc, DiagramValidationResponse response)
    {
        if (string.IsNullOrWhiteSpace(uc.Title))
            response.Errors.Add("UseCase diagram requires a title.");

        if (uc.Actors.Count == 0)
            response.Errors.Add("UseCase diagram must have at least one actor.");

        if (uc.UseCases.Count == 0)
            response.Errors.Add("UseCase diagram must have at least one use case.");

        var actorsSet = uc.Actors.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var useCasesSet = uc.UseCases.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Trùng tên actor và use case khiến relation không biết trỏ tới phần tử nào
        foreach (var name in uc.Actors.Where(useCasesSet.Contains).Distinct(StringComparer.OrdinalIgnoreCase))
            response.Errors.Add($"Name '{name}' is declared as both an actor and a use case.");

        var seenRelations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directedEdges = new Dictionary<string, List<(string From, string To)>>();
        var links = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var rel in uc.Relations)
        {
            if (string.IsNullOrWhiteSpace(rel.From) || string.IsNullOrWhiteSpace(rel.To))
            {
                response.Errors.Add("Relation must have non-empty 'from' and 'to' targets.");
                continue;
            }

            var fromIsActor = actorsSet.Contains(rel.From);
            var fromIsUseCase = useCasesSet.Contains(rel.From);
            var toIsActor = actorsSet.Contains(rel.To);
            var toIsUseCase = useCasesSet.Contains(rel.To);

            if (!fromIsActor && !fromIsUseCase)
                response.Warnings.Add($"Relation 'from' target '{rel.From}' is neither a declared actor nor a use case.");

            if (!toIsActor && !toIsUseCase)
                response.Warnings.Add($"Relation 'to' target '{rel.To}' is neither a declared actor nor a use case.");

            var type = rel.RelationType;
            if (!UseCaseModel.SupportedRelationTypes.Contains(type))
            {
                response.Errors.Add($"Use case relation type '{rel.Type}' is not supported. Use 'association', 'include', 'extend' or 'generalization'.");
                continue;
            }

            // Chỉ kiểm tra loại quan hệ khi cả 2 đầu đã khai báo (tên lạ đã có cảnh báo ở trên)
            if ((!fromIsActor && !fromIsUseCase) || (!toIsActor && !toIsUseCase))
                continue;

            if (string.Equals(rel.From, rel.To, StringComparison.OrdinalIgnoreCase))
            {
                response.Errors.Add($"Relation '{rel.From}' cannot point to itself.");
                continue;
            }

            if (!seenRelations.Add($"{type}|{rel.From}|{rel.To}"))
            {
                response.Warnings.Add($"Relation '{rel.From}' -> '{rel.To}' ({type}) is declared more than once.");
                continue;
            }

            var sameKind = (fromIsActor && toIsActor) || (fromIsUseCase && toIsUseCase);
            var isValidRelation = true;
            switch (type)
            {
                case "association" when sameKind:
                    response.Errors.Add($"Association '{rel.From}' - '{rel.To}' must connect an actor with a use case. Use 'include', 'extend' or 'generalization' instead.");
                    isValidRelation = false;
                    break;
                case "include" or "extend" when !(fromIsUseCase && toIsUseCase):
                    response.Errors.Add($"'{type}' relation '{rel.From}' -> '{rel.To}' must connect two use cases.");
                    isValidRelation = false;
                    break;
                case "generalization" when !sameKind:
                    response.Errors.Add($"Generalization '{rel.From}' -> '{rel.To}' must connect two actors or two use cases.");
                    isValidRelation = false;
                    break;
            }

            if (!isValidRelation)
                continue;

            if (type != "association")
            {
                if (!directedEdges.TryGetValue(type, out var edges))
                    directedEdges[type] = edges = new List<(string From, string To)>();
                edges.Add((rel.From, rel.To));
            }

            AddLink(links, rel.From, rel.To);
            AddLink(links, rel.To, rel.From);
        }

        foreach (var (type, edges) in directedEdges)
        {
            if (HasCycle(edges))
                response.Errors.Add($"'{type}' relations must not form a cycle.");
        }

        // Xét theo nhóm liên thông (bỏ qua chiều mũi tên): use case phải tới được từ 1 actor, actor phải tới được 1 use case
        foreach (var group in GetConnectedGroups(uc.Actors.Concat(uc.UseCases), links))
        {
            if (!group.Any(actorsSet.Contains))
            {
                foreach (var useCase in uc.UseCases.Where(group.Contains))
                    response.Warnings.Add($"Use case '{useCase}' is not used by any actor (directly or via include, extend or generalization).");
            }

            if (!group.Any(useCasesSet.Contains))
            {
                foreach (var actor in uc.Actors.Where(group.Contains))
                    response.Warnings.Add($"Actor '{actor}' is not connected to any use case.");
            }
        }
    }

    private static void AddLink(Dictionary<string, List<string>> links, string from, string to)
    {
        if (!links.TryGetValue(from, out var neighbors))
            links[from] = neighbors = new List<string>();
        neighbors.Add(to);
    }

    // Gom các phần tử nối với nhau (bỏ qua chiều) thành từng nhóm bằng BFS
    private static List<HashSet<string>> GetConnectedGroups(IEnumerable<string> nodes, Dictionary<string, List<string>> links)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<HashSet<string>>();
        foreach (var start in nodes)
        {
            if (!visited.Add(start))
                continue;

            var group = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
            var queue = new Queue<string>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                if (!links.TryGetValue(queue.Dequeue(), out var neighbors))
                    continue;

                foreach (var next in neighbors.Where(visited.Add))
                {
                    group.Add(next);
                    queue.Enqueue(next);
                }
            }
            groups.Add(group);
        }
        return groups;
    }

    // Phát hiện vòng lặp trong đồ thị có hướng bằng DFS: 1 = đang duyệt, 2 = đã duyệt xong
    private static bool HasCycle(List<(string From, string To)> edges)
    {
        var graph = edges
            .GroupBy(edge => edge.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.To).ToList(), StringComparer.OrdinalIgnoreCase);
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        bool Visit(string node)
        {
            if (state.TryGetValue(node, out var current))
                return current == 1;

            state[node] = 1;
            if (graph.TryGetValue(node, out var nextNodes) && nextNodes.Any(Visit))
                return true;

            state[node] = 2;
            return false;
        }

        return graph.Keys.ToList().Any(Visit);
    }

    private static void ValidateSequence(SequenceModel seq, DiagramValidationResponse response)
    {
        if (string.IsNullOrWhiteSpace(seq.Title))
            response.Errors.Add("Sequence diagram requires a title.");

        if (seq.Participants.Count < 2)
            response.Errors.Add("Sequence diagram must have at least two participants for message exchange.");

        if (seq.Messages.Count == 0)
            response.Errors.Add("Sequence diagram must contain at least one message.");

        var participantsSet = seq.Participants.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ValidateSequenceMessages(seq.Messages, participantsSet, response);
    }

    // Kiểm tra đệ quy: message thường kiểm tra participant, khối alt/opt/loop kiểm tra cấu trúc nhánh
    private static void ValidateSequenceMessages(
        IEnumerable<SequenceMessage> messages, HashSet<string> participantsSet, DiagramValidationResponse response)
    {
        foreach (var msg in messages)
        {
            if (msg.IsFragment)
            {
                ValidateSequenceFragment(msg, participantsSet, response);
                continue;
            }

            if (!participantsSet.Contains(msg.From))
                response.Errors.Add($"Sequence message source '{msg.From}' is not in declared participants list.");

            if (!participantsSet.Contains(msg.To))
                response.Errors.Add($"Sequence message target '{msg.To}' is not in declared participants list.");

            if (string.IsNullOrWhiteSpace(msg.Label))
                response.Warnings.Add($"Message between '{msg.From}' and '{msg.To}' has no label.");
        }
    }

    private static void ValidateSequenceFragment(
        SequenceMessage fragment, HashSet<string> participantsSet, DiagramValidationResponse response)
    {
        var kind = fragment.Fragment!.Trim().ToLowerInvariant();
        if (!SequenceModel.SupportedFragments.Contains(kind))
        {
            response.Errors.Add($"Sequence fragment '{fragment.Fragment}' is not supported. Use 'alt', 'opt' or 'loop'.");
            return;
        }

        if (fragment.Branches.Count == 0)
        {
            response.Errors.Add($"Sequence '{kind}' fragment must have at least one branch.");
            return;
        }

        if (kind == "alt" && fragment.Branches.Count == 1)
            response.Warnings.Add("Sequence 'alt' fragment has only one branch. Consider using 'opt' instead.");

        if (kind != "alt" && fragment.Branches.Count > 1)
            response.Errors.Add($"Sequence '{kind}' fragment must have exactly one branch. Only 'alt' supports multiple branches.");

        if (string.IsNullOrWhiteSpace(fragment.Branches[0].Condition))
            response.Errors.Add($"Sequence '{kind}' fragment requires a condition on its first branch.");

        foreach (var branch in fragment.Branches)
        {
            if (branch.Messages.Count == 0)
            {
                var name = string.IsNullOrWhiteSpace(branch.Condition) ? "else" : branch.Condition;
                response.Errors.Add($"Branch '{name}' of sequence '{kind}' fragment must contain at least one message.");
            }

            ValidateSequenceMessages(branch.Messages, participantsSet, response);
        }
    }

    private static void ValidateBusinessContext(BusinessContextModel bc, DiagramValidationResponse response)
    {
        if (string.IsNullOrWhiteSpace(bc.Title))
            response.Errors.Add("BusinessContext diagram requires a title.");

        if (string.IsNullOrWhiteSpace(bc.System))
            response.Errors.Add("BusinessContext diagram requires a primary system boundary name.");

        if (bc.ExternalSystems.Count == 0)
            response.Errors.Add("BusinessContext diagram requires at least one external entity around the system.");

        // Hệ thống và thành phần nội bộ được coi là cùng 1 khối ở giữa context diagram
        var systemSet = bc.InternalComponents.Append(bc.System).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var externalSet = bc.ExternalSystems.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rel in bc.Relations)
        {
            if (!systemSet.Contains(rel.From) && !externalSet.Contains(rel.From))
                response.Warnings.Add($"Flow source '{rel.From}' is neither the system, an internal component nor an external entity.");

            if (!systemSet.Contains(rel.To) && !externalSet.Contains(rel.To))
                response.Warnings.Add($"Flow target '{rel.To}' is neither the system, an internal component nor an external entity.");

            if (externalSet.Contains(rel.From) && externalSet.Contains(rel.To))
                response.Warnings.Add($"Flow '{rel.From}' -> '{rel.To}' is between two external entities and is not shown in a context diagram.");

            if (externalSet.Contains(rel.From) && systemSet.Contains(rel.To))
                connected.Add(rel.From);

            if (systemSet.Contains(rel.From) && externalSet.Contains(rel.To))
                connected.Add(rel.To);
        }

        foreach (var external in bc.ExternalSystems.Where(external => !connected.Contains(external)))
            response.Warnings.Add($"External entity '{external}' has no data flow with the system.");
    }

    private static void ValidateActivity(ActivityModel act, DiagramValidationResponse response)
    {
        if (string.IsNullOrWhiteSpace(act.Title))
            response.Errors.Add("Activity diagram requires a title.");

        if (act.Steps.Count == 0)
            response.Errors.Add("Activity diagram must contain at least one action step.");
    }

    private static void ValidateState(StateModel st, DiagramValidationResponse response)
    {
        if (string.IsNullOrWhiteSpace(st.Title))
            response.Errors.Add("State diagram requires a title.");

        if (string.IsNullOrWhiteSpace(st.InitialState))
            response.Errors.Add("State diagram must declare an initial state.");

        if (st.Transitions.Count == 0)
            response.Errors.Add("State diagram must contain at least one state transition.");
    }
}
