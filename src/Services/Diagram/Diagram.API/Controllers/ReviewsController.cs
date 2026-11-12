using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.Diagram.Service.Interfaces;

namespace UMLForge.Diagram.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// Submits a project for review. Only project leader can perform this action.
    /// All diagrams must be in Rendered status (BR-29).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReviewDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitReview(
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reviewService.SubmitReviewAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Retrieves a paged list of reviews for a project with optional status filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReviewSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReviews(
        [FromQuery] Guid projectId,
        [FromQuery] ReviewStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _reviewService.GetReviewsAsync(projectId, status, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves detailed information of a review, including diagram snapshots.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReviewDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetReviewByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Approves a review. Only assigned lecturer can perform this action (BR-30).
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ReviewDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _reviewService.ApproveReviewAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Requests changes on a review with review comments. Only assigned lecturer can perform this action (BR-30).
    /// </summary>
    [HttpPost("{id:guid}/request-changes")]
    [ProducesResponseType(typeof(ReviewDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestChanges(
        Guid id,
        [FromBody] ReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _reviewService.RequestChangesAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
