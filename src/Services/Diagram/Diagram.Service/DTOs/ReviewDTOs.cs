using System.ComponentModel.DataAnnotations;
using UMLForge.Diagram.Repositories.Enums;

namespace UMLForge.Diagram.Service.DTOs;

public class CreateReviewRequest
{
    [Required]
    public Guid ProjectId { get; set; }
}

public class ReviewDecisionRequest
{
    [MaxLength(2000)]
    public string? Note { get; set; }
}

public class DiagramSnapshotItem
{
    public Guid DiagramId { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class ReviewSummaryResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public ReviewStatus Status { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Note { get; set; }
    public int DiagramCount { get; set; }
}

public class ReviewDetailResponse : ReviewSummaryResponse
{
    public string SnapshotJson { get; set; } = "[]";
    public List<DiagramSnapshotItem> Diagrams { get; set; } = new();
}

public class UnrenderedDiagramItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DiagramStatus Status { get; set; }
}
