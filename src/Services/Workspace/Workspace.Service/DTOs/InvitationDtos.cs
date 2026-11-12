namespace Workspace.Service.DTOs;

public record InviteMemberRequest(string Username);

public record InvitationResponse(
    Guid Id,
    Guid ProjectId,
    Guid InviteeId,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt);

public record MyInvitationResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid InviteeId,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt);
