using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Models.Validation;

namespace UMLForge.Diagram.Service.Services;

public sealed class FakeDiagramEngineClient : IDiagramEngineClient
{
    public Task<DiagramValidationResult> ValidateDiagramAsync(
        DiagramType diagramType,
        string modelJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DiagramValidationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>()
        });
    }

    public Task<IReadOnlyList<RelationSuggestion>> SuggestRelationsAsync(
        DiagramType diagramType,
        string modelJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<RelationSuggestion>>(Array.Empty<RelationSuggestion>());
    }

    public Task<IReadOnlyList<ProjectDiagramValidationResult>> ValidateProjectAsync(
        IEnumerable<(Guid DiagramId, DiagramType Type, string ModelJson)> diagrams,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = diagrams.Select(d => new ProjectDiagramValidationResult
        {
            DiagramId = d.DiagramId,
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>()
        }).ToList();

        return Task.FromResult<IReadOnlyList<ProjectDiagramValidationResult>>(results);
    }
}
