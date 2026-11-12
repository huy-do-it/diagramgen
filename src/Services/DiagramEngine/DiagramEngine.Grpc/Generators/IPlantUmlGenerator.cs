using UMLForge.DiagramEngine.Grpc.Models;

namespace UMLForge.DiagramEngine.Grpc.Generators;

/// <summary>
/// Sinh mã PlantUML từ model đã parse (chép từ v1, logic giữ nguyên).
/// 5 loại: BusinessContext, UseCase, Sequence, Activity, State.
/// </summary>
public interface IPlantUmlGenerator
{
    string GeneratePlantUml(DiagramType type, BaseDiagramModel model);
}
