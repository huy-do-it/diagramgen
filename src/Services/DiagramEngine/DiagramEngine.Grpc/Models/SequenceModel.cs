using System.Text.Json.Serialization;

namespace UMLForge.DiagramEngine.Grpc.Models;

public class SequenceModel : BaseDiagramModel
{
    public static readonly IReadOnlySet<string> SupportedFragments =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "alt", "opt", "loop" };

    public override string Type => "Sequence";

    [JsonPropertyName("participants")]
    public List<string> Participants { get; set; } = new();

    [JsonPropertyName("messages")]
    public List<SequenceMessage> Messages { get; set; } = new();

    // Duyệt đệ quy để lấy mọi message thật, kể cả message nằm trong khối alt/opt/loop lồng nhau
    public IEnumerable<SequenceMessage> GetAllMessages() => Flatten(Messages);

    private static IEnumerable<SequenceMessage> Flatten(IEnumerable<SequenceMessage> messages)
    {
        foreach (var message in messages)
        {
            if (!message.IsFragment)
            {
                yield return message;
                continue;
            }

            foreach (var inner in message.Branches.SelectMany(branch => Flatten(branch.Messages)))
            {
                yield return inner;
            }
        }
    }
}

public class SequenceMessage
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("isResponse")]
    public bool IsResponse { get; set; } = false;

    // Có giá trị (alt/opt/loop) thì phần tử này là một khối điều kiện thay vì một message
    [JsonPropertyName("fragment")]
    public string? Fragment { get; set; }

    [JsonPropertyName("branches")]
    public List<SequenceBranch> Branches { get; set; } = new();

    [JsonIgnore]
    public bool IsFragment => !string.IsNullOrWhiteSpace(Fragment);
}

public class SequenceBranch
{
    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<SequenceMessage> Messages { get; set; } = new();
}
