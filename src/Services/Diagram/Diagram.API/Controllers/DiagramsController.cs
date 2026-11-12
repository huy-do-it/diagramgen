using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Models.Validation;

namespace UMLForge.Diagram.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DiagramsController : ControllerBase
{
    private readonly IDiagramService _diagramService;
    private readonly ICommentService _commentService;
    private readonly IDiagramExportService _exportService;

    public DiagramsController(
        IDiagramService diagramService,
        ICommentService commentService,
        IDiagramExportService exportService)
    {
        _diagramService = diagramService;
        _commentService = commentService;
        _exportService = exportService;
    }

    /// <summary>
    /// Retrieves a paged list of diagrams with optional filtering, sorting, and search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DiagramSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDiagrams([FromQuery] DiagramQueryParameters query, CancellationToken cancellationToken)
    {
        var result = await _diagramService.GetDiagramsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves detailed information of a specific diagram by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DiagramDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (detail, etag) = await _diagramService.GetDiagramByIdAsync(id, cancellationToken);
        Response.Headers.ETag = $"\"{etag}\"";
        return Ok(detail);
    }

    /// <summary>
    /// Creates a new diagram in Draft status with version 1.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DiagramDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateDiagramRequest request, CancellationToken cancellationToken)
    {
        var created = await _diagramService.CreateDiagramAsync(request, cancellationToken);
        var etag = created.ETag;
        if (!string.IsNullOrEmpty(etag))
        {
            Response.Headers.ETag = $"\"{etag}\"";
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates a diagram's title and model JSON (creates a new version, requires If-Match header).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DiagramDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDiagramRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        var updated = await _diagramService.UpdateDiagramAsync(id, request, ifMatch, cancellationToken);
        var etag = updated.ETag;
        if (!string.IsNullOrEmpty(etag))
        {
            Response.Headers.ETag = $"\"{etag}\"";
        }

        return Ok(updated);
    }

    /// <summary>
    /// Soft deletes a diagram.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _diagramService.DeleteDiagramAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Requests an asynchronous rendering for a diagram via Outbox pattern (BR-14).
    /// </summary>
    [HttpPost("{id:guid}/render")]
    [ProducesResponseType(typeof(RenderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestRender(Guid id, CancellationToken cancellationToken)
    {
        var result = await _diagramService.RequestRenderAsync(id, cancellationToken);
        return Accepted(result);
    }

    /// <summary>
    /// Retrieves a paged list of version snapshots for a diagram.
    /// </summary>
    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(PagedResult<DiagramVersionSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _diagramService.GetVersionsAsync(id, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific version snapshot of a diagram.
    /// </summary>
    [HttpGet("{id:guid}/versions/{version:int}")]
    [ProducesResponseType(typeof(DiagramVersionDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersionByNumber(
        Guid id,
        int version,
        CancellationToken cancellationToken = default)
    {
        var result = await _diagramService.GetVersionByNumberAsync(id, version, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Exports all diagrams of a project as a ZIP archive containing .json, .puml, and .svg files.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportProjectDiagrams(
        [FromQuery] Guid projectId,
        CancellationToken cancellationToken)
    {
        var (fileBytes, fileName) = await _exportService.ExportProjectDiagramsZipAsync(projectId, cancellationToken);
        return File(fileBytes, "application/zip", fileName);
    }

    /// <summary>
    /// Adds a comment to a diagram. All project members including lecturers can comment (BR-32).
    /// </summary>
    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        Guid id,
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _commentService.AddCommentAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetComments), new { id }, result);
    }

    /// <summary>
    /// Retrieves all comments for a diagram.
    /// </summary>
    [HttpGet("{id:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<CommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComments(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _commentService.GetCommentsAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Validates a diagram's model against semantic UML rules via DiagramEngine.
    /// </summary>
    [HttpPost("{id:guid}/validate")]
    [ProducesResponseType(typeof(DiagramValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _diagramService.ValidateDiagramAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns relation suggestions for a diagram via DiagramEngine.
    /// </summary>
    [HttpGet("{id:guid}/suggestions")]
    [ProducesResponseType(typeof(IReadOnlyList<RelationSuggestion>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSuggestions(Guid id, CancellationToken cancellationToken)
    {
        var result = await _diagramService.SuggestRelationsAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Validates all diagrams in a project and returns a consolidated report.
    /// </summary>
    [HttpGet("validate-project")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectDiagramValidationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ValidateProject(
        [FromQuery] Guid projectId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Bad Request", Detail = "ProjectId is required." });
        }

        var result = await _diagramService.ValidateProjectAsync(projectId, cancellationToken);
        return Ok(result);
    }
}
