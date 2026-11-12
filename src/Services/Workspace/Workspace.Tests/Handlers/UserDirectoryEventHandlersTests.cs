using FluentAssertions;
using UMLForge.Contracts.Events;
using Workspace.Repositories.Data;
using Workspace.Repositories.Repositories;
using Workspace.Service.EventHandlers;
using Workspace.Tests.Helpers;

namespace Workspace.Tests.Handlers;

public class UserDirectoryEventHandlersTests
{
    private readonly WorkspaceDbContext _context;
    private readonly UserDirectoryRepository _userDirectoryRepo;

    public UserDirectoryEventHandlersTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _userDirectoryRepo = new UserDirectoryRepository(_context);
    }

    [Fact]
    public async Task UserRegisteredHandler_WhenNewUser_CreatesDirectoryRecord()
    {
        // Arrange
        var handler = new UserRegisteredHandler(_userDirectoryRepo);
        var userId = Guid.NewGuid();
        var payload = new UserRegistered(userId, "new_student", "New Student Name", "Student");
        var envelope = EventEnvelope.Create("identity", payload);

        // Act
        await handler.HandleAsync(envelope, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Assert
        var user = await _userDirectoryRepo.GetByIdAsync(userId);
        user.Should().NotBeNull();
        user!.Username.Should().Be("new_student");
        user.FullName.Should().Be("New Student Name");
        user.Role.Should().Be("Student");
        user.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task UserRegisteredHandler_WhenDuplicateEventReceived_UpsertsIdempotently()
    {
        // Arrange
        var handler = new UserRegisteredHandler(_userDirectoryRepo);
        var userId = Guid.NewGuid();

        var initialPayload = new UserRegistered(userId, "duplicate_user", "Original Name", "Student");
        var envelope1 = EventEnvelope.Create("identity", initialPayload);
        await handler.HandleAsync(envelope1, CancellationToken.None);
        await _context.SaveChangesAsync();

        var duplicatePayload = new UserRegistered(userId, "duplicate_user", "Updated Name", "Student");
        var envelope2 = EventEnvelope.Create("identity", duplicatePayload);

        // Act: simulate duplicate delivery
        var act = async () =>
        {
            await handler.HandleAsync(envelope2, CancellationToken.None);
            await _context.SaveChangesAsync();
        };

        // Assert: must succeed without throwing unique constraint or key conflict
        await act.Should().NotThrowAsync();

        var user = await _userDirectoryRepo.GetByIdAsync(userId);
        user!.FullName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UserRoleChangedHandler_UpdatesUserRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var initialUser = new UserRegistered(userId, "changing_role_user", "Role User", "Student");
        var regHandler = new UserRegisteredHandler(_userDirectoryRepo);
        await regHandler.HandleAsync(EventEnvelope.Create("identity", initialUser), CancellationToken.None);
        await _context.SaveChangesAsync();

        var roleHandler = new UserRoleChangedHandler(_userDirectoryRepo);
        var roleChanged = new UserRoleChanged(userId, "Lecturer");
        var envelope = EventEnvelope.Create("identity", roleChanged);

        // Act
        await roleHandler.HandleAsync(envelope, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Assert
        var user = await _userDirectoryRepo.GetByIdAsync(userId);
        user!.Role.Should().Be("Lecturer");
    }

    [Fact]
    public async Task UserLockedHandler_UpdatesUserLockState()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var initialUser = new UserRegistered(userId, "locking_user", "Lock User", "Student");
        var regHandler = new UserRegisteredHandler(_userDirectoryRepo);
        await regHandler.HandleAsync(EventEnvelope.Create("identity", initialUser), CancellationToken.None);
        await _context.SaveChangesAsync();

        var lockHandler = new UserLockedHandler(_userDirectoryRepo);
        var userLocked = new UserLocked(userId, true);
        var envelope = EventEnvelope.Create("identity", userLocked);

        // Act
        await lockHandler.HandleAsync(envelope, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Assert
        var user = await _userDirectoryRepo.GetByIdAsync(userId);
        user!.IsLocked.Should().BeTrue();
    }
}
