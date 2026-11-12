using FluentValidation;
using Moq;
using UMLForge.Notification.Repositories.Repositories;
using UMLForge.Notification.Service.Models;
using UMLForge.Notification.Service.Services;
using UMLForge.Notification.Service.Validators;
using NotificationEntity = UMLForge.Notification.Repositories.Entities.Notification;

namespace UMLForge.Notification.Tests;

public sealed class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> _repository = new(MockBehavior.Strict);
    private static readonly DateTime Now = new(2026, 11, 12, 4, 0, 0, DateTimeKind.Utc);
    private NotificationService Service() => new(_repository.Object, new NotificationQueryValidator(), new FixedTime());

    [Fact, Trait("BR", "BR-33")]
    public async Task MarkReadAsync_OtherOwner_ReturnsForbiddenWithoutUpdate()
    {
        var id = Guid.NewGuid();
        _repository.Setup(x => x.GetOwnerIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        Assert.Equal(MarkReadOutcome.Forbidden, await Service().MarkReadAsync(Guid.NewGuid(), id));
        _repository.Verify(x => x.MarkReadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("BR", "BR-33")]
    public async Task MarkReadAsync_MissingNotification_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _repository.Setup(x => x.GetOwnerIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        Assert.Equal(MarkReadOutcome.NotFound, await Service().MarkReadAsync(Guid.NewGuid(), id));
    }

    [Theory, Trait("BR", "BR-33")]
    [InlineData(0)] [InlineData(1)]
    public async Task MarkReadAsync_OwnNotification_UsesOwnerAndUtcTime(int affected)
    {
        var user = Guid.NewGuid(); var id = Guid.NewGuid();
        _repository.Setup(x => x.GetOwnerIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _repository.Setup(x => x.MarkReadAsync(id, user, Now, It.IsAny<CancellationToken>())).ReturnsAsync(affected);
        Assert.Equal(MarkReadOutcome.Success, await Service().MarkReadAsync(user, id));
    }

    [Fact, Trait("BR", "BR-33")]
    public async Task MarkAllReadAsync_CurrentUser_PassesOnlyCurrentOwner()
    {
        var user = Guid.NewGuid();
        _repository.Setup(x => x.MarkAllReadAsync(user, Now, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        Assert.Equal(3, await Service().MarkAllReadAsync(user));
    }

    [Fact, Trait("BR", "BR-33")]
    public async Task CountUnreadAsync_CurrentUser_PassesOnlyCurrentOwner()
    {
        var user = Guid.NewGuid();
        _repository.Setup(x => x.CountUnreadAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        Assert.Equal(2, await Service().CountUnreadAsync(user));
    }

    [Fact, Trait("BR", "BR-33")]
    public async Task GetPageAsync_FilteredQuery_UsesOwnerAndReturnsPagingContract()
    {
        var user = Guid.NewGuid();
        var item = new NotificationEntity { UserId = user, Type = "RenderCompleted", Title = "Render xong", Link = "/diagrams/1", CreatedAt = Now };
        _repository.Setup(x => x.GetPageAsync(user, 2, 2, true, "RenderCompleted", "Render", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<NotificationEntity>)new[] { item }, 3));
        var page = await Service().GetPageAsync(user, new NotificationQuery
        {
            Page = 2, PageSize = 2, UnreadOnly = true, Type = " RenderCompleted ", Search = " Render ", Sort = "createdAt:asc"
        });
        Assert.Equal(2, page.TotalPages); Assert.Equal(3, page.TotalItems); Assert.Equal(2, page.Page);
        Assert.Equal(item.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task GetPageAsync_EmptyList_ReturnsZeroPages()
    {
        var user = Guid.NewGuid();
        _repository.Setup(x => x.GetPageAsync(user, 1, 20, false, null, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<NotificationEntity>)Array.Empty<NotificationEntity>(), 0));
        var page = await Service().GetPageAsync(user, new NotificationQuery());
        Assert.Empty(page.Items); Assert.Equal(0, page.TotalPages);
    }

    [Theory]
    [InlineData(0, 20)] [InlineData(-1, 20)] [InlineData(1, 0)] [InlineData(1, 101)] [InlineData(int.MaxValue, 100)]
    public async Task GetPageAsync_InvalidPaging_RejectsBeforeRepository(int page, int size)
        => await Assert.ThrowsAsync<ValidationException>(() => Service().GetPageAsync(Guid.NewGuid(), new NotificationQuery { Page = page, PageSize = size }));

    [Theory]
    [InlineData("createdAt")] [InlineData("title:asc")] [InlineData("")]
    public async Task GetPageAsync_UnsupportedSort_RejectsBeforeRepository(string sort)
        => await Assert.ThrowsAsync<ValidationException>(() => Service().GetPageAsync(Guid.NewGuid(), new NotificationQuery { Sort = sort }));

    [Fact]
    public async Task GetPageAsync_OverlongFilter_RejectsBeforeRepository()
        => await Assert.ThrowsAsync<ValidationException>(() => Service().GetPageAsync(Guid.NewGuid(), new NotificationQuery { Type = new string('x', 81), Search = new string('x', 201) }));

    [Fact]
    public async Task SeedDevelopmentAsync_CurrentUser_AddsFiveOwnUnreadNotifications()
    {
        var user = Guid.NewGuid();
        NotificationEntity[]? created = null;
        _repository.Setup(x => x.AddRange(It.IsAny<IEnumerable<NotificationEntity>>()))
            .Callback<IEnumerable<NotificationEntity>>(x => created = x.ToArray());
        _repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(5);
        Assert.Equal(5, await Service().SeedDevelopmentAsync(user));
        Assert.Equal(5, created!.Length);
        Assert.All(created, x => { Assert.Equal(user, x.UserId); Assert.False(x.IsRead); Assert.Null(x.ReadAt); Assert.Equal(DateTimeKind.Utc, x.CreatedAt.Kind); });
        Assert.Equal(5, created.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public async Task CountUnreadAsync_CallerCancelled_PropagatesCancellation()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var user = Guid.NewGuid();
        _repository.Setup(x => x.CountUnreadAsync(user, source.Token)).ThrowsAsync(new OperationCanceledException(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().CountUnreadAsync(user, source.Token));
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
