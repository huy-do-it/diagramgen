namespace UMLForge.Contracts.Events;

// Stream: identity.events — producer: identity
public sealed record UserRegistered(Guid UserId, string Username, string FullName, string Role);

public sealed record UserRoleChanged(Guid UserId, string Role);

public sealed record UserLocked(Guid UserId, bool IsLocked);
