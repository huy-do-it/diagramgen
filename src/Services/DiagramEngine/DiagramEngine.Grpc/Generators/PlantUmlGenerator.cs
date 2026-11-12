using System.Text;
using UMLForge.DiagramEngine.Grpc.Models;

namespace UMLForge.DiagramEngine.Grpc.Generators;

/// <summary>
/// Chuyển 5 model sơ đồ thành mã PlantUML (chép từ v1, logic giữ nguyên).
/// </summary>
public class PlantUmlGenerator : IPlantUmlGenerator
{
    // Smetana là layout engine thuần Java -> plantuml.jar chạy local không cần cài Graphviz
    private const string SmetanaPragma = "!pragma layout smetana";

    private const string ContextSystemAlias = "SYS";

    public string GeneratePlantUml(DiagramType type, BaseDiagramModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return (type, model) switch
        {
            (DiagramType.BusinessContext, BusinessContextModel businessContext) => GenerateBusinessContext(businessContext),
            (DiagramType.UseCase, UseCaseModel useCase) => GenerateUseCase(useCase),
            (DiagramType.Sequence, SequenceModel sequence) => GenerateSequence(sequence),
            (DiagramType.Activity, ActivityModel activity) => GenerateActivity(activity),
            (DiagramType.State, StateModel state) => GenerateState(state),
            _ when !Enum.IsDefined(type) => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported diagram type: {type}"),
            _ => throw new ArgumentException($"Model '{model.GetType().Name}' does not match diagram type '{type}'.", nameof(model))
        };
    }

    // Context diagram kiểu DFD mức 0: hệ thống là 1 hình tròn ở giữa (hộp đen), thực thể bên ngoài xếp
    // thành 2 cột trái/phải, mũi tên là luồng dữ liệu. Thành phần nội bộ được gộp vào hệ thống.
    // Dùng layout Graphviz (không Smetana) vì Smetana không vẽ được đường gấp khúc vuông góc.
    private static string GenerateBusinessContext(BusinessContextModel model)
    {
        var sb = BeginDiagram(model.Title, useSmetana: false);
        WriteLine(sb, "left to right direction");
        WriteLine(sb, "skinparam linetype ortho");
        WriteLine(sb, "skinparam nodesep 25");
        WriteLine(sb, "skinparam ranksep 40");
        WriteLine(sb, "skinparam shadowing false");
        WriteLine(sb, "skinparam ArrowColor #333333");
        WriteLine(sb, "skinparam usecase {");
        WriteLine(sb, "  BackgroundColor #D6EEF5");
        WriteLine(sb, "  BorderColor #333333");
        WriteLine(sb, "  FontStyle bold");
        WriteLine(sb, "}");
        WriteLine(sb, "skinparam rectangle {");
        WriteLine(sb, "  BackgroundColor #D9F7D6");
        WriteLine(sb, "  BorderColor #333333");
        WriteLine(sb, "}");

        // Tên hệ thống và mọi thành phần nội bộ đều trỏ về cùng alias của hệ thống
        var aliases = CreateAliasMap();
        foreach (var name in model.InternalComponents.Append(model.System).Select(Sanitize).Where(name => name.Length > 0))
        {
            aliases.TryAdd(name, ContextSystemAlias);
        }

        // Thực thể bên ngoài xếp xen kẽ trái/phải; cột trái khai báo trước hệ thống, cột phải khai báo sau
        var externals = AssignAliases(model.ExternalSystems, "E", aliases);
        var leftAliases = externals.Where((_, index) => index % 2 == 0).Select(external => external.Alias).ToHashSet();
        foreach (var (name, alias) in externals.Where(external => leftAliases.Contains(external.Alias)))
        {
            WriteLine(sb, $"rectangle \"{name}\" as {alias}");
        }
        WriteLine(sb, $"usecase \"{BuildContextSystemLabel(NameOrDefault(model.System, "System"))}\" as {ContextSystemAlias}");
        foreach (var (name, alias) in externals.Where(external => !leftAliases.Contains(external.Alias)))
        {
            WriteLine(sb, $"rectangle \"{name}\" as {alias}");
        }

        var flowIndex = 0;
        foreach (var relation in model.Relations)
        {
            if (!aliases.TryGetValue(Sanitize(relation.From), out var fromAlias)
                || !aliases.TryGetValue(Sanitize(relation.To), out var toAlias))
            {
                continue;
            }

            // Chỉ vẽ luồng giữa hệ thống và bên ngoài; luồng nội bộ hoặc giữa 2 bên ngoài không thuộc context diagram
            var fromIsSystem = fromAlias == ContextSystemAlias;
            var toIsSystem = toAlias == ContextSystemAlias;
            if (fromIsSystem == toIsSystem)
            {
                continue;
            }

            // Vế trái luôn là phần tử ở cột bên trái để Graphviz giữ đúng thứ tự cột; chiều luồng thể hiện bằng --> / <--
            var external = fromIsSystem ? toAlias : fromAlias;
            var isLeftColumn = leftAliases.Contains(external);
            var leftNode = isLeftColumn ? external : ContextSystemAlias;
            var rightNode = isLeftColumn ? ContextSystemAlias : external;
            var arrowToRight = isLeftColumn ? !fromIsSystem : fromIsSystem;

            var label = Sanitize(relation.Protocol);
            if (label.Length == 0)
            {
                WriteLine(sb, arrowToRight ? $"{leftNode} --> {rightNode}" : $"{leftNode} <-- {rightNode}");
                continue;
            }

            // Nhãn được vẽ thành 1 nút chữ nằm giữa đường để Graphviz dành chỗ riêng, tránh chữ đè lên nhau
            var flowAlias = $"F{flowIndex++}";
            WriteLine(sb, $"label \"{label}\" as {flowAlias}");
            WriteLine(sb, arrowToRight ? $"{leftNode} -- {flowAlias}" : $"{leftNode} <-- {flowAlias}");
            WriteLine(sb, arrowToRight ? $"{flowAlias} --> {rightNode}" : $"{flowAlias} -- {rightNode}");
        }

        return EndDiagram(sb);
    }

    // Chia tên hệ thống thành các dòng ngắn và đệm dòng trống trên/dưới để usecase thành hình tròn to,
    // giúp mũi tên có nhiều chỗ bám quanh viền thay vì dồn vào 1 điểm
    private static string BuildContextSystemLabel(string systemName)
    {
        const int maxLineLength = 16;
        const int paddingLines = 4;

        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in systemName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > maxLineLength)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }
            current.Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        var padding = string.Concat(Enumerable.Repeat("\\n", paddingLines));
        return padding + string.Join("\\n", lines) + padding;
    }

    private static string GenerateUseCase(UseCaseModel model)
    {
        var sb = BeginDiagram(model.Title, useSmetana: false);
        WriteLine(sb, "left to right direction");
        WriteLine(sb, "skinparam packageStyle rectangle");

        var aliases = CreateAliasMap();
        foreach (var (name, alias) in AssignAliases(model.Actors, "A", aliases))
        {
            WriteLine(sb, $"actor \"{name}\" as {alias}");
        }

        WriteLine(sb, $"rectangle \"{NameOrDefault(model.SystemBoundary, "System")}\" {{");
        foreach (var (name, alias) in AssignAliases(model.UseCases, "UC", aliases))
        {
            WriteLine(sb, $"  usecase \"{name}\" as {alias}");
        }
        WriteLine(sb, "}");

        WriteUseCaseRelations(sb, model.Relations, aliases);
        return EndDiagram(sb);
    }

    // association: đường thẳng không mũi tên; include/extend: mũi tên nét đứt kèm stereotype;
    // generalization: tam giác rỗng chỉ về cha (viết cha ở vế trái để cha nằm phía trước con).
    // Relation trỏ tới tên chưa khai báo hoặc sai loại bị bỏ qua (gRPC ValidateDiagram đã báo trước đó)
    private static void WriteUseCaseRelations(
        StringBuilder sb, IEnumerable<UseCaseRelation> relations, Dictionary<string, string> aliases)
    {
        foreach (var relation in relations)
        {
            if (!aliases.TryGetValue(Sanitize(relation.From), out var fromAlias)
                || !aliases.TryGetValue(Sanitize(relation.To), out var toAlias))
            {
                continue;
            }

            var line = relation.RelationType switch
            {
                "association" => $"{fromAlias} -- {toAlias}",
                "include" => $"{fromAlias} ..> {toAlias} : <<include>>",
                "extend" => $"{fromAlias} ..> {toAlias} : <<extend>>",
                "generalization" => $"{toAlias} <|-- {fromAlias}",
                _ => null
            };

            if (line != null)
            {
                WriteLine(sb, line);
            }
        }
    }

    private static string GenerateSequence(SequenceModel model)
    {
        var sb = BeginDiagram(model.Title, useSmetana: false);

        var aliases = CreateAliasMap();
        foreach (var (name, alias) in AssignAliases(model.Participants, "P", aliases))
        {
            WriteLine(sb, $"participant \"{name}\" as {alias}");
        }

        WriteSequenceMessages(sb, model.Messages, aliases, depth: 0);
        return EndDiagram(sb);
    }

    private static void WriteSequenceMessages(
        StringBuilder sb, IEnumerable<SequenceMessage> messages, Dictionary<string, string> aliases, int depth)
    {
        var indent = new string(' ', depth * 2);
        foreach (var message in messages)
        {
            if (message.IsFragment)
            {
                WriteSequenceFragment(sb, message, aliases, depth);
                continue;
            }

            if (!aliases.TryGetValue(Sanitize(message.From), out var fromAlias)
                || !aliases.TryGetValue(Sanitize(message.To), out var toAlias))
            {
                continue;
            }

            // Message phản hồi vẽ nét đứt, message gọi vẽ nét liền
            var arrow = message.IsResponse ? "-->" : "->";
            var label = Sanitize(message.Label);
            WriteLine(sb, label.Length == 0
                ? $"{indent}{fromAlias} {arrow} {toAlias}"
                : $"{indent}{fromAlias} {arrow} {toAlias} : {label}");
        }
    }

    // alt: nhánh đầu "alt <điều kiện>", các nhánh sau "else <điều kiện>"; opt/loop chỉ lấy nhánh đầu.
    // Khối sai loại hoặc không có nhánh bị bỏ qua (gRPC ValidateDiagram đã báo lỗi trước đó)
    private static void WriteSequenceFragment(
        StringBuilder sb, SequenceMessage fragment, Dictionary<string, string> aliases, int depth)
    {
        var kind = fragment.Fragment!.Trim().ToLowerInvariant();
        if (!SequenceModel.SupportedFragments.Contains(kind) || fragment.Branches.Count == 0)
        {
            return;
        }

        var indent = new string(' ', depth * 2);
        var branches = kind == "alt" ? fragment.Branches : fragment.Branches.Take(1);
        var isFirst = true;
        foreach (var branch in branches)
        {
            var keyword = isFirst ? kind : "else";
            var condition = Sanitize(branch.Condition);
            WriteLine(sb, condition.Length == 0 ? $"{indent}{keyword}" : $"{indent}{keyword} {condition}");
            WriteSequenceMessages(sb, branch.Messages, aliases, depth + 1);
            isFirst = false;
        }

        WriteLine(sb, $"{indent}end");
    }

    private static string GenerateActivity(ActivityModel model)
    {
        var sb = BeginDiagram(model.Title, useSmetana: false);
        WriteLine(sb, "start");

        var startNode = Sanitize(model.StartNode);
        if (startNode.Length > 0)
        {
            WriteLine(sb, $":{startNode};");
        }

        var stepNames = model.Steps
            .Select(Sanitize)
            .Where(step => step.Length > 0 && !step.Equals(startNode, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Decision có yesBranch trùng 1 step -> đặt if tại vị trí step đó (step trở thành nhánh yes).
        // Decision không khớp step nào (hoặc trùng step đã có decision khác) -> đặt sau step cuối.
        var decisionsByStep = new Dictionary<string, ActivityDecision>(StringComparer.OrdinalIgnoreCase);
        var trailingDecisions = new List<ActivityDecision>();
        foreach (var decision in model.Decisions)
        {
            if (Sanitize(decision.Condition).Length == 0)
            {
                continue;
            }

            var yesBranch = Sanitize(decision.YesBranch);
            if (!stepNames.Contains(yesBranch) || !decisionsByStep.TryAdd(yesBranch, decision))
            {
                trailingDecisions.Add(decision);
            }
        }

        foreach (var step in model.Steps.Select(Sanitize))
        {
            if (step.Length == 0 || step.Equals(startNode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (decisionsByStep.Remove(step, out var decision))
            {
                WriteDecision(sb, decision);
            }
            else
            {
                WriteLine(sb, $":{step};");
            }
        }

        foreach (var decision in trailingDecisions)
        {
            WriteDecision(sb, decision);
        }

        WriteLine(sb, "stop");
        return EndDiagram(sb);
    }

    private static string GenerateState(StateModel model)
    {
        var sb = BeginDiagram(model.Title, useSmetana: true);

        // Khai báo cả state chỉ xuất hiện ở initialState/transitions để không bị mất khỏi diagram
        var aliases = CreateAliasMap();
        var stateNames = model.States
            .Append(model.InitialState)
            .Concat(model.Transitions.SelectMany(t => new[] { t.From, t.To }));
        var declaredStates = AssignAliases(stateNames, "S", aliases);
        foreach (var (name, alias) in declaredStates)
        {
            WriteLine(sb, $"state \"{name}\" as {alias}");
        }

        if (aliases.TryGetValue(Sanitize(model.InitialState), out var initialAlias))
        {
            WriteLine(sb, $"[*] --> {initialAlias}");
        }

        var statesWithOutgoing = new HashSet<string>();
        foreach (var transition in model.Transitions)
        {
            if (!aliases.TryGetValue(Sanitize(transition.From), out var fromAlias)
                || !aliases.TryGetValue(Sanitize(transition.To), out var toAlias))
            {
                continue;
            }

            statesWithOutgoing.Add(fromAlias);
            var trigger = Sanitize(transition.Event);
            WriteLine(sb, trigger.Length == 0
                ? $"{fromAlias} --> {toAlias}"
                : $"{fromAlias} --> {toAlias} : {trigger}");
        }

        // State không có transition đi ra được coi là trạng thái kết thúc
        foreach (var (_, alias) in declaredStates.Where(s => !statesWithOutgoing.Contains(s.Alias)))
        {
            WriteLine(sb, $"{alias} --> [*]");
        }

        return EndDiagram(sb);
    }

    private static void WriteDecision(StringBuilder sb, ActivityDecision decision)
    {
        WriteLine(sb, $"if ({Sanitize(decision.Condition)}) then (yes)");

        var yesBranch = Sanitize(decision.YesBranch);
        if (yesBranch.Length > 0)
        {
            WriteLine(sb, $"  :{yesBranch};");
        }

        var noBranch = Sanitize(decision.NoBranch);
        if (noBranch.Length > 0)
        {
            WriteLine(sb, "else (no)");
            WriteLine(sb, $"  :{noBranch};");
        }

        WriteLine(sb, "endif");
    }

    // Gán alias <prefix><index> cho từng tên; bỏ qua tên rỗng hoặc đã có alias (không phân biệt hoa thường)
    private static List<(string Name, string Alias)> AssignAliases(
        IEnumerable<string> names, string prefix, Dictionary<string, string> aliases)
    {
        var declared = new List<(string Name, string Alias)>();
        foreach (var name in names.Select(Sanitize))
        {
            if (name.Length == 0 || aliases.ContainsKey(name))
            {
                continue;
            }

            var alias = $"{prefix}{declared.Count}";
            aliases[name] = alias;
            declared.Add((name, alias));
        }
        return declared;
    }

    private static Dictionary<string, string> CreateAliasMap() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static StringBuilder BeginDiagram(string title, bool useSmetana)
    {
        var sb = new StringBuilder();
        WriteLine(sb, "@startuml");
        if (useSmetana)
        {
            WriteLine(sb, SmetanaPragma);
        }

        var safeTitle = Sanitize(title);
        if (safeTitle.Length > 0)
        {
            WriteLine(sb, $"title {safeTitle}");
        }
        return sb;
    }

    private static string EndDiagram(StringBuilder sb) => sb.Append("@enduml").ToString();

    // Dùng '\n' cố định để output giống nhau trên Windows và Linux (CI)
    private static void WriteLine(StringBuilder sb, string line) => sb.Append(line).Append('\n');

    private static string NameOrDefault(string? name, string fallback)
    {
        var safeName = Sanitize(name);
        return safeName.Length == 0 ? fallback : safeName;
    }

    // Bỏ xuống dòng và thay dấu " để text không phá cú pháp PlantUML
    private static string Sanitize(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Replace("\r", " ").Replace("\n", " ").Replace("\"", "'").Trim();
}
