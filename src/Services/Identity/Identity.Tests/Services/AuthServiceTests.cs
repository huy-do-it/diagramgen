using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;
using Identity.Repositories.Repositories;
using Identity.Service.DTOs;
using Identity.Service.Exceptions;
using Identity.Service.Services;
using Identity.Service.Validators;
using Identity.Tests.Helpers;

namespace Identity.Tests.Services;

public class AuthServiceTests
{
    private readonly IdentityDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly RefreshTokenRepository _refreshTokenRepository;
    private readonly TokenService _tokenService;
    private readonly Outbox _outbox;
    private readonly AuthService _authService;

    public AuthServiceTests()
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
    public async Task RegisterAsync_QueuesUserRegisteredEventInOutbox()
    {
        // Arrange
        var request = new RegisterRequest("outbox_user", "Password123@", "Outbox User");

        // Act
        var user = await _authService.RegisterAsync(request);

        // Assert
        user.Should().NotBeNull();

        var outboxMessages = await _context.OutboxMessages.ToListAsync();
        outboxMessages.Should().ContainSingle();

        var message = outboxMessages.First();
        message.Stream.Should().Be("identity.events");
        message.Type.Should().Be("UserRegistered");
        message.Envelope.Should().Contain("outbox_user");
        message.Envelope.Should().Contain(user.Id.ToString());
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_Throws401InvalidCredentials()
    {
        // Arrange
        await _authService.RegisterAsync(new RegisterRequest("wrongpass_user", "Password123@", "Test User"));

        var request = new LoginRequest("wrongpass_user", "IncorrectPassword123@");

        // Act
        var act = async () => await _authService.LoginAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("INVALID_CREDENTIALS");
        exception.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_Throws401InvalidCredentials()
    {
        // Arrange
        var request = new LoginRequest("does_not_exist", "Password123@");

        // Act
        var act = async () => await _authService.LoginAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("INVALID_CREDENTIALS");
        exception.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task LogoutAsync_RevokesActiveRefreshToken()
    {
        // Arrange
        await _authService.RegisterAsync(new RegisterRequest("logout_user", "Password123@", "Logout User"));
        var loginResponse = await _authService.LoginAsync(new LoginRequest("logout_user", "Password123@"));

        var rawToken = loginResponse.RefreshToken;
        var tokenHash = _tokenService.HashToken(rawToken);

        // Verify token is active before logout
        var tokenBefore = await _context.RefreshTokens.FirstAsync(r => r.TokenHash == tokenHash);
        tokenBefore.RevokedAt.Should().BeNull();

        // Act
        await _authService.LogoutAsync(new RefreshTokenRequest(rawToken));

        // Assert
        var tokenAfter = await _context.RefreshTokens.FirstAsync(r => r.TokenHash == tokenHash);
        tokenAfter.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenUserExists_ReturnsProfile()
    {
        // Arrange
        var registered = await _authService.RegisterAsync(new RegisterRequest("profile_user", "Password123@", "Profile User"));

        // Act
        var profile = await _authService.GetCurrentUserAsync(registered.Id);

        // Assert
        profile.Should().NotBeNull();
        profile.Id.Should().Be(registered.Id);
        profile.Username.Should().Be("profile_user");
        profile.FullName.Should().Be("Profile User");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenUserDoesNotExist_Throws404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = async () => await _authService.GetCurrentUserAsync(nonExistentId);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.StatusCode.Should().Be(404);
    }
}
