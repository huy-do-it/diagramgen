using System.Text.Json;
using System.Text.Json.Serialization;

namespace UMLForge.Contracts.Events;

/// <summary>
/// Mọi DateTime trong hệ thống là UTC (RULES §6), nhưng giá trị đọc từ SQL Server có Kind = Unspecified
/// → JSON thiếu 'Z' và trình duyệt hiểu nhầm là giờ địa phương (lệch 7 tiếng ở Việt Nam).
/// Converter luôn ghi kèm 'Z'; DateTime? dùng chung converter này.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        });
}
