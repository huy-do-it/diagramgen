using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;
using Identity.Repositories.Repositories;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Identity.Service.Validators;
using Identity.Tests.Helpers;

namespace Identity.Tests.Security;

public class SecurityAndTokenTests
{
    private readonly IdentityDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly RefreshTokenRepository _refreshTokenRepository;
    private readonly TokenService _tokenService;
    private readonly Outbox _outbox;
    private readonly AuthService _authService;

    public SecurityAndTokenTests()
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
    [Trait("Security", "BCrypt")]
    public async Task Register_HashesPasswordUsingBCryptWorkFactor11()
    {
        // Arrange
        var password = "SecurePassword123@";
        var request = new RegisterRequest("bcrypt_user", password, "BCrypt User");

        // Act
        var response = await _authService.RegisterAsync(request);

        // Assert
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == response.Id);
        user.Should().NotBeNull();
        user!.PasswordHash.Should().NotBeNullOrWhiteSpace();

        // BCrypt work factor 11 format: $2a$11$... or $2b$11$...
        user.PasswordHash.Should().MatchRegex(@"^\$2[ab]\$11\$");

        // Verify that BCrypt.Verify correctly validates the password
        BCrypt.Net.BCrypt.Verify(password, user.PasswordHash).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("WrongPassword", user.PasswordHash).Should().BeFalse();
    }

    [Fact]
    [Trait("Security", "SHA256")]
    public async Task Login_StoresOnlySha256HashInDatabaseAndNotRawToken()
    {
        // Arrange: Seed user
        await _authService.RegisterAsync(new RegisterRequest("sha256_user", "Password123@", "SHA256 User"));

        // Act
        var loginResponse = await _authService.LoginAsync(new LoginRequest("sha256_user", "Password123@"));

        // Assert
        var rawToken = loginResponse.RefreshToken;
        rawToken.Should().NotBeNullOrWhiteSpace();

        // The database must NOT contain the raw token
        var tokensInDb = await _context.RefreshTokens.ToListAsync();
        tokensInDb.Should().NotBeEmpty();

        var storedToken = tokensInDb.First(t => t.UserId == loginResponse.User.Id);
        storedToken.TokenHash.Should().NotBe(rawToken);

        // Stored hash must be 64 hexadecimal characters (SHA-256 output)
        storedToken.TokenHash.Length.Should().Be(64);
        storedToken.TokenHash.Should().MatchRegex("^[a-f0-9]{64}$");

        // Stored hash must match the SHA-256 hash of the raw token
        var expectedHash = _tokenService.HashToken(rawToken);
        storedToken.TokenHash.Should().Be(expectedHash);
    }

    [Fact]
    [Trait("Security", "JWT")]
    public void GenerateAccessToken_ProducesValidClaimsAndExpiration()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "jwt_student",
            PasswordHash = "hash",
            FullName = "JWT Student",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var tokenString = _tokenService.GenerateAccessToken(user);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        jwt.Issuer.Should().Be("umlforge");
        jwt.Audiences.Should().Contain("umlforge-client");

        var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        subClaim.Should().Be(user.Id.ToString());

        var nameClaim = jwt.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
        nameClaim.Should().Be(user.Username);

        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
        roleClaim.Should().Be(user.Role);

        // Expiration should be within ~15 minutes
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow.AddMinutes(13));
        jwt.ValidTo.Should().BeBefore(DateTime.UtcNow.AddMinutes(16));
    }
}
