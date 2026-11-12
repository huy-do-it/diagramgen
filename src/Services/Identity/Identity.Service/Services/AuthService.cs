using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;
using Identity.Repositories.Repositories;
using Identity.Service.DTOs;
using Identity.Service.Exceptions;
using Identity.Service.Interfaces;
using UMLForge.Contracts.Events;

namespace Identity.Service.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IOutbox _outbox;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IOutbox outbox,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _outbox = outbox;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // 1. FluentValidation check
        var validationResult = await _registerValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            if (errors.ContainsKey(nameof(RegisterRequest.Password)))
            {
                throw new BusinessRuleException(
                    "BR-02",
                    "Mật khẩu phải có độ dài tối thiểu 8 ký tự và không vượt quá 72 bytes.",
                    statusCode: 400,
                    errors: errors
                );
            }

            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Dữ liệu đăng ký không hợp lệ.",
                statusCode: 400,
                errors: errors
            );
        }

        // 2. BR-01: Check duplicate username
        var usernameExists = await _userRepository.UsernameExistsAsync(request.Username, ct);
        if (usernameExists)
        {
            throw new BusinessRuleException("BR-01", "Tên đăng nhập đã tồn tại trong hệ thống.", statusCode: 409);
        }

        // 3. BCrypt hash with explicit work factor 11
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            PasswordHash = passwordHash,
            FullName = request.FullName.Trim(),
            Role = "Student",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _userRepository.AddAsync(user, ct);

            // Queue Outbox message
            _outbox.Add(StreamNames.Identity, new UserRegistered(user.Id, user.Username, user.FullName, user.Role));

            await _userRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrency race condition for unique username index
            throw new BusinessRuleException("BR-01", "Tên đăng nhập đã tồn tại trong hệ thống.", statusCode: 409);
        }

        return MapToUserResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var validationResult = await _loginValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new BusinessRuleException("VALIDATION_ERROR", "Thông tin đăng nhập không hợp lệ.", statusCode: 400, errors: validationResult.ToDictionary());
        }

        var user = await _userRepository.GetByUsernameAsync(request.Username, ct);
        if (user == null)
        {
            throw new BusinessRuleException("INVALID_CREDENTIALS", "Tên đăng nhập hoặc mật khẩu không chính xác.", statusCode: 401);
        }

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            throw new BusinessRuleException("INVALID_CREDENTIALS", "Tên đăng nhập hoặc mật khẩu không chính xác.", statusCode: 401);
        }

        // BR-03: Locked account check
        if (user.IsLocked)
        {
            throw new BusinessRuleException("BR-03", "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.", statusCode: 403);
        }

        // Generate JWT Access Token
        var accessToken = _tokenService.GenerateAccessToken(user);

        // Generate Refresh Token & hash with SHA-256
        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashToken(rawRefreshToken);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.AddAsync(refreshToken, ct);
        await _refreshTokenRepository.SaveChangesAsync(ct);

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            ExpiresIn: 900, // 15 minutes in seconds
            User: MapToUserResponse(user)
        );
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new BusinessRuleException("BR-05", "Refresh token không hợp lệ hoặc đã hết hạn.", statusCode: 401);
        }

        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var token = await _refreshTokenRepository.GetByHashAsync(tokenHash, ct);

        if (token == null)
        {
            throw new BusinessRuleException("BR-05", "Refresh token không hợp lệ hoặc đã hết hạn.", statusCode: 401);
        }

        // BR-05: Token reuse detection
        if (token.RevokedAt != null)
        {
            // Reuse detected! Compromised token! Revoke all tokens in family
            await _refreshTokenRepository.RevokeAllByUserIdAsync(token.UserId, ct);
            await _refreshTokenRepository.SaveChangesAsync(ct);

            throw new BusinessRuleException(
                "BR-05",
                "Phát hiện sử dụng lại refresh token đã bị thu hồi. Toàn bộ phiên đăng nhập đã bị hủy.",
                statusCode: 401
            );
        }

        // Expired check
        if (token.ExpiresAt < DateTime.UtcNow)
        {
            throw new BusinessRuleException("BR-05", "Refresh token không hợp lệ hoặc đã hết hạn.", statusCode: 401);
        }

        var user = token.User ?? await _userRepository.GetByIdAsync(token.UserId, ct);
        if (user == null)
        {
            throw new BusinessRuleException("BR-05", "Người dùng không tồn tại.", statusCode: 401);
        }

        // BR-03: Check if account got locked
        if (user.IsLocked)
        {
            throw new BusinessRuleException("BR-03", "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.", statusCode: 403);
        }

        // Revoke current token (Single use rotation)
        token.RevokedAt = DateTime.UtcNow;

        // Issue new token pair
        var newRawRefreshToken = _tokenService.GenerateRefreshToken();
        var newTokenHash = _tokenService.HashToken(newRawRefreshToken);

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        token.ReplacedById = newRefreshToken.Id;

        await _refreshTokenRepository.AddAsync(newRefreshToken, ct);
        await _refreshTokenRepository.SaveChangesAsync(ct);

        var newAccessToken = _tokenService.GenerateAccessToken(user);

        return new AuthResponse(
            AccessToken: newAccessToken,
            RefreshToken: newRawRefreshToken,
            ExpiresIn: 900,
            User: MapToUserResponse(user)
        );
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var token = await _refreshTokenRepository.GetByHashAsync(tokenHash, ct);

        if (token != null && token.RevokedAt == null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _refreshTokenRepository.SaveChangesAsync(ct);
        }
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            throw new BusinessRuleException("NOT_FOUND", "Người dùng không tồn tại trong hệ thống.", statusCode: 404);
        }

        return MapToUserResponse(user);
    }

    private static UserResponse MapToUserResponse(User user)
    {
        return new UserResponse(
            Id: user.Id,
            Username: user.Username,
            FullName: user.FullName,
            Role: user.Role,
            IsLocked: user.IsLocked,
            CreatedAt: user.CreatedAt
        );
    }
}
