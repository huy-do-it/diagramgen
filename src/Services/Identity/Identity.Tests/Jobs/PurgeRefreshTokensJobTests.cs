using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;
using Identity.Service.Jobs;
using Identity.Tests.Helpers;

namespace Identity.Tests.Jobs;

public class PurgeRefreshTokensJobTests
{
    private readonly IdentityDbContext _context;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<ILogger<PurgeRefreshTokensJob>> _loggerMock;
    private readonly PurgeRefreshTokensJob _job;

    public PurgeRefreshTokensJobTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IdentityDbContext))).Returns(_context);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        _loggerMock = new Mock<ILogger<PurgeRefreshTokensJob>>();

        _job = new PurgeRefreshTokensJob(_scopeFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void JobName_ShouldBe_PurgeTokens()
    {
        _job.JobName.Should().Be("purge-tokens");
    }

    [Fact]
    public async Task ExecuteDirectlyAsync_WhenTokensExpiredMoreThan7Days_DeletesThem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "purge_user_1",
            PasswordHash = "hash",
            FullName = "Purge User 1",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        // Token expired 10 days ago (should be purged)
        var oldExpiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "old_expired_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(-10),
            CreatedAt = DateTime.UtcNow.AddDays(-17)
        };

        // Token revoked 8 days ago (should be purged)
        var oldRevokedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "old_revoked_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            RevokedAt = DateTime.UtcNow.AddDays(-8),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        // Token expired 3 days ago (less than 7 days - should NOT be purged)
        var recentExpiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "recent_expired_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(-3),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        // Active token expiring in 7 days (should NOT be purged)
        var activeToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "active_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        // Token revoked 2 days ago (less than 7 days - should NOT be purged)
        var recentlyRevokedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "recently_revoked_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            RevokedAt = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };

        _context.RefreshTokens.AddRange(oldExpiredToken, oldRevokedToken, recentExpiredToken, activeToken, recentlyRevokedToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _job.ExecuteDirectlyAsync();

        // Assert
        result.Should().Contain("Purged 2 expired or revoked refresh tokens");

        var remainingTokens = await _context.RefreshTokens.ToListAsync();
        remainingTokens.Should().HaveCount(3);
        remainingTokens.Select(t => t.Id).Should().Contain([recentExpiredToken.Id, activeToken.Id, recentlyRevokedToken.Id]);
        remainingTokens.Select(t => t.Id).Should().NotContain([oldExpiredToken.Id, oldRevokedToken.Id]);
    }

    [Fact]
    public async Task ExecuteDirectlyAsync_WhenNoEligibleTokens_ReturnsZeroPurged()
    {
        // Arrange: only active tokens
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "purge_user_2",
            PasswordHash = "hash",
            FullName = "Purge User 2",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var activeToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "active_hash_2",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(activeToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _job.ExecuteDirectlyAsync();

        // Assert
        result.Should().Contain("Purged 0 expired or revoked refresh tokens");
        var count = await _context.RefreshTokens.CountAsync();
        count.Should().Be(1);
    }
}
