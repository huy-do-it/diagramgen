using System.Text.Json;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Service.Models.Diagrams;

namespace UMLForge.Diagram.Service.Helpers;

public static class DiagramModelParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static BaseDiagramModel Parse(DiagramType type, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Diagram model JSON cannot be empty.", nameof(json));
        }

        try
        {
            return type switch
            {
                DiagramType.BusinessContext => JsonSerializer.Deserialize<BusinessContextModel>(json, JsonOptions)
                    ?? throw new JsonException("Deserialization returned null."),
                DiagramType.UseCase => JsonSerializer.Deserialize<UseCaseModel>(json, JsonOptions)
                    ?? throw new JsonException("Deserialization returned null."),
                DiagramType.Sequence => JsonSerializer.Deserialize<SequenceModel>(json, JsonOptions)
                    ?? throw new JsonException("Deserialization returned null."),
                DiagramType.Activity => JsonSerializer.Deserialize<ActivityModel>(json, JsonOptions)
                    ?? throw new JsonException("Deserialization returned null."),
                DiagramType.State => JsonSerializer.Deserialize<StateModel>(json, JsonOptions)
                    ?? throw new JsonException("Deserialization returned null."),
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported diagram type: {type}")
            };
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Malformed diagram JSON for type '{type}': {ex.Message}", ex);
        }
    }

    public static bool TryParse(DiagramType type, string json, out BaseDiagramModel? model, out string? error)
    {
        try
        {
            model = Parse(type, json);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            model = null;
            error = ex.Message;
            return false;
        }
    }
}
