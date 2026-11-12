namespace UMLForge.DiagramEngine.Grpc.Models;

public abstract class BaseDiagramModel
{
    public abstract string Type { get; }
    public string Title { get; set; } = string.Empty;
}
