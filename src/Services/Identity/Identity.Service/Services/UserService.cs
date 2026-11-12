using FluentValidation;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;
using Identity.Repositories.Repositories;
using Identity.Service.DTOs;
using Identity.Service.Exceptions;
using Identity.Service.Interfaces;
using UMLForge.Contracts.Events;

namespace Identity.Service.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IOutbox _outbox;
    private readonly IValidator<ChangeRoleRequest> _changeRoleValidator;

    public UserService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IOutbox outbox,
        IValidator<ChangeRoleRequest> changeRoleValidator)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _outbox = outbox;
        _changeRoleValidator = changeRoleValidator;
    }

    public async Task<PagedResult<UserResponse>> GetPagedUsersAsync(
        string? search,
        string? role,
        bool? isLocked,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _userRepository.GetPagedAsync(search, role, isLocked, sort, page, pageSize, ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var mappedItems = items.Select(MapToUserResponse).ToList();

        return new PagedResult<UserResponse>(
            Items: mappedItems,
            Page: page,
            PageSize: pageSize,
            TotalItems: totalCount,
            TotalPages: totalPages
        );
    }

    public async Task<UserResponse> LockUserAsync(Guid adminId, Guid targetUserId, bool isLocked, CancellationToken ct = default)
    {
        // BR-04: Admin cannot lock their own account
        if (adminId == targetUserId && isLocked)
        {
            throw new BusinessRuleException("BR-04", "Admin không được phép tự khóa chính mình.", statusCode: 403);
        }

        var user = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (user == null)
        {
            throw new BusinessRuleException("NOT_FOUND", "Người dùng không tồn tại.", statusCode: 404);
        }

        user.IsLocked = isLocked;

        // If locked, revoke all active sessions/tokens
        if (isLocked)
        {
            await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id, ct);
        }

        // Outbox event
        _outbox.Add(StreamNames.Identity, new UserLocked(user.Id, isLocked));

        await _userRepository.SaveChangesAsync(ct);

        return MapToUserResponse(user);
    }

    public async Task<UserResponse> ChangeRoleAsync(Guid adminId, Guid targetUserId, string newRole, CancellationToken ct = default)
    {
        var validationResult = await _changeRoleValidator.ValidateAsync(new ChangeRoleRequest(newRole), ct);
        if (!validationResult.IsValid)
        {
            throw new BusinessRuleException("VALIDATION_ERROR", "Vai trò không hợp lệ.", statusCode: 400, errors: validationResult.ToDictionary());
        }

        var user = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (user == null)
        {
            throw new BusinessRuleException("NOT_FOUND", "Người dùng không tồn tại.", statusCode: 404);
        }

        user.Role = newRole;

        // Outbox event
        _outbox.Add(StreamNames.Identity, new UserRoleChanged(user.Id, newRole));

        await _userRepository.SaveChangesAsync(ct);

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
