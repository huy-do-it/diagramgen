using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Identity.Repositories.Data;
using Identity.Repositories.Repositories;
using Identity.Service.DTOs;
using Identity.Service.Exceptions;
using Identity.Service.Services;
using Identity.Service.Validators;
using Identity.Tests.Helpers;

namespace Identity.Tests.BusinessRules;

[Trait("Trait", "BR")]
public class BR02_PasswordValidationTests
{
    private readonly IdentityDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly RefreshTokenRepository _refreshTokenRepository;
    private readonly TokenService _tokenService;
    private readonly Outbox _outbox;
    private readonly AuthService _authService;

    public BR02_PasswordValidationTests()
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

    [Theory]
    [InlineData("1234567")] // 7 characters
    [InlineData("abc")]     // 3 characters
    [InlineData("P@ss1")]   // 5 characters
    [Trait("BR", "BR-02")]
    public async Task Register_WhenPasswordIsLessThan8Characters_ThrowsBusinessRuleExceptionWith400AndBR02Code(string shortPassword)
    {
        // Arrange
        var request = new RegisterRequest("validuser", shortPassword, "Valid User");

        // Act
        var act = async () => await _authService.RegisterAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-02");
        exception.Which.StatusCode.Should().Be(400);
        exception.Which.Message.Should().Contain("tối thiểu 8 ký tự");
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task Register_WhenPasswordIsEmpty_ThrowsBusinessRuleExceptionWith400AndBR02Code()
    {
        // Arrange
        var request = new RegisterRequest("validuser2", "", "Valid User");

        // Act
        var act = async () => await _authService.RegisterAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-02");
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task Register_WhenPasswordExceeds72Utf8Bytes_ThrowsBusinessRuleExceptionWith400AndBR02Code()
    {
        // Arrange: Password with > 72 bytes
        var longPassword = new string('A', 73);
        Encoding.UTF8.GetByteCount(longPassword).Should().Be(73);

        var request = new RegisterRequest("validuser3", longPassword, "Valid User");

        // Act
        var act = async () => await _authService.RegisterAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-02");
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task Register_WhenPasswordIsExactly8Characters_Succeeds()
    {
        // Arrange
        var request = new RegisterRequest("user8char", "12345678", "Eight Char User");

        // Act
        var response = await _authService.RegisterAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Username.Should().Be("user8char");
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task Register_WhenPasswordIsStrong_Succeeds()
    {
        // Arrange
        var request = new RegisterRequest("stronguser", "P@ssw0rd!#2026", "Strong Password User");

        // Act
        var response = await _authService.RegisterAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Username.Should().Be("stronguser");
    }
}
