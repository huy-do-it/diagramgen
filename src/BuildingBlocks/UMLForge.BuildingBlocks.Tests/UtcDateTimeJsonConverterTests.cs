using System.Text.Json;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.Tests;

/// <summary>DateTime đọc từ SQL Server (Kind = Unspecified) phải ra JSON kèm 'Z' để client hiển thị đúng giờ.</summary>
public sealed class UtcDateTimeJsonConverterTests
{
    private sealed record Sample(DateTime At, DateTime? Optional);

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new UtcDateTimeJsonConverter() },
    };

    [Fact]
    public void Write_UnspecifiedKind_TreatedAsUtcWithZ()
    {
        var value = new DateTime(2026, 11, 13, 13, 32, 54, DateTimeKind.Unspecified);

        var json = JsonSerializer.Serialize(new Sample(value, value), Options);

        Assert.Equal("{\"at\":\"2026-11-13T13:32:54Z\",\"optional\":\"2026-11-13T13:32:54Z\"}", json);
    }

    [Fact]
    public void Write_UtcKind_KeptAsIs()
    {
        var value = new DateTime(2026, 11, 13, 13, 32, 54, DateTimeKind.Utc);

        var json = JsonSerializer.Serialize(new Sample(value, null), Options);

        Assert.Equal("{\"at\":\"2026-11-13T13:32:54Z\",\"optional\":null}", json);
    }

    [Fact]
    public void Write_LocalKind_ConvertedToUtc()
    {
        var utc = new DateTime(2026, 11, 13, 13, 32, 54, DateTimeKind.Utc);

        var json = JsonSerializer.Serialize(new Sample(utc.ToLocalTime(), null), Options);

        Assert.Contains("\"at\":\"2026-11-13T13:32:54Z\"", json);
    }

    [Fact]
    public void Read_IsoStringWithZ_RoundTrips()
    {
        var sample = JsonSerializer.Deserialize<Sample>("{\"at\":\"2026-11-13T13:32:54Z\",\"optional\":null}", Options);

        Assert.Equal(new DateTime(2026, 11, 13, 13, 32, 54, DateTimeKind.Utc), sample!.At.ToUniversalTime());
        Assert.Null(sample.Optional);
    }
}
