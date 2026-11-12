namespace UMLForge.Render.Service.Interfaces;

public interface IDiagramEngineClient
{
    Task<bool> ValidateAsync(int diagramType, string modelJson, CancellationToken cancellationToken = default);
    Task<string> GeneratePlantUmlAsync(int diagramType, string modelJson, string title, CancellationToken cancellationToken = default);
}
