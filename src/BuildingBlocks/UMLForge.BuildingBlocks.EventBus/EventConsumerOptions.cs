namespace UMLForge.BuildingBlocks.EventBus;

/// <summary>Cấu hình 1 consumer (1 stream + 1 group). Mặc định theo ARCHITECTURE §6.3.</summary>
public sealed class EventConsumerOptions
{
    internal EventConsumerOptions(string stream, string group)
    {
        Stream = stream;
        Group = group;
    }

    public string Stream { get; }

    public string Group { get; }

    /// <summary>Nghỉ khi không có message mới.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    public int BatchSize { get; set; } = 10;

    /// <summary>Chu kỳ quét message chưa ACK để thử lại.</summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Message phải "nằm im" ít nhất bấy nhiêu mới được nhận lại từ consumer khác.</summary>
    public TimeSpan MinIdleBeforeRetry { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Giao quá số lần này vẫn lỗi → chuyển sang <c>&lt;stream&gt;.dlq</c>.</summary>
    public int MaxDeliveries { get; set; } = 5;

    internal Dictionary<string, EventDispatcher> Handlers { get; } = new(StringComparer.Ordinal);
}
