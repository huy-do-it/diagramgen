using UMLForge.BuildingBlocks.Web;
using UMLForge.Diagram.Service.DTOs;

namespace UMLForge.Diagram.Service.Interfaces;

public interface IDiagramService
{
    Task<PagedResult<DiagramSummaryResponse>> GetDiagramsAsync(DiagramQueryParameters query, CancellationToken cancellationToken = default);
    Task<(DiagramDetailResponse Detail, string ETag)> GetDiagramByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DiagramDetailResponse> CreateDiagramAsync(CreateDiagramRequest request, CancellationToken cancellationToken = default);
    Task<DiagramDetailResponse> UpdateDiagramAsync(Guid id, UpdateDiagramRequest request, string? ifMatch = null, CancellationToken cancellationToken = default);
    Task DeleteDiagramAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RenderResponse> RequestRenderAsync(Guid diagramId, CancellationToken cancellationToken = default);
    Task<PagedResult<DiagramVersionSummaryResponse>> GetVersionsAsync(Guid diagramId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<DiagramVersionDetailResponse> GetVersionByNumberAsync(Guid diagramId, int version, CancellationToken cancellationToken = default);
    Task<DiagramDetailResponse> RestoreVersionAsync(Guid diagramId, int version, CancellationToken cancellationToken = default);
    Task<UMLForge.Diagram.Service.Models.Validation.DiagramValidationResult> ValidateDiagramAsync(Guid diagramId, string? draftModelJson = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UMLForge.Diagram.Service.Models.Validation.RelationSuggestion>> SuggestRelationsAsync(Guid diagramId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UMLForge.Diagram.Service.Models.Validation.ProjectDiagramValidationResult>> ValidateProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
