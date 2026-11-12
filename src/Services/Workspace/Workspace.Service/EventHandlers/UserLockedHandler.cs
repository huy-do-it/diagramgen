using UMLForge.BuildingBlocks.EventBus;
using UMLForge.Contracts.Events;
using Workspace.Repositories.Repositories;

namespace Workspace.Service.EventHandlers;

public class UserLockedHandler : IEventHandler<UserLocked>
{
    private readonly IUserDirectoryRepository _userDirectoryRepository;

    public UserLockedHandler(IUserDirectoryRepository userDirectoryRepository)
    {
        _userDirectoryRepository = userDirectoryRepository;
    }

    public async Task HandleAsync(EventEnvelope<UserLocked> e, CancellationToken ct)
    {
        var existing = await _userDirectoryRepository.GetByIdAsync(e.Payload.UserId, ct);
        if (existing != null)
        {
            existing.IsLocked = e.Payload.IsLocked;
            _userDirectoryRepository.Update(existing);
        }
    }
}
