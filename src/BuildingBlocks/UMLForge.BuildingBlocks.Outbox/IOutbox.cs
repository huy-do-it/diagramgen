using Microsoft.EntityFrameworkCore;

namespace UMLForge.BuildingBlocks.Outbox;

/// <summary>DbContext của service phải có 2 bảng này (gọi <c>modelBuilder.ApplyOutboxInbox()</c>).</summary>
public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<InboxMessage> InboxMessages { get; }
}

/// <summary>
/// Ghi sự kiện vào bảng Outbox. CHỈ thêm vào DbContext — sự kiện được lưu cùng
/// <c>SaveChangesAsync</c> của nghiệp vụ (1 transaction), OutboxPublisher gửi lên Redis sau.
/// </summary>
public interface IOutbox
{
    void Add<T>(string stream, T payload) where T : class;
}
