namespace Identity.Service.DTOs;

public record RegisterRequest(
    string Username,
    string Password,
    string FullName
);

public record LoginRequest(
    string Username,
    string Password
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record UserResponse(
    Guid Id,
    string Username,
    string FullName,
    string Role,
    bool IsLocked,
    DateTime CreatedAt
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    UserResponse User
);

public record LockUserRequest(
    bool IsLocked
);

public record ChangeRoleRequest(
    string Role
);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);
