namespace Identity.Repositories.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Stream { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Envelope { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public int Attempts { get; set; } = 0;
    public string? LastError { get; set; }
}
