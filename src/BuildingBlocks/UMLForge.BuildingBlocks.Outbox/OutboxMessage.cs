namespace UMLForge.BuildingBlocks.Outbox;

/// <summary>Sự kiện chờ gửi lên Redis Streams; <see cref="Id"/> chính là EventId của envelope.</summary>
public class OutboxMessage
{
    public Guid Id { get; set; }

    public string Stream { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Envelope { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}

/// <summary>Sự kiện đã xử lý bởi 1 consumer group — chống xử lý trùng (idempotent).</summary>
public class InboxMessage
{
    public Guid EventId { get; set; }

    public string Consumer { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; }
}
