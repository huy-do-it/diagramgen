using System.ComponentModel.DataAnnotations;
using UMLForge.Diagram.Repositories.Enums;

namespace UMLForge.Diagram.Service.DTOs;

public class DiagramQueryParameters
{
    public Guid? ProjectId { get; set; }
    public DiagramType? Type { get; set; }
    public DiagramStatus? Status { get; set; }
    public string? Search { get; set; }
    public string? Sort { get; set; } = "updatedAt:desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class CreateDiagramRequest
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public DiagramType Type { get; set; }

    [Required]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string ModelJson { get; set; } = string.Empty;
}

public class UpdateDiagramRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string ModelJson { get; set; } = string.Empty;
}

public class ValidateDiagramRequest
{
    public string? ModelJson { get; set; }
}

public class DiagramSummaryResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public DiagramType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public DiagramStatus Status { get; set; }
    public int CurrentVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? RenderedAt { get; set; }
    public string? LastError { get; set; }
}

public class DiagramDetailResponse : DiagramSummaryResponse
{
    public string ModelJson { get; set; } = string.Empty;
    public string? PlantUml { get; set; }
    public string? SvgXml { get; set; }
    public string? ETag { get; set; }
    public bool NeedsResubmit { get; set; } = false;
    public List<string> Warnings { get; set; } = new();
}

public class DiagramVersionSummaryResponse
{
    public Guid Id { get; set; }
    public Guid DiagramId { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DiagramVersionDetailResponse : DiagramVersionSummaryResponse
{
    public string ModelJson { get; set; } = string.Empty;
}

public class RenderResponse
{
    public Guid DiagramId { get; set; }
    public int Version { get; set; }
    public DiagramStatus Status { get; set; }
    public string Message { get; set; } = "Render request accepted.";
}
