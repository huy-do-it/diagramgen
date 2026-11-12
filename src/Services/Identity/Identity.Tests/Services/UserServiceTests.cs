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

namespace Identity.Tests.Services;

public class UserServiceTests
{
    private readonly IdentityDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly RefreshTokenRepository _refreshTokenRepository;
    private readonly Outbox _outbox;
    private readonly UserService _userService;

    public UserServiceTests()
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
    public async Task GetPagedUsersAsync_ReturnsPaginatedUsersCorrectly()
    {
        // Arrange: Seed 25 users
        for (int i = 1; i <= 25; i++)
        {
            _context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = $"user_{i:D2}",
                FullName = $"User Number {i}",
                PasswordHash = "hash",
                Role = i % 2 == 0 ? "Student" : "Lecturer",
                IsLocked = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await _context.SaveChangesAsync();

        // Act: Fetch page 2 with page size 10
        var result = await _userService.GetPagedUsersAsync(
            search: null,
            role: null,
            isLocked: null,
            sort: "username:asc",
            page: 2,
            pageSize: 10
        );

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalItems.Should().Be(25);
        result.TotalPages.Should().Be(3);
        result.Items.Should().HaveCount(10);
        result.Items[0].Username.Should().Be("user_11");
    }

    [Fact]
    public async Task GetPagedUsersAsync_WithSearchFilter_FiltersByUsernameOrFullName()
    {
        // Arrange
        _context.Users.AddRange(
            new User { Id = Guid.NewGuid(), Username = "alice_dev", FullName = "Alice Wonderland", PasswordHash = "h", Role = "Student", IsLocked = false, CreatedAt = DateTime.UtcNow },
            new User { Id = Guid.NewGuid(), Username = "bob_smith", FullName = "Bob Alice", PasswordHash = "h", Role = "Student", IsLocked = false, CreatedAt = DateTime.UtcNow },
            new User { Id = Guid.NewGuid(), Username = "charlie_brown", FullName = "Charlie Chaplin", PasswordHash = "h", Role = "Student", IsLocked = false, CreatedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        // Act: search for "alice"
        var result = await _userService.GetPagedUsersAsync(
            search: "alice",
            role: null,
            isLocked: null,
            sort: null,
            page: 1,
            pageSize: 20
        );

        // Assert
        result.TotalItems.Should().Be(2);
        result.Items.Select(u => u.Username).Should().Contain(["alice_dev", "bob_smith"]);
    }

    [Fact]
    public async Task GetPagedUsersAsync_WithRoleAndLockFilter_FiltersCorrectly()
    {
        // Arrange
        _context.Users.AddRange(
            new User { Id = Guid.NewGuid(), Username = "lec_locked", FullName = "Lecturer 1", PasswordHash = "h", Role = "Lecturer", IsLocked = true, CreatedAt = DateTime.UtcNow },
            new User { Id = Guid.NewGuid(), Username = "lec_active", FullName = "Lecturer 2", PasswordHash = "h", Role = "Lecturer", IsLocked = false, CreatedAt = DateTime.UtcNow },
            new User { Id = Guid.NewGuid(), Username = "stu_locked", FullName = "Student 1", PasswordHash = "h", Role = "Student", IsLocked = true, CreatedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        // Act: filter for Lecturer + IsLocked = true
        var result = await _userService.GetPagedUsersAsync(
            search: null,
            role: "Lecturer",
            isLocked: true,
            sort: null,
            page: 1,
            pageSize: 20
        );

        // Assert
        result.TotalItems.Should().Be(1);
        result.Items[0].Username.Should().Be("lec_locked");
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenRoleIsInvalid_ThrowsBusinessRuleExceptionWith400()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = targetId,
            Username = "user_role_invalid",
            FullName = "Test",
            PasswordHash = "h",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act: invalid role "SuperGod"
        var act = async () => await _userService.ChangeRoleAsync(adminId, targetId, "SuperGod");

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.StatusCode.Should().Be(400);
        ex.Which.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenUserDoesNotExist_ThrowsBusinessRuleExceptionWith404()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = async () => await _userService.ChangeRoleAsync(adminId, nonExistentId, "Admin");

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.StatusCode.Should().Be(404);
        ex.Which.Code.Should().Be("NOT_FOUND");
    }
}
