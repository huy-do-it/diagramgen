using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.EventBus;

/// <summary>
/// Xử lý 1 loại sự kiện. Chỉ cần sửa entity qua repository — EventConsumer tự lưu Inbox và gọi
/// <c>SaveChangesAsync</c> một lần sau khi handler chạy xong. Ném exception → message không bị ACK, sẽ được thử lại.
/// </summary>
public interface IEventHandler<T> where T : class
{
    Task HandleAsync(EventEnvelope<T> e, CancellationToken ct);
}
