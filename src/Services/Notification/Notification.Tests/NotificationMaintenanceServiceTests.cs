using Moq;
using UMLForge.Notification.Repositories.Repositories;
using UMLForge.Notification.Service.Services;

namespace UMLForge.Notification.Tests;

public sealed class NotificationMaintenanceServiceTests
{
    [Theory, Trait("BR", "BR-35")]
    [InlineData(0)]
    [InlineData(3)]
    public async Task PurgeReadAsync_CurrentTime_UsesThirtyDayUtcCutoffAndReturnsCount(int removed)
    {
        var now = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.FromHours(7));
        var cutoff = new DateTime(2026, 10, 13, 5, 0, 0, DateTimeKind.Utc);
        using var cancellation = new CancellationTokenSource();
        var repository = new Mock<INotificationEventRepository>(MockBehavior.Strict);
        repository.Setup(x => x.PurgeReadAsync(It.Is<DateTime>(t => t == cutoff && t.Kind == DateTimeKind.Utc), cancellation.Token))
            .ReturnsAsync(removed);

        var result = await new NotificationMaintenanceService(repository.Object, new FixedClock(now)).PurgeReadAsync(cancellation.Token);

        Assert.Equal(removed, result);
        repository.Verify(x => x.PurgeReadAsync(It.Is<DateTime>(t => t == cutoff && t.Kind == DateTimeKind.Utc), cancellation.Token), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact, Trait("BR", "BR-35")]
    public async Task PurgeReadAsync_Cancelled_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new Mock<INotificationEventRepository>(MockBehavior.Strict);
        repository.Setup(x => x.PurgeReadAsync(It.IsAny<DateTime>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new NotificationMaintenanceService(repository.Object, TimeProvider.System).PurgeReadAsync(cancellation.Token));
    }

    [Fact, Trait("BR", "BR-35")]
    public async Task PurgeReadAsync_RepositoryFails_DoesNotReportSuccess()
    {
        var repository = new Mock<INotificationEventRepository>(MockBehavior.Strict);
        repository.Setup(x => x.PurgeReadAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("controlled SQL outage"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new NotificationMaintenanceService(repository.Object, TimeProvider.System).PurgeReadAsync());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
