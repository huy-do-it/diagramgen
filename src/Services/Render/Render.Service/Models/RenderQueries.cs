using FluentValidation;
namespace UMLForge.Render.Service.Models;
public sealed class RenderQuery
{
    public string? Status { get; set; }
    public Guid? DiagramId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public sealed class RenderQueryValidator : AbstractValidator<RenderQuery>
{
    public RenderQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1,100);
        RuleFor(x => x).Must(x => ((long)x.Page-1)*x.PageSize <= int.MaxValue).WithMessage("Page offset is too large.");
        RuleFor(x => x.Status).Must(x => x is null or "Queued" or "Running" or "Done" or "Failed");
        RuleFor(x => x.DiagramId).Must(x => !x.HasValue || x.Value != Guid.Empty);
        RuleFor(x => x).Must(x => !x.From.HasValue || !x.To.HasValue || x.From < x.To).WithMessage("From must precede To.");
        RuleFor(x => x.From).Must(x => !x.HasValue || x.Value.Kind == DateTimeKind.Utc).WithMessage("From must be UTC (Z).");
        RuleFor(x => x.To).Must(x => !x.HasValue || x.Value.Kind == DateTimeKind.Utc).WithMessage("To must be UTC (Z).");
    }
}

public sealed record RenderJobResponse(Guid Id, Guid DiagramId, Guid ProjectId, int DiagramVersion, string Status, int Attempt, string? Error, DateTime CreatedAt, DateTime? StartedAt, DateTime? FinishedAt, long? DurationMs);
