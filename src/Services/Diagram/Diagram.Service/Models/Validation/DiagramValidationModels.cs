namespace UMLForge.Diagram.Service.Models.Validation;

public class DiagramValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class RelationSuggestion
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ProjectDiagramValidationResult
{
    public Guid DiagramId { get; set; }
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
