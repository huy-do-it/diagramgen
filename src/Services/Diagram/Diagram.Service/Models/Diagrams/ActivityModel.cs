using System.Text.Json.Serialization;

namespace UMLForge.Diagram.Service.Models.Diagrams;

public class ActivityModel : BaseDiagramModel
{
    public override string Type => "Activity";

    [JsonPropertyName("startNode")]
    public string StartNode { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public List<string> Steps { get; set; } = new();

    [JsonPropertyName("decisions")]
    public List<ActivityDecision> Decisions { get; set; } = new();
}

public class ActivityDecision
{
    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("yesBranch")]
    public string YesBranch { get; set; } = string.Empty;

    [JsonPropertyName("noBranch")]
    public string NoBranch { get; set; } = string.Empty;
}
