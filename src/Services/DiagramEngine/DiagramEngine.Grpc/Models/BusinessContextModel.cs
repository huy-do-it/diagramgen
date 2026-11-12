using System.Text.Json.Serialization;

namespace UMLForge.DiagramEngine.Grpc.Models;

public class BusinessContextModel : BaseDiagramModel
{
    public override string Type => "BusinessContext";

    [JsonPropertyName("system")]
    public string System { get; set; } = string.Empty;

    [JsonPropertyName("internalComponents")]
    public List<string> InternalComponents { get; set; } = new();

    [JsonPropertyName("externalSystems")]
    public List<string> ExternalSystems { get; set; } = new();

    [JsonPropertyName("relations")]
    public List<ContextRelation> Relations { get; set; } = new();
}

public class ContextRelation
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }
}
