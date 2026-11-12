using Workspace.Service.DTOs;

namespace Workspace.Service.Interfaces;

public interface IInvitationService
{
    Task<InvitationResponse> InviteMemberAsync(Guid projectId, InviteMemberRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MyInvitationResponse>> GetMyInvitationsAsync(CancellationToken ct = default);
    Task<InvitationResponse> AcceptInvitationAsync(Guid invitationId, CancellationToken ct = default);
    Task<InvitationResponse> DeclineInvitationAsync(Guid invitationId, CancellationToken ct = default);
}
