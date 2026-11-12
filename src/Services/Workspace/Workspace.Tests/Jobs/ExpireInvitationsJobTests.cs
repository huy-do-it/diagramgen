using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Workspace.Repositories.Data;
using Workspace.Repositories.Entities;
using Workspace.Repositories.Repositories;
using Workspace.Service.Jobs;
using Workspace.Tests.Helpers;

namespace Workspace.Tests.Jobs;

public class ExpireInvitationsJobTests
{
    private readonly WorkspaceDbContext _context;
    private readonly InvitationRepository _invitationRepo;

    public ExpireInvitationsJobTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _invitationRepo = new InvitationRepository(_context);
    }

    [Fact]
    public async Task RunNowAsync_MarksExpiredInvitationsAsExpired()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var expiredInv1 = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            InviteeId = Guid.NewGuid(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(-3)
        };
        var expiredInv2 = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            InviteeId = Guid.NewGuid(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            ExpiresAt = DateTime.UtcNow.AddSeconds(-10)
        };
        var activeInv = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            InviteeId = Guid.NewGuid(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await _context.Invitations.AddRangeAsync(expiredInv1, expiredInv2, activeInv);
        await _context.SaveChangesAsync();

        // Setup ServiceScopeFactory for job execution
        var services = new ServiceCollection();
        services.AddScoped<IInvitationRepository>(_ => new InvitationRepository(_context));
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var job = new ExpireInvitationsJob(scopeFactory, NullLogger<ExpireInvitationsJob>.Instance);

        // Act
        var result = await job.RunNowAsync("manual_test", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.Summary.Should().Be("Expired 2 invitations");

        var updatedInv1 = await _context.Invitations.FindAsync(expiredInv1.Id);
        updatedInv1!.Status.Should().Be("Expired");

        var updatedInv2 = await _context.Invitations.FindAsync(expiredInv2.Id);
        updatedInv2!.Status.Should().Be("Expired");

        var updatedActiveInv = await _context.Invitations.FindAsync(activeInv.Id);
        updatedActiveInv!.Status.Should().Be("Pending");
    }
}
