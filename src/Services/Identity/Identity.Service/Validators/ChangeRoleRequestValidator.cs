using FluentValidation;
using Identity.Service.DTOs;

namespace Identity.Service.Validators;

public class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    private static readonly string[] AllowedRoles = ["Student", "Lecturer", "Admin"];

    public ChangeRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Vai trò không được để trống.")
            .Must(r => AllowedRoles.Contains(r))
            .WithMessage("Vai trò chỉ được là Student, Lecturer hoặc Admin.");
    }
}
