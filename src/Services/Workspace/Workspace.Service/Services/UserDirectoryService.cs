using FluentValidation;
using Microsoft.AspNetCore.Http;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;
using Workspace.Repositories.Entities;
using Workspace.Repositories.Repositories;
using Workspace.Service.DTOs;
using Workspace.Service.Interfaces;

namespace Workspace.Service.Services;

public class UserDirectoryService : IUserDirectoryService
{
    private readonly IUserDirectoryRepository _userDirectoryRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IOutbox _outbox;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<AssignLecturerRequest> _assignValidator;

    public UserDirectoryService(
        IUserDirectoryRepository userDirectoryRepo,
        IProjectRepository projectRepo,
        IOutbox outbox,
        ICurrentUser currentUser,
        IValidator<AssignLecturerRequest> assignValidator)
    {
        _userDirectoryRepo = userDirectoryRepo;
        _projectRepo = projectRepo;
        _outbox = outbox;
        _currentUser = currentUser;
        _assignValidator = assignValidator;
    }

    public async Task<PagedResult<UserDirectoryResponse>> GetUsersAsync(PageQuery query, string? role, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var (items, totalCount) = await _userDirectoryRepo.GetPagedUsersAsync(
            query.Search, role, query.Page, query.PageSize, ct);

        var dtos = items.Select(u => new UserDirectoryResponse(
            u.UserId,
            u.Username,
            u.FullName,
            u.Role,
            u.IsLocked
        )).ToList();

        return PagedResult<UserDirectoryResponse>.Create(dtos, query.Page, query.PageSize, totalCount);
    }

    public async Task<ProjectDetailResponse> AssignLecturerAsync(Guid projectId, AssignLecturerRequest request, CancellationToken ct = default)
    {
        await _assignValidator.ValidateAndThrowAsync(request, ct);

        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var project = await _projectRepo.GetByIdWithMembersAsync(projectId, ct);
        if (project == null)
            throw new NotFoundException("Không tìm thấy dự án.");

        // BR-08: Only Leader can assign lecturer
        var isLeader = project.OwnerId == _currentUser.UserId ||
                       project.Members.Any(m => m.UserId == _currentUser.UserId && m.Role == "Leader");
        if (!isLeader)
            throw new ForbiddenException("BR-08", "Chỉ Team Leader mới có quyền gán giảng viên hướng dẫn.");

        var lecturer = await _userDirectoryRepo.GetByIdAsync(request.LecturerId, ct);
        if (lecturer == null)
            throw new NotFoundException("Không tìm thấy giảng viên trong danh bạ.");

        // BR-03: Locked account check
        if (lecturer.IsLocked)
            throw new BusinessRuleException("BR-03", "Tài khoản giảng viên đã bị khóa.", StatusCodes.Status400BadRequest);

        // BR-10: User must possess Lecturer role
        if (!string.Equals(lecturer.Role, UserRoles.Lecturer, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("BR-10", "Chỉ người dùng có vai trò Lecturer mới được gán làm giảng viên hướng dẫn.", StatusCodes.Status400BadRequest);

        var previousLecturerId = project.LecturerId;
        project.LecturerId = lecturer.UserId;

        if (previousLecturerId.HasValue && previousLecturerId.Value != lecturer.UserId)
        {
            var oldMember = project.Members.FirstOrDefault(m => m.UserId == previousLecturerId.Value && m.Role == "Lecturer");
            if (oldMember != null)
            {
                _projectRepo.RemoveMember(oldMember);
            }
        }

        if (!project.Members.Any(m => m.UserId == lecturer.UserId))
        {
            var lecturerMember = new ProjectMember
            {
                ProjectId = projectId,
                UserId = lecturer.UserId,
                Role = "Lecturer",
                JoinedAt = DateTime.UtcNow
            };
            await _projectRepo.AddMemberAsync(lecturerMember, ct);
        }

        _projectRepo.Update(project);

        // Stage LecturerAssigned event in Outbox
        _outbox.Add(StreamNames.Workspace, new LecturerAssigned(projectId, project.Name, lecturer.UserId, previousLecturerId, _currentUser.UserId));

        await _projectRepo.SaveChangesAsync(ct);

        var memberDtos = project.Members
            .Select(m => new ProjectMemberResponse(m.UserId, m.Role, m.JoinedAt))
            .ToList();

        return new ProjectDetailResponse(
            project.Id,
            project.Name,
            project.Description,
            project.OwnerId,
            project.LecturerId,
            project.CreatedAt,
            project.RowVersion,
            memberDtos
        );
    }
}
