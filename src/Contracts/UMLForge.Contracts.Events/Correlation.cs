using System.Diagnostics;

namespace UMLForge.Contracts.Events;

/// <summary>Tên header / metadata / thuộc tính log của CorrelationId, dùng chung mọi service.</summary>
public static class CorrelationKeys
{
    public const string HttpHeader = "X-Correlation-Id";
    public const string GrpcMetadata = "x-correlation-id";
    public const string Baggage = "correlation_id";
    public const string LogProperty = "CorrelationId";
}

/// <summary>
/// CorrelationId của luồng xử lý hiện tại (HTTP request hoặc 1 sự kiện), lưu trong baggage của
/// <see cref="Activity.Current"/> để Outbox, gRPC client và log đọc lại mà không cần truyền tham số.
/// </summary>
public static class CorrelationContext
{
    public static Guid? Current
        => Guid.TryParse(Activity.Current?.GetBaggageItem(CorrelationKeys.Baggage), out var id) ? id : null;

    /// <summary>Mở 1 Activity con mang CorrelationId; Dispose khi xử lý xong.</summary>
    public static Activity Begin(Guid correlationId, string operationName)
    {
        var activity = new Activity(operationName);
        activity.SetBaggage(CorrelationKeys.Baggage, correlationId.ToString());
        return activity.Start();
    }
}
