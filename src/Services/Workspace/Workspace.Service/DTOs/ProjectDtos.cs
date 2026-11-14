using Workspace.Repositories.Entities;

namespace Workspace.Service.DTOs;

public record CreateProjectRequest(string Name, string? Description);

public record UpdateProjectRequest(string Name, string? Description);

public record ProjectSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    Guid? LecturerId,
    int MemberCount,
    DateTime CreatedAt);

public record ProjectMemberResponse(
    Guid UserId,
    string Username,
    string FullName,
    string Role,
    DateTime JoinedAt)
{
    // Ghép thành viên với danh bạ; user chưa đồng bộ sang danh bạ thì để trống tên
    public static ProjectMemberResponse From(ProjectMember member, IReadOnlyDictionary<Guid, UserDirectory> directory)
        => directory.TryGetValue(member.UserId, out var user)
            ? new(member.UserId, user.Username, user.FullName, member.Role, member.JoinedAt)
            : new(member.UserId, string.Empty, string.Empty, member.Role, member.JoinedAt);
}

public record ProjectDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    Guid? LecturerId,
    DateTime CreatedAt,
    byte[] RowVersion,
    IReadOnlyList<ProjectMemberResponse> Members);
