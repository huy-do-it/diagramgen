using System.Text;
using FluentValidation;
using UMLForge.Contracts.Events;

namespace UMLForge.Render.Service.Validators;

public sealed class RenderRequestedValidator : AbstractValidator<RenderRequested>
{
    public RenderRequestedValidator()
    {
        RuleFor(x => x.DiagramId).NotEmpty();
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.DiagramType).InclusiveBetween(1, 5);
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ModelJson).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(json => Encoding.UTF8.GetByteCount(json) <= 256 * 1024)
            .WithMessage("ModelJson must not exceed 256 KiB UTF-8.");
    }
}
