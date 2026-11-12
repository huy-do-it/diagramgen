using System.ComponentModel.DataAnnotations;

namespace UMLForge.Diagram.Service.DTOs;

public class CreateCommentRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Comment content must be between 1 and 2000 characters.")]
    public string Content { get; set; } = string.Empty;
}

public class CommentResponse
{
    public Guid Id { get; set; }
    public Guid DiagramId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
