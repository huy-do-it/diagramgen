using System.Reflection;
using System.Text.Json;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.Tests;

/// <summary>
/// Mọi file mẫu trong tools/events (dùng bởi seed-events.ps1) phải khớp Contracts.Events:
/// đúng tên sự kiện + stream, đọc được bằng EventJson, đủ field bắt buộc, không có field lạ.
/// </summary>
public sealed class SampleEventFilesTests
{
    private static readonly NullabilityInfoContext Nullability = new();

    public static TheoryData<string> SampleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(EventsDirectory(), "*.json", SearchOption.AllDirectories).Order())
            data.Add(Path.GetRelativePath(EventsDirectory(), file));
        return data;
    }

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public void SampleFile_MatchesEventContract(string relativePath)
    {
        var json = File.ReadAllText(Path.Combine(EventsDirectory(), relativePath));
        using var document = JsonDocument.Parse(json);
        var type = document.RootElement.GetProperty("type").GetString()!;

        Assert.True(EventTypeRegistry.TryResolve(type, out var payloadType), $"'{type}' không có trong EventTypeRegistry");
        var folder = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        Assert.Equal(EventTypeRegistry.StreamOf(type), $"{folder}.events");

        // Đọc đúng như EventConsumer: EventJson.Deserialize<TPayload>
        var envelope = typeof(EventJson).GetMethod(nameof(EventJson.Deserialize))!
            .MakeGenericMethod(payloadType).Invoke(null, [json])!;
        var envelopeType = envelope.GetType();
        Assert.NotEqual(Guid.Empty, (Guid)envelopeType.GetProperty(nameof(EventEnvelope<object>.EventId))!.GetValue(envelope)!);
        Assert.Equal(EventEnvelope.CurrentVersion, (int)envelopeType.GetProperty(nameof(EventEnvelope<object>.Version))!.GetValue(envelope)!);
        var payload = envelopeType.GetProperty(nameof(EventEnvelope<object>.Payload))!.GetValue(envelope);
        Assert.NotNull(payload);

        // Field bắt buộc (không nullable) phải có giá trị
        foreach (var property in payloadType.GetProperties())
        {
            var value = property.GetValue(payload);
            if (property.PropertyType == typeof(Guid))
                Assert.True((Guid)value! != Guid.Empty, $"{type}.{property.Name} đang là Guid rỗng");
            else if (property.PropertyType == typeof(string) && Nullability.Create(property).ReadState == NullabilityState.NotNull)
                Assert.False(string.IsNullOrEmpty((string?)value), $"{type}.{property.Name} bị thiếu");
        }

        // Không có field gõ sai tên (System.Text.Json sẽ âm thầm bỏ qua)
        var known = payloadType.GetProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)).ToHashSet();
        var unknown = document.RootElement.GetProperty("payload").EnumerateObject().Select(field => field.Name).Where(name => !known.Contains(name));
        Assert.Empty(unknown);
    }

    [Fact]
    public void SampleFiles_EventIdsAreUnique()
    {
        var ids = Directory.EnumerateFiles(EventsDirectory(), "*.json", SearchOption.AllDirectories)
            .Select(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("eventId").GetString())
            .ToList();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void SampleFiles_CoverEveryRegisteredEvent()
    {
        var types = Directory.EnumerateFiles(EventsDirectory(), "*.json", SearchOption.AllDirectories)
            .Select(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("type").GetString())
            .ToHashSet();

        var missing = EventTypeRegistry.Names.Where(name => !types.Contains(name)).ToList();
        Assert.True(missing.Count <= MissingAllowed.Count && missing.All(MissingAllowed.Contains),
            $"Thiếu file mẫu cho: {string.Join(", ", missing)}");
    }

    // Sự kiện xóa/khóa chưa cần cho kịch bản demo CampusEats
    private static readonly HashSet<string> MissingAllowed = [nameof(UserRoleChanged), nameof(UserLocked), nameof(MemberRemoved), nameof(ProjectDeleted)];

    private static string EventsDirectory()
    {
        // Đi ngược từ thư mục bin tới gốc repo (nơi có global.json)
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
            directory = directory.Parent;
        return directory is null
            ? throw new DirectoryNotFoundException("Không tìm thấy gốc repo (global.json).")
            : Path.Combine(directory.FullName, "tools", "events");
    }
}
