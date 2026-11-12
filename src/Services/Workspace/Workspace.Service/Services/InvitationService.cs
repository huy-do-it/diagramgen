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

public class InvitationService : IInvitationService
{
    private readonly IInvitationRepository _invitationRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IUserDirectoryRepository _userDirectoryRepo;
    private readonly IOutbox _outbox;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<InviteMemberRequest> _inviteValidator;

    public InvitationService(
        IInvitationRepository invitationRepo,
        IProjectRepository projectRepo,
        IUserDirectoryRepository userDirectoryRepo,
        IOutbox outbox,
        ICurrentUser currentUser,
        IValidator<InviteMemberRequest> inviteValidator)
    {
        _invitationRepo = invitationRepo;
        _projectRepo = projectRepo;
        _userDirectoryRepo = userDirectoryRepo;
        _outbox = outbox;
        _currentUser = currentUser;
        _inviteValidator = inviteValidator;
    }

    public async Task<InvitationResponse> InviteMemberAsync(Guid projectId, InviteMemberRequest request, CancellationToken ct = default)
    {
        await _inviteValidator.ValidateAndThrowAsync(request, ct);

        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var project = await _projectRepo.GetByIdWithMembersAsync(projectId, ct);
        if (project == null)
            throw new NotFoundException("Không tìm thấy dự án.");

        // BR-08: Only Leader can invite members
        var isLeader = project.OwnerId == _currentUser.UserId ||
                       project.Members.Any(m => m.UserId == _currentUser.UserId && m.Role == "Leader");
        if (!isLeader)
            throw new ForbiddenException("BR-08", "Chỉ Team Leader mới có quyền mời thành viên.");

        // Lookup invitee in UserDirectory
        var invitee = await _userDirectoryRepo.GetByUsernameAsync(request.Username, ct);
        if (invitee == null)
            throw new NotFoundException("Người dùng không tồn tại trong danh bạ.");

        // BR-03: Locked users cannot be invited
        if (invitee.IsLocked)
            throw new BusinessRuleException("BR-03", "Tài khoản người dùng đã bị khóa.", StatusCodes.Status400BadRequest);

        // Check if already a member
        if (project.Members.Any(m => m.UserId == invitee.UserId))
            throw new BusinessRuleException("ALREADY_MEMBER", "Người dùng đã là thành viên của dự án.", StatusCodes.Status409Conflict);

        // Check if pending invitation already exists
        var hasPending = await _invitationRepo.HasPendingInvitationAsync(projectId, invitee.UserId, ct);
        if (hasPending)
            throw new BusinessRuleException("ALREADY_INVITED", "Người dùng đã có lời mời đang chờ phản hồi.", StatusCodes.Status409Conflict);

        // BR-07: Max 5 members excluding Lecturer (active student members + pending invitations < 5)
        var activeStudentMembers = project.Members.Count(m => m.Role != "Lecturer");
        var pendingInvitations = await _invitationRepo.GetPendingCountForProjectAsync(projectId, ct);
        if (activeStudentMembers + pendingInvitations >= 5)
        {
            throw new BusinessRuleException("BR-07", "Dự án đã đạt tối đa 5 thành viên bao gồm lời mời đang chờ.", StatusCodes.Status409Conflict);
        }

        // BR-09: 7 days expiration
        var now = DateTime.UtcNow;
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            InviteeId = invitee.UserId,
            Status = "Pending",
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        };

        await _invitationRepo.AddAsync(invitation, ct);

        // Stage InvitationSent event in Outbox
        _outbox.Add(StreamNames.Workspace, new InvitationSent(invitation.Id, projectId, project.Name, invitee.UserId, _currentUser.UserId));

        await _invitationRepo.SaveChangesAsync(ct);

        return new InvitationResponse(
            invitation.Id,
            invitation.ProjectId,
            invitation.InviteeId,
            invitation.Status,
            invitation.CreatedAt,
            invitation.ExpiresAt
        );
    }

    public async Task<IReadOnlyList<MyInvitationResponse>> GetMyInvitationsAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var invitations = await _invitationRepo.GetPendingForUserAsync(_currentUser.UserId, ct);

        return invitations.Select(i => new MyInvitationResponse(
            i.Id,
            i.ProjectId,
            i.Project?.Name ?? string.Empty,
            i.InviteeId,
            i.Status,
            i.CreatedAt,
            i.ExpiresAt
        )).ToList();
    }

    public async Task<InvitationResponse> AcceptInvitationAsync(Guid invitationId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var invitation = await _invitationRepo.GetByIdWithProjectAsync(invitationId, ct);
        if (invitation == null)
            throw new NotFoundException("Không tìm thấy lời mời.");

        if (invitation.InviteeId != _currentUser.UserId)
            throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền phản hồi lời mời này.");

        if (invitation.Status != "Pending")
            throw new BusinessRuleException("INVALID_STATUS", "Lời mời không còn ở trạng thái chờ phản hồi.", StatusCodes.Status400BadRequest);

        // BR-09: Expired invitations rejected with 410 Gone
        if (invitation.ExpiresAt <= DateTime.UtcNow)
        {
            invitation.Status = "Expired";
            _invitationRepo.Update(invitation);
            await _invitationRepo.SaveChangesAsync(ct);
            throw new BusinessRuleException("BR-09", "Lời mời đã hết hạn sau 7 ngày.", StatusCodes.Status410Gone);
        }

        var project = await _projectRepo.GetByIdWithMembersAsync(invitation.ProjectId, ct);
        if (project == null)
            throw new NotFoundException("Dự án không tồn tại.");

        // BR-07: Capacity check upon acceptance
        var activeStudentMembers = project.Members.Count(m => m.Role != "Lecturer");
        if (activeStudentMembers >= 5)
        {
            throw new BusinessRuleException("BR-07", "Dự án đã đạt tối đa 5 thành viên.", StatusCodes.Status409Conflict);
        }

        // Add member
        if (!project.Members.Any(m => m.UserId == _currentUser.UserId))
        {
            var newMember = new ProjectMember
            {
                ProjectId = invitation.ProjectId,
                UserId = _currentUser.UserId,
                Role = "Member",
                JoinedAt = DateTime.UtcNow
            };
            await _projectRepo.AddMemberAsync(newMember, ct);
        }

        invitation.Status = "Accepted";
        _invitationRepo.Update(invitation);

        // Stage MemberAdded event in Outbox
        _outbox.Add(StreamNames.Workspace, new MemberAdded(invitation.ProjectId, project.Name, _currentUser.UserId, "Member", _currentUser.UserId));

        await _invitationRepo.SaveChangesAsync(ct);

        return new InvitationResponse(
            invitation.Id,
            invitation.ProjectId,
            invitation.InviteeId,
            invitation.Status,
            invitation.CreatedAt,
            invitation.ExpiresAt
        );
    }

    public async Task<InvitationResponse> DeclineInvitationAsync(Guid invitationId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var invitation = await _invitationRepo.GetByIdAsync(invitationId, ct);
        if (invitation == null)
            throw new NotFoundException("Không tìm thấy lời mời.");

        if (invitation.InviteeId != _currentUser.UserId)
            throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền phản hồi lời mời này.");

        if (invitation.Status != "Pending")
            throw new BusinessRuleException("INVALID_STATUS", "Lời mời không còn ở trạng thái chờ phản hồi.", StatusCodes.Status400BadRequest);

        invitation.Status = "Declined";
        _invitationRepo.Update(invitation);
        await _invitationRepo.SaveChangesAsync(ct);

        return new InvitationResponse(
            invitation.Id,
            invitation.ProjectId,
            invitation.InviteeId,
            invitation.Status,
            invitation.CreatedAt,
            invitation.ExpiresAt
        );
    }
}
