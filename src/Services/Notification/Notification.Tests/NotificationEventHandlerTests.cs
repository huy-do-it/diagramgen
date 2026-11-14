using FluentValidation;
using Moq;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.Contracts.Events;
using UMLForge.Notification.Repositories.Entities;
using UMLForge.Notification.Repositories.Repositories;
using UMLForge.Notification.Service.EventHandlers;
using UMLForge.Notification.Service.Validators;
using NotificationEntity = UMLForge.Notification.Repositories.Entities.Notification;
namespace UMLForge.Notification.Tests;
public sealed class NotificationEventHandlerTests
{
    private readonly Guid project = Guid.NewGuid(), leader = Guid.NewGuid(), member = Guid.NewGuid(), lecturer = Guid.NewGuid();
    private readonly Mock<INotificationEventRepository> repo = new(MockBehavior.Strict);
    private readonly List<NotificationEntity> notifications = [];
    private NotificationEventHandler Handler(List<ProjectRecipient>? rows = null)
    {
        repo.Setup(x => x.RecipientsAsync(project, It.IsAny<CancellationToken>())).ReturnsAsync(rows ?? Rows());
        repo.Setup(x => x.AddNotification(It.IsAny<NotificationEntity>())).Callback<NotificationEntity>(notifications.Add);
        return new(repo.Object, new NotificationEventValidator(), TimeProvider.System);
    }
    private List<ProjectRecipient> Rows() => [new() { ProjectId = project, UserId = leader, Role = "Leader", ProjectName = "Project" }, new() { ProjectId = project, UserId = member, Role = "Member", ProjectName = "Project" }, new() { ProjectId = project, UserId = lecturer, Role = "Lecturer", ProjectName = "Project" }];
    private Task Send<T>(NotificationEventHandler handler, T payload) where T : class
        => ((IEventHandler<T>)(object)handler).HandleAsync(EventEnvelope.Create("fixture", payload), default);
    [Theory, Trait("BR", "BR-34")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_RenderOutcome_NotifiesRequesterWithoutMembership(bool success)
    {
        var h = Handler();
        if (success) await Send(h, new RenderCompleted(Guid.NewGuid(), Guid.NewGuid(), project, 1, "Diagram", "<svg/>", "code", 10, member));
        else await Send(h, new RenderFailed(Guid.NewGuid(), Guid.NewGuid(), project, 1, "Diagram", "failure", member));
        Assert.Equal(member, Assert.Single(notifications).UserId);
        Assert.StartsWith("/diagrams/", notifications[0].Link);
        repo.Verify(x => x.RecipientsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory, Trait("BR", "BR-34")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_Invitation_ExcludesActor(bool self)
    {
        await Send(Handler(), new InvitationSent(Guid.NewGuid(), project, "Project", member, self ? member : leader));
        Assert.Equal(self ? 0 : 1, notifications.Count);
        if (!self)
        {
            Assert.Equal(member, notifications[0].UserId);
            Assert.Equal("/invitations/mine", notifications[0].Link);
        }
    }
    [Fact, Trait("BR", "BR-34")]
    public async Task HandleAsync_ReviewSubmitted_NotifiesLecturerOnly()
    {
        await Send(Handler(), new ReviewSubmitted(Guid.NewGuid(), project, leader));
        Assert.Equal(lecturer, Assert.Single(notifications).UserId);
    }
    [Fact, Trait("BR", "BR-34")]
    public async Task HandleAsync_ReviewDecided_NotifiesMembersWithoutActor()
    {
        await Send(Handler(), new ReviewDecided(Guid.NewGuid(), project, "Approved", null, lecturer));
        Assert.Equal(new[] { leader, member }.Order(), notifications.Select(x => x.UserId).Order());
    }
    [Fact, Trait("BR", "BR-34")]
    public async Task HandleAsync_Comment_NotifiesOtherMembersOnly()
    {
        await Send(Handler(), new CommentAdded(Guid.NewGuid(), Guid.NewGuid(), project, "Diagram", member));
        Assert.Equal(leader, Assert.Single(notifications).UserId);
    }
    [Fact, Trait("BR", "BR-34")]
    public async Task HandleAsync_ReviewSubmittedByLecturer_DoesNotNotifyActor()
    {
        await Send(Handler(), new ReviewSubmitted(Guid.NewGuid(), project, lecturer));
        Assert.Empty(notifications);
    }
    [Fact, Trait("BR", "BR-34")]
    public async Task HandleAsync_ReviewDecidedByMember_ExcludesActorFromRecipients()
    {
        await Send(Handler(), new ReviewDecided(Guid.NewGuid(), project, "Approved", null, member));
        Assert.Equal(leader, Assert.Single(notifications).UserId);
    }
    [Fact]
    public async Task HandleAsync_ProjectCreated_UpsertsLeader()
    {
        var rows = new List<ProjectRecipient>(); repo.Setup(x => x.AddRecipient(It.IsAny<ProjectRecipient>())).Callback<ProjectRecipient>(rows.Add);
        var h = Handler(rows); var payload = new ProjectCreated(project, "Project", leader, leader);
        await Send(h, payload); await Send(h, payload);
        Assert.Equal("Leader", Assert.Single(rows).Role); Assert.Empty(notifications);
    }
    [Fact, Trait("BR", "BR-34")]
    public async Task HandleAsync_MemberAdded_UpdatesReadModelAndNotifiesLeader()
    {
        var rows = Rows(); var user = Guid.NewGuid(); repo.Setup(x => x.AddRecipient(It.IsAny<ProjectRecipient>())).Callback<ProjectRecipient>(rows.Add);
        await Send(Handler(rows), new MemberAdded(project, "Renamed", user, "Member", member));
        Assert.Contains(rows, x => x.UserId == user && x.Role == "Member"); Assert.All(rows, x => Assert.Equal("Renamed", x.ProjectName));
        Assert.Equal(leader, Assert.Single(notifications).UserId);
    }
    [Fact]
    public async Task HandleAsync_MemberRemoved_RemovesOnlyMatchingUser()
    {
        List<ProjectRecipient>? removed = null; repo.Setup(x => x.RemoveRecipients(It.IsAny<IEnumerable<ProjectRecipient>>())).Callback<IEnumerable<ProjectRecipient>>(x => removed = x.ToList());
        await Send(Handler(), new MemberRemoved(project, member, leader)); Assert.Equal(member, Assert.Single(removed!).UserId);
    }
    [Fact]
    public async Task HandleAsync_LecturerAssigned_ReplacesPreviousLecturer()
    {
        var rows = Rows(); List<ProjectRecipient>? removed = null; repo.Setup(x => x.RemoveRecipients(It.IsAny<IEnumerable<ProjectRecipient>>())).Callback<IEnumerable<ProjectRecipient>>(x => removed = x.ToList());
        repo.Setup(x => x.AddRecipient(It.IsAny<ProjectRecipient>())).Callback<ProjectRecipient>(rows.Add);
        var next = Guid.NewGuid(); await Send(Handler(rows), new LecturerAssigned(project, "Project", next, lecturer, leader));
        Assert.Equal(lecturer, Assert.Single(removed!).UserId); Assert.Contains(rows, x => x.UserId == next && x.Role == "Lecturer");
    }
    [Fact]
    public async Task HandleAsync_ProjectDeleted_RemovesAllProjectRecipients()
    {
        repo.Setup(x => x.RemoveRecipients(It.Is<IEnumerable<ProjectRecipient>>(rows => rows.Count() == 3)));
        await Send(Handler(), new ProjectDeleted(project, leader)); repo.Verify(x => x.RemoveRecipients(It.Is<IEnumerable<ProjectRecipient>>(rows => rows.Count() == 3)), Times.Once); Assert.Empty(notifications);
    }
    [Fact]
    public async Task HandleAsync_ReadModelMissing_ThrowsForSharedRetry()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Send(Handler([]), new ReviewSubmitted(Guid.NewGuid(), project, leader)));
        Assert.Empty(notifications);
    }
    [Fact]
    public async Task HandleAsync_LecturerNotYetSynchronized_ThrowsForSharedRetry()
    {
        var rows = Rows().Where(x => x.Role != "Lecturer").ToList();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Send(Handler(rows), new ReviewSubmitted(Guid.NewGuid(), project, leader)));
        Assert.Empty(notifications);
    }
    [Fact, Trait("BR", "BR-34")]
    public async Task HandleAsync_MemberAddedByLeader_DoesNotNotifyActor()
    {
        await Send(Handler(), new MemberAdded(project, "Project", member, "Member", leader));
        Assert.Empty(notifications);
    }
    [Fact]
    public async Task HandleAsync_InvalidEvent_RejectsBeforeRepositoryAccess()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Send(Handler(), new ProjectCreated(Guid.Empty, "Project", leader, leader)));
        repo.Verify(x => x.RecipientsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
