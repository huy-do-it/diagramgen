using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Service.Helpers;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Models.Diagrams;
using UMLForge.Diagram.Service.Models.Validation;

namespace UMLForge.Diagram.Service.Services;

public class DiagramEngineClient : IDiagramEngineClient
{
    public Task<DiagramValidationResult> ValidateDiagramAsync(
        DiagramType type,
        string modelJson,
        CancellationToken cancellationToken = default)
    {
        var result = new DiagramValidationResult();

        if (!DiagramModelParser.TryParse(type, modelJson, out var model, out var parseError))
        {
            result.IsValid = false;
            result.Errors.Add($"Structural syntax error: {parseError}");
            return Task.FromResult(result);
        }

        if (model == null)
        {
            result.IsValid = false;
            result.Errors.Add("Diagram model is null.");
            return Task.FromResult(result);
        }

        switch (model)
        {
            case UseCaseModel uc:
                ValidateUseCase(uc, result);
                break;
            case SequenceModel seq:
                ValidateSequence(seq, result);
                break;
            case BusinessContextModel bc:
                ValidateBusinessContext(bc, result);
                break;
            case ActivityModel act:
                ValidateActivity(act, result);
                break;
            case StateModel st:
                ValidateState(st, result);
                break;
            default:
                result.IsValid = false;
                result.Errors.Add($"Unknown diagram model type: {model.GetType().Name}");
                break;
        }

        result.IsValid = result.Errors.Count == 0;
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<RelationSuggestion>> SuggestRelationsAsync(
        DiagramType type,
        string modelJson,
        CancellationToken cancellationToken = default)
    {
        var suggestions = new List<RelationSuggestion>();

        if (!DiagramModelParser.TryParse(type, modelJson, out var model, out _))
        {
            return Task.FromResult<IReadOnlyList<RelationSuggestion>>(suggestions);
        }

        if (model is UseCaseModel uc && uc.Actors.Count > 0 && uc.UseCases.Count > 0)
        {
            var connectedActors = uc.Relations
                .Where(r => r.RelationType is "association" or "generalization")
                .SelectMany(r => new[] { r.From, r.To })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var actor in uc.Actors)
            {
                if (!connectedActors.Contains(actor))
                {
                    suggestions.Add(new RelationSuggestion
                    {
                        From = actor,
                        To = uc.UseCases[0],
                        Description = $"Actor '{actor}' is currently unconnected. Suggested relation to primary use case '{uc.UseCases[0]}'."
                    });
                }
            }
        }
        else if (model is SequenceModel seq && seq.Participants.Count >= 2)
        {
            var receivers = seq.GetAllMessages().Select(m => m.To).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var participant in seq.Participants)
            {
                if (!receivers.Contains(participant) && participant != seq.Participants[0])
                {
                    suggestions.Add(new RelationSuggestion
                    {
                        From = seq.Participants[0],
                        To = participant,
                        Description = $"Participant '{participant}' receives no messages. Consider adding an inquiry message from '{seq.Participants[0]}'."
                    });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<RelationSuggestion>>(suggestions);
    }

    public async Task<IReadOnlyList<ProjectDiagramValidationResult>> ValidateProjectAsync(
        IEnumerable<(Guid DiagramId, DiagramType Type, string ModelJson)> diagrams,
        CancellationToken cancellationToken = default)
    {
        var list = new List<ProjectDiagramValidationResult>();
        foreach (var (diagramId, type, modelJson) in diagrams)
        {
            var val = await ValidateDiagramAsync(type, modelJson, cancellationToken);
            list.Add(new ProjectDiagramValidationResult
            {
                DiagramId = diagramId,
                IsValid = val.IsValid,
                Errors = val.Errors,
                Warnings = val.Warnings
            });
        }
        return list;
    }

    private static void ValidateUseCase(UseCaseModel uc, DiagramValidationResult response)
    {
        if (uc.Actors.Count == 0)
            response.Errors.Add("UseCase diagram must have at least one actor.");

        if (uc.UseCases.Count == 0)
            response.Errors.Add("UseCase diagram must have at least one use case.");

        var actorsSet = uc.Actors.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var useCasesSet = uc.UseCases.ToHashSet(StringComparer.OrdinalIgnoreCase);

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

    private static void ValidateSequence(SequenceModel seq, DiagramValidationResult response)
    {
        if (seq.Participants.Count < 2)
            response.Errors.Add("Sequence diagram must have at least two participants for message exchange.");

        if (seq.Messages.Count == 0)
            response.Errors.Add("Sequence diagram must contain at least one message.");

        var participantsSet = seq.Participants.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ValidateSequenceMessages(seq.Messages, participantsSet, response);
    }

    private static void ValidateSequenceMessages(
        IEnumerable<SequenceMessage> messages, HashSet<string> participantsSet, DiagramValidationResult response)
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
        SequenceMessage fragment, HashSet<string> participantsSet, DiagramValidationResult response)
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

    private static void ValidateBusinessContext(BusinessContextModel bc, DiagramValidationResult response)
    {
        if (string.IsNullOrWhiteSpace(bc.System))
            response.Errors.Add("BusinessContext diagram requires a primary system boundary name.");

        if (bc.ExternalSystems.Count == 0)
            response.Errors.Add("BusinessContext diagram requires at least one external entity around the system.");

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

    private static void ValidateActivity(ActivityModel act, DiagramValidationResult response)
    {
        if (act.Steps.Count == 0)
            response.Errors.Add("Activity diagram must contain at least one action step.");
    }

    private static void ValidateState(StateModel st, DiagramValidationResult response)
    {
        if (string.IsNullOrWhiteSpace(st.InitialState))
            response.Errors.Add("State diagram must declare an initial state.");

        if (st.Transitions.Count == 0)
            response.Errors.Add("State diagram must contain at least one state transition.");
    }
}
