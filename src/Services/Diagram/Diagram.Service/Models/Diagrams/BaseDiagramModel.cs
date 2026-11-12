namespace UMLForge.Diagram.Service.Models.Diagrams;

public abstract class BaseDiagramModel
{
    public abstract string Type { get; }
    public string Title { get; set; } = string.Empty;
}
