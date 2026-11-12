using UMLForge.BuildingBlocks.EventBus;
using UMLForge.Contracts.Events;
using Workspace.Repositories.Entities;
using Workspace.Repositories.Repositories;

namespace Workspace.Service.EventHandlers;

public class UserRegisteredHandler : IEventHandler<UserRegistered>
{
    private readonly IUserDirectoryRepository _userDirectoryRepository;

    public UserRegisteredHandler(IUserDirectoryRepository userDirectoryRepository)
    {
        _userDirectoryRepository = userDirectoryRepository;
    }

    public async Task HandleAsync(EventEnvelope<UserRegistered> e, CancellationToken ct)
    {
        var payload = e.Payload;
        await _userDirectoryRepository.UpsertUserAsync(new UserDirectory
        {
            UserId = payload.UserId,
            Username = payload.Username,
            FullName = payload.FullName,
            Role = payload.Role,
            IsLocked = false
        }, ct);
    }
}
