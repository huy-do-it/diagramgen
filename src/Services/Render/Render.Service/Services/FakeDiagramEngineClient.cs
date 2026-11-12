using UMLForge.Render.Service.Interfaces;

namespace UMLForge.Render.Service.Services;

public sealed class FakeDiagramEngineClient : IDiagramEngineClient
{
    public Task<bool> ValidateAsync(int diagramType, string modelJson, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    public Task<string> GeneratePlantUmlAsync(int diagramType, string modelJson, string title, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("@startuml\nAlice -> Bob: hello\n@enduml");
    }
}
