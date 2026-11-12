using System.Text.Json.Serialization;

namespace UMLForge.Diagram.Service.Models.Diagrams;

public class UseCaseModel : BaseDiagramModel
{
    public static readonly IReadOnlySet<string> SupportedRelationTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "association", "include", "extend", "generalization" };

    public override string Type => "UseCase";

    [JsonPropertyName("systemBoundary")]
    public string SystemBoundary { get; set; } = "System";

    [JsonPropertyName("actors")]
    public List<string> Actors { get; set; } = new();

    [JsonPropertyName("useCases")]
    public List<string> UseCases { get; set; } = new();

    [JsonPropertyName("relations")]
    public List<UseCaseRelation> Relations { get; set; } = new();
}

public class UseCaseRelation
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonIgnore]
    public string RelationType => string.IsNullOrWhiteSpace(Type) ? "association" : Type.Trim().ToLowerInvariant();
}
