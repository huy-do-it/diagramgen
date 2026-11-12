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

namespace Identity.Tests.BusinessRules;

[Trait("Trait", "BR")]
public class BR05_RefreshTokenRotationTests
{
    private readonly IdentityDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly RefreshTokenRepository _refreshTokenRepository;
    private readonly TokenService _tokenService;
    private readonly Outbox _outbox;
    private readonly AuthService _authService;

    public BR05_RefreshTokenRotationTests()
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
    [Trait("BR", "BR-05")]
    public async Task Refresh_WhenTokenValid_RevokesOldTokenAndReturnsNewTokenPair()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "rotation_user",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123@", 11),
            FullName = "Rotation User",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(user);

        var rawToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var originalToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            User = user
        };
        await _refreshTokenRepository.AddAsync(originalToken);
        await _refreshTokenRepository.SaveChangesAsync();

        var request = new RefreshTokenRequest(rawToken);

        // Act
        var response = await _authService.RefreshAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.RefreshToken.Should().NotBeNullOrWhiteSpace();
        response.RefreshToken.Should().NotBe(rawToken); // New token issued

        // Verify old token is revoked
        var updatedOriginal = await _context.RefreshTokens.FindAsync(originalToken.Id);
        updatedOriginal.Should().NotBeNull();
        updatedOriginal!.RevokedAt.Should().NotBeNull();
        updatedOriginal.ReplacedById.Should().NotBeNull();

        // Verify new token exists in database and is active
        var newTokenHash = _tokenService.HashToken(response.RefreshToken);
        var storedNewToken = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == newTokenHash);
        storedNewToken.Should().NotBeNull();
        storedNewToken!.RevokedAt.Should().BeNull();
        storedNewToken.Id.Should().Be(updatedOriginal.ReplacedById!.Value);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public async Task Refresh_WhenReusedTokenSubmitted_RevokesAllUserTokensAndThrows401()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "theft_victim",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123@", 11),
            FullName = "Theft Victim",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(user);

        // Old token that was already revoked
        var stolenRawToken = _tokenService.GenerateRefreshToken();
        var stolenTokenHash = _tokenService.HashToken(stolenRawToken);

        var revokedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = stolenTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddMinutes(-5), // Already revoked
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            User = user
        };
        await _refreshTokenRepository.AddAsync(revokedToken);

        // Another active token belonging to the same user
        var activeRawToken = _tokenService.GenerateRefreshToken();
        var activeTokenHash = _tokenService.HashToken(activeRawToken);
        var activeToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = activeTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            User = user
        };
        await _refreshTokenRepository.AddAsync(activeToken);
        await _refreshTokenRepository.SaveChangesAsync();

        var request = new RefreshTokenRequest(stolenRawToken);

        // Act: Submitting revoked token simulates token theft reuse!
        var act = async () => await _authService.RefreshAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-05");
        exception.Which.StatusCode.Should().Be(401);
        exception.Which.Message.Should().Contain("sử dụng lại");

        // Verify that ALL active tokens for this user were revoked as compromise protection
        var refreshedActiveToken = await _context.RefreshTokens.FindAsync(activeToken.Id);
        refreshedActiveToken.Should().NotBeNull();
        refreshedActiveToken!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public async Task Refresh_WhenTokenNotFoundOrMalformed_Throws401()
    {
        // Arrange: Random token never stored in DB
        var request = new RefreshTokenRequest("non_existent_token_12345");

        // Act
        var act = async () => await _authService.RefreshAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-05");
        exception.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public async Task Refresh_WhenTokenExpired_Throws401()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "expired_token_user",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123@", 11),
            FullName = "Expired User",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(user);

        var rawToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Expired in the past
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            User = user
        };
        await _refreshTokenRepository.AddAsync(expiredToken);
        await _refreshTokenRepository.SaveChangesAsync();

        var request = new RefreshTokenRequest(rawToken);

        // Act
        var act = async () => await _authService.RefreshAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-05");
        exception.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public async Task Refresh_AfterSuccessfulRefresh_OldTokenCannotBeUsedAgain()
    {
        // Arrange: Register and login to get initial tokens
        var registerResponse = await _authService.RegisterAsync(new RegisterRequest("sequence_user", "Password123@", "Sequence User"));
        var loginResponse = await _authService.LoginAsync(new LoginRequest("sequence_user", "Password123@"));

        var initialRefreshToken = loginResponse.RefreshToken;

        // Act 1: First refresh succeeds
        var firstRefreshResponse = await _authService.RefreshAsync(new RefreshTokenRequest(initialRefreshToken));
        firstRefreshResponse.Should().NotBeNull();
        firstRefreshResponse.RefreshToken.Should().NotBe(initialRefreshToken);

        // Act 2: Second refresh with initial token MUST FAIL (Single-use rotation & reuse detection)
        var secondAct = async () => await _authService.RefreshAsync(new RefreshTokenRequest(initialRefreshToken));

        // Assert
        var exception = await secondAct.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-05");
        exception.Which.StatusCode.Should().Be(401);
    }
}
