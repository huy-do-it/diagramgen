namespace UMLForge.Contracts.Events;

/// <summary>Bảng tra tên sự kiện ↔ kiểu CLR ↔ stream cho 16 sự kiện ở TASKS §1.5.</summary>
public static class EventTypeRegistry
{
    private static readonly Dictionary<string, (Type Type, string Stream)> _events = new(StringComparer.Ordinal)
    {
        [nameof(UserRegistered)] = (typeof(UserRegistered), StreamNames.Identity),
        [nameof(UserRoleChanged)] = (typeof(UserRoleChanged), StreamNames.Identity),
        [nameof(UserLocked)] = (typeof(UserLocked), StreamNames.Identity),

        [nameof(ProjectCreated)] = (typeof(ProjectCreated), StreamNames.Workspace),
        [nameof(InvitationSent)] = (typeof(InvitationSent), StreamNames.Workspace),
        [nameof(MemberAdded)] = (typeof(MemberAdded), StreamNames.Workspace),
        [nameof(MemberRemoved)] = (typeof(MemberRemoved), StreamNames.Workspace),
        [nameof(LecturerAssigned)] = (typeof(LecturerAssigned), StreamNames.Workspace),
        [nameof(ProjectDeleted)] = (typeof(ProjectDeleted), StreamNames.Workspace),

        [nameof(RenderRequested)] = (typeof(RenderRequested), StreamNames.Diagram),
        [nameof(ReviewSubmitted)] = (typeof(ReviewSubmitted), StreamNames.Diagram),
        [nameof(ReviewDecided)] = (typeof(ReviewDecided), StreamNames.Diagram),
        [nameof(CommentAdded)] = (typeof(CommentAdded), StreamNames.Diagram),

        [nameof(RenderStarted)] = (typeof(RenderStarted), StreamNames.Render),
        [nameof(RenderCompleted)] = (typeof(RenderCompleted), StreamNames.Render),
        [nameof(RenderFailed)] = (typeof(RenderFailed), StreamNames.Render),
    };

    public static IReadOnlyCollection<string> Names => _events.Keys;

    public static string NameOf<T>() => NameOf(typeof(T));

    public static string NameOf(Type payloadType)
        => _events.TryGetValue(payloadType.Name, out var entry) && entry.Type == payloadType
            ? payloadType.Name
            : throw new ArgumentException($"'{payloadType.FullName}' không phải sự kiện trong Contracts.Events.", nameof(payloadType));

    public static bool TryResolve(string name, out Type payloadType)
    {
        var found = _events.TryGetValue(name, out var entry);
        payloadType = found ? entry.Type : typeof(object);
        return found;
    }

    public static string StreamOf(string name)
        => _events.TryGetValue(name, out var entry)
            ? entry.Stream
            : throw new ArgumentException($"Không có sự kiện tên '{name}'.", nameof(name));
}
