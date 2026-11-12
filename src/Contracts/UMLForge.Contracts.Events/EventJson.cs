using System.Text.Json;

namespace UMLForge.Contracts.Events;

/// <summary>Cấu hình JSON thống nhất cho envelope: camelCase, không phân biệt hoa thường khi đọc.</summary>
public static class EventJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(EventEnvelope<T> envelope)
        => JsonSerializer.Serialize(envelope, Options);

    public static EventEnvelope<T>? Deserialize<T>(string json)
        => JsonSerializer.Deserialize<EventEnvelope<T>>(json, Options);
}
