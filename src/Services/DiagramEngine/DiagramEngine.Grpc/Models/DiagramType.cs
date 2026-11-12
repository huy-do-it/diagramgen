namespace UMLForge.DiagramEngine.Grpc.Models;

/// <summary>Loại sơ đồ — giữ số như v1 (TASKS §1.3), khớp <c>diagram_type</c> trong proto.</summary>
public enum DiagramType
{
    BusinessContext = 1,
    UseCase = 2,
    Sequence = 3,
    Activity = 4,
    State = 5
}
