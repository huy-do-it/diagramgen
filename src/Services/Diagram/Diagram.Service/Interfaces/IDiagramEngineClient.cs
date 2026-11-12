using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Service.Models.Validation;

namespace UMLForge.Diagram.Service.Interfaces;

public interface IDiagramEngineClient
{
    Task<DiagramValidationResult> ValidateDiagramAsync(DiagramType diagramType, string modelJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RelationSuggestion>> SuggestRelationsAsync(DiagramType diagramType, string modelJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectDiagramValidationResult>> ValidateProjectAsync(IEnumerable<(Guid DiagramId, DiagramType Type, string ModelJson)> diagrams, CancellationToken cancellationToken = default);
}
