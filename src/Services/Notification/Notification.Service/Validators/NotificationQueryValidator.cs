using FluentValidation;
using UMLForge.Notification.Service.Models;

namespace UMLForge.Notification.Service.Validators;

public sealed class NotificationQueryValidator : AbstractValidator<NotificationQuery>
{
    public NotificationQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Type).MaximumLength(80);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.Sort).Must(x => x is "createdAt:asc" or "createdAt:desc")
            .WithMessage("Sort must be createdAt:asc or createdAt:desc.");
        RuleFor(x => x).Must(x => ((long)x.Page - 1) * x.PageSize <= int.MaxValue)
            .OverridePropertyName("Page").WithMessage("Page offset exceeds the supported range.");
    }
}
