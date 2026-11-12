using System.Text.Json.Serialization;

namespace UMLForge.Diagram.Service.Models.Diagrams;

public class StateModel : BaseDiagramModel
{
    public override string Type => "State";

    [JsonPropertyName("initialState")]
    public string InitialState { get; set; } = string.Empty;

    [JsonPropertyName("states")]
    public List<string> States { get; set; } = new();

    [JsonPropertyName("transitions")]
    public List<StateTransition> Transitions { get; set; } = new();
}

public class StateTransition
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;
}
