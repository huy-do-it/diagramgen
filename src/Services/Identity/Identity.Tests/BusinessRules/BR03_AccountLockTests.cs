using FluentAssertions;
using Microsoft.Extensions.Options;
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
public class BR03_AccountLockTests
{
    private readonly IdentityDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly RefreshTokenRepository _refreshTokenRepository;
    private readonly TokenService _tokenService;
    private readonly Outbox _outbox;
    private readonly AuthService _authService;

    public BR03_AccountLockTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _userRepository = new UserRepository(_context);
        _refreshTokenRepository = new RefreshTokenRepository(_context);
        _tokenService = new TokenService(Options.Create(new JwtSettings()));
        _outbox = new Outbox(_context);

        _authService = new AuthService(
            _userRepository,
            _refreshTokenRepository,
            _tokenService,
            _outbox,
            new RegisterRequestValidator(),
            new LoginRequestValidator()
        );
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task Login_WhenUserIsLocked_ThrowsBusinessRuleExceptionWith403AndBR03Code()
    {
        // Arrange: Create a locked user
        var lockedUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "locked_student",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123@", 11),
            FullName = "Locked Student",
            Role = "Student",
            IsLocked = true,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(lockedUser);
        await _userRepository.SaveChangesAsync();

        var request = new LoginRequest("locked_student", "Password123@");

        // Act
        var act = async () => await _authService.LoginAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-03");
        exception.Which.StatusCode.Should().Be(403);
        exception.Which.Message.Should().Contain("khóa");
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task Refresh_WhenUserIsLocked_ThrowsBusinessRuleExceptionWith403AndBR03Code()
    {
        // Arrange: Create locked user with active refresh token
        var lockedUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "locked_user_refresh",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123@", 11),
            FullName = "Locked User",
            Role = "Student",
            IsLocked = true,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(lockedUser);

        var rawToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = lockedUser.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            User = lockedUser
        };
        await _refreshTokenRepository.AddAsync(token);
        await _refreshTokenRepository.SaveChangesAsync();

        var request = new RefreshTokenRequest(rawToken);

        // Act
        var act = async () => await _authService.RefreshAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-03");
        exception.Which.StatusCode.Should().Be(403);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task Login_WhenUserIsNotLocked_SucceedsAndReturnsTokens()
    {
        // Arrange: Unlocked user
        var activeUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "active_student",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123@", 11),
            FullName = "Active Student",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(activeUser);
        await _userRepository.SaveChangesAsync();

        var request = new LoginRequest("active_student", "Password123@");

        // Act
        var response = await _authService.LoginAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.RefreshToken.Should().NotBeNullOrWhiteSpace();
        response.User.IsLocked.Should().BeFalse();
    }
}
