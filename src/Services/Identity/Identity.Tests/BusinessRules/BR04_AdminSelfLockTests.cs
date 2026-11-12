using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;
using Identity.Repositories.Repositories;
using Identity.Service.DTOs;
using Identity.Service.Exceptions;
using Identity.Service.Services;
using Identity.Service.Validators;
using Identity.Tests.Helpers;

namespace Identity.Tests.BusinessRules;

[Trait("Trait", "BR")]
public class BR04_AdminSelfLockTests
{
    private readonly IdentityDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly RefreshTokenRepository _refreshTokenRepository;
    private readonly Outbox _outbox;
    private readonly UserService _userService;

    public BR04_AdminSelfLockTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _userRepository = new UserRepository(_context);
        _refreshTokenRepository = new RefreshTokenRepository(_context);
        _outbox = new Outbox(_context);

        _userService = new UserService(
            _userRepository,
            _refreshTokenRepository,
            _outbox,
            new ChangeRoleRequestValidator()
        );
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task LockUser_WhenAdminAttemptsToLockSelf_ThrowsBusinessRuleExceptionWith403AndBR04Code()
    {
        // Arrange: Admin user
        var adminId = Guid.NewGuid();
        var adminUser = new User
        {
            Id = adminId,
            Username = "admin_super",
            PasswordHash = "hash",
            FullName = "Admin Super",
            Role = "Admin",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(adminUser);
        await _userRepository.SaveChangesAsync();

        // Act: Admin tries to lock themselves
        var act = async () => await _userService.LockUserAsync(adminId, adminId, isLocked: true);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-04");
        exception.Which.StatusCode.Should().Be(403);
        exception.Which.Message.Should().Contain("tự khóa");
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task LockUser_WhenAdminLocksAnotherUser_SucceedsRevokesTokensAndQueuesOutboxEvent()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var adminUser = new User
        {
            Id = adminId,
            Username = "admin_user",
            PasswordHash = "hash",
            FullName = "Admin User",
            Role = "Admin",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };

        var targetUser = new User
        {
            Id = targetId,
            Username = "bad_user",
            PasswordHash = "hash",
            FullName = "Bad User",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };

        var activeToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = targetId,
            TokenHash = "some_token_hash_value_1234567890abcdef1234567890abcdef1234567890abcdef",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            User = targetUser
        };

        await _userRepository.AddAsync(adminUser);
        await _userRepository.AddAsync(targetUser);
        await _refreshTokenRepository.AddAsync(activeToken);
        await _userRepository.SaveChangesAsync();

        // Act: Admin locks target user
        var response = await _userService.LockUserAsync(adminId, targetId, isLocked: true);

        // Assert
        response.Should().NotBeNull();
        response.IsLocked.Should().BeTrue();

        // Verify user is locked in database
        var updated = await _context.Users.FindAsync(targetId);
        updated!.IsLocked.Should().BeTrue();

        // Verify refresh token was revoked
        var tokenAfter = await _context.RefreshTokens.FindAsync(activeToken.Id);
        tokenAfter!.RevokedAt.Should().NotBeNull();

        // Verify UserLocked event was written to Outbox
        var outboxMessage = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "UserLocked");
        outboxMessage.Should().NotBeNull();
        outboxMessage!.Envelope.Should().Contain(targetId.ToString());
        outboxMessage.Envelope.Should().Contain("true");
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task ChangeRole_WhenValidRoleRequested_UpdatesRoleAndQueuesOutboxEvent()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var student = new User
        {
            Id = studentId,
            Username = "promoted_student",
            PasswordHash = "hash",
            FullName = "Promoted Student",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(student);
        await _userRepository.SaveChangesAsync();

        // Act: Promote to Lecturer
        var response = await _userService.ChangeRoleAsync(adminId, studentId, "Lecturer");

        // Assert
        response.Should().NotBeNull();
        response.Role.Should().Be("Lecturer");

        var updated = await _context.Users.FindAsync(studentId);
        updated!.Role.Should().Be("Lecturer");

        // Verify UserRoleChanged event in Outbox
        var outboxMessage = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "UserRoleChanged");
        outboxMessage.Should().NotBeNull();
        outboxMessage!.Envelope.Should().Contain("Lecturer");
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task LockUser_WhenUnlockingAnotherUser_SucceedsAndQueuesUserLockedFalseEvent()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var adminUser = new User
        {
            Id = adminId,
            Username = "admin_unlocker",
            PasswordHash = "hash",
            FullName = "Admin Unlocker",
            Role = "Admin",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };

        var targetUser = new User
        {
            Id = targetId,
            Username = "locked_user",
            PasswordHash = "hash",
            FullName = "Locked User",
            Role = "Student",
            IsLocked = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(adminUser);
        await _userRepository.AddAsync(targetUser);
        await _userRepository.SaveChangesAsync();

        // Act: Admin unlocks the target user
        var response = await _userService.LockUserAsync(adminId, targetId, isLocked: false);

        // Assert
        response.Should().NotBeNull();
        response.IsLocked.Should().BeFalse();

        var updated = await _context.Users.FindAsync(targetId);
        updated!.IsLocked.Should().BeFalse();

        var outboxMessage = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "UserLocked" && m.Envelope.Contains("false"));
        outboxMessage.Should().NotBeNull();
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task LockUser_WhenAdminUnlocksSelf_AllowedWithoutException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var adminUser = new User
        {
            Id = adminId,
            Username = "admin_self_unlock",
            PasswordHash = "hash",
            FullName = "Admin Self Unlock",
            Role = "Admin",
            IsLocked = true,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(adminUser);
        await _userRepository.SaveChangesAsync();

        // Act: Admin unlocks self (isLocked = false) -> Allowed!
        var response = await _userService.LockUserAsync(adminId, adminId, isLocked: false);

        // Assert
        response.Should().NotBeNull();
        response.IsLocked.Should().BeFalse();

        var updated = await _context.Users.FindAsync(adminId);
        updated!.IsLocked.Should().BeFalse();
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task LockUser_WhenTargetUserNotFound_ThrowsBusinessRuleExceptionWith404()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = async () => await _userService.LockUserAsync(adminId, nonExistentId, isLocked: true);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.StatusCode.Should().Be(404);
        exception.Which.Code.Should().Be("NOT_FOUND");
    }
}
