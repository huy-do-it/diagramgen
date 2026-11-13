using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;
using Identity.Service.Jobs;
using Identity.Tests.Helpers;

namespace Identity.Tests.Jobs;

public class PurgeRefreshTokensJobTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly PurgeRefreshTokensJob _job;

    public PurgeRefreshTokensJobTests()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => TestDbContextFactory.CreateInMemoryDbContext(_dbName));
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _job = new PurgeRefreshTokensJob(scopeFactory, NullLogger<PurgeRefreshTokensJob>.Instance);
    }

    [Fact]
    public void JobName_ShouldBe_PurgeRefreshTokens()
    {
        _job.JobName.Should().Be("purge-refresh-tokens");
    }

    [Fact]
    public async Task RunNowAsync_WhenTokensExpiredMoreThan7Days_DeletesThem()
    {
        // Arrange
        using (var setupContext = TestDbContextFactory.CreateInMemoryDbContext(_dbName))
        {
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
            setupContext.Users.Add(user);

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

            setupContext.RefreshTokens.AddRange(oldExpiredToken, oldRevokedToken, recentExpiredToken, activeToken, recentlyRevokedToken);
            await setupContext.SaveChangesAsync();
        }

        // Act
        var result = await _job.RunNowAsync("manual_test", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.Summary.Should().Contain("Purged 2 expired or revoked refresh tokens");

        using (var verifyContext = TestDbContextFactory.CreateInMemoryDbContext(_dbName))
        {
            var remainingTokens = await verifyContext.RefreshTokens.ToListAsync();
            remainingTokens.Should().HaveCount(3);
            remainingTokens.Select(t => t.TokenHash).Should().Contain(["recent_expired_hash", "active_hash", "recently_revoked_hash"]);
            remainingTokens.Select(t => t.TokenHash).Should().NotContain(["old_expired_hash", "old_revoked_hash"]);
        }
    }

    [Fact]
    public async Task RunNowAsync_WhenNoEligibleTokens_ReturnsZeroPurged()
    {
        // Arrange: only active tokens
        using (var setupContext = TestDbContextFactory.CreateInMemoryDbContext(_dbName))
        {
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
            setupContext.Users.Add(user);

            var activeToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = "active_hash_2",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };
            setupContext.RefreshTokens.Add(activeToken);
            await setupContext.SaveChangesAsync();
        }

        // Act
        var result = await _job.RunNowAsync("manual_test", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.Summary.Should().Contain("Purged 0 expired or revoked refresh tokens");

        using (var verifyContext = TestDbContextFactory.CreateInMemoryDbContext(_dbName))
        {
            var count = await verifyContext.RefreshTokens.CountAsync();
            count.Should().Be(1);
        }
    }
}
