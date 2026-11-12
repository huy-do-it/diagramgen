using FluentAssertions;
using Microsoft.Extensions.Options;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;
using Identity.Repositories.Repositories;
using Identity.Service.DTOs;
using Identity.Service.Exceptions;
using Identity.Service.Interfaces;
using Identity.Service.Services;
using Identity.Service.Validators;
using Identity.Tests.Helpers;

namespace Identity.Tests.BusinessRules;

[Trait("Trait", "BR")]
public class BR01_DuplicateUsernameTests
{
    private readonly IdentityDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly RefreshTokenRepository _refreshTokenRepository;
    private readonly TokenService _tokenService;
    private readonly Outbox _outbox;
    private readonly AuthService _authService;

    public BR01_DuplicateUsernameTests()
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
    [Trait("BR", "BR-01")]
    public async Task Register_WhenUsernameAlreadyExists_ThrowsBusinessRuleExceptionWith409AndBR01Code()
    {
        // Arrange: Seed an existing user
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "student1",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123@", 11),
            FullName = "Student One",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(existingUser);
        await _userRepository.SaveChangesAsync();

        var request = new RegisterRequest("student1", "Password123@", "New Student");

        // Act
        var act = async () => await _authService.RegisterAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-01");
        exception.Which.StatusCode.Should().Be(409);
        exception.Which.Message.Should().Contain("đã tồn tại");
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public async Task Register_WhenUsernameExistsWithDifferentCasing_ThrowsBusinessRuleExceptionWith409AndBR01Code()
    {
        // Arrange
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "johndoe",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123@", 11),
            FullName = "John Doe",
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(existingUser);
        await _userRepository.SaveChangesAsync();

        var request = new RegisterRequest("JohnDoe", "Password123@", "John Doe Clone");

        // Act
        var act = async () => await _authService.RegisterAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("BR-01");
        exception.Which.StatusCode.Should().Be(409);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public async Task Register_WhenUsernameIsUnique_SucceedsAndCreatesUser()
    {
        // Arrange
        var request = new RegisterRequest("unique_user_2026", "Password123@", "Unique User");

        // Act
        var response = await _authService.RegisterAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Username.Should().Be("unique_user_2026");
        response.Role.Should().Be("Student");
        response.IsLocked.Should().BeFalse();

        var created = await _userRepository.GetByUsernameAsync("unique_user_2026");
        created.Should().NotBeNull();
        created!.FullName.Should().Be("Unique User");
    }
}
