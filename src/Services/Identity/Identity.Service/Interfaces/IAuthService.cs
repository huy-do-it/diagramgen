using Identity.Service.DTOs;

namespace Identity.Service.Interfaces;

public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
