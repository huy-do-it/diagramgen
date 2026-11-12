using UMLForge.BuildingBlocks.EventBus;
using UMLForge.Contracts.Events;
using Workspace.Repositories.Repositories;

namespace Workspace.Service.EventHandlers;

public class UserRoleChangedHandler : IEventHandler<UserRoleChanged>
{
    private readonly IUserDirectoryRepository _userDirectoryRepository;

    public UserRoleChangedHandler(IUserDirectoryRepository userDirectoryRepository)
    {
        _userDirectoryRepository = userDirectoryRepository;
    }

    public async Task HandleAsync(EventEnvelope<UserRoleChanged> e, CancellationToken ct)
    {
        var existing = await _userDirectoryRepository.GetByIdAsync(e.Payload.UserId, ct);
        if (existing != null)
        {
            existing.Role = e.Payload.Role;
            _userDirectoryRepository.Update(existing);
        }
    }
}
