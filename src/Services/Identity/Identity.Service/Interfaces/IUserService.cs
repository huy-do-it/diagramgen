using Identity.Service.DTOs;

namespace Identity.Service.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserResponse>> GetPagedUsersAsync(
        string? search,
        string? role,
        bool? isLocked,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task<UserResponse> LockUserAsync(Guid adminId, Guid targetUserId, bool isLocked, CancellationToken ct = default);
    Task<UserResponse> ChangeRoleAsync(Guid adminId, Guid targetUserId, string newRole, CancellationToken ct = default);
}
