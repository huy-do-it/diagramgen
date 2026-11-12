namespace UMLForge.Contracts.Events;

public static class StreamNames
{
    public const string Identity = "identity.events";
    public const string Workspace = "workspace.events";
    public const string Diagram = "diagram.events";
    public const string Render = "render.events";

    public const string DeadLetterSuffix = ".dlq";

    public static string DeadLetter(string stream) => stream + DeadLetterSuffix;
}

/// <summary>Tên consumer group theo TASKS §1.4.</summary>
public static class ConsumerGroups
{
    public const string WorkspaceSvc = "workspace-svc";
    public const string DiagramSvc = "diagram-svc";
    public const string RenderSvc = "render-svc";
    public const string NotificationSvc = "notification-svc";
}

/// <summary>Giá trị <c>producer</c> trong envelope — truyền vào AddOutbox(producer).</summary>
public static class Producers
{
    public const string Identity = "identity";
    public const string Workspace = "workspace";
    public const string Diagram = "diagram";
    public const string Render = "render";
}
