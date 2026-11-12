using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Service.Interfaces;

namespace UMLForge.Diagram.Service.Services;

public class MemberAccessService : IMemberAccessService
{
    private readonly IMemberAccessRepository _memberAccessRepository;

    public MemberAccessService(IMemberAccessRepository memberAccessRepository)
    {
        _memberAccessRepository = memberAccessRepository;
    }

    public async Task GrantAccessAsync(Guid projectId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var existing = await _memberAccessRepository.GetAccessAsync(projectId, userId, cancellationToken);
        if (existing != null)
        {
            existing.Role = role;
            _memberAccessRepository.Update(existing);
        }
        else
        {
            await _memberAccessRepository.AddAsync(new MemberAccess
            {
                ProjectId = projectId,
                UserId = userId,
                Role = role
            }, cancellationToken);
        }

        await _memberAccessRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<MemberAccess?> GetAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _memberAccessRepository.GetAccessAsync(projectId, userId, cancellationToken);
    }

    public async Task<bool> HasAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _memberAccessRepository.HasAccessAsync(projectId, userId, cancellationToken);
    }
}
