using FluentValidation;
using Workspace.Service.DTOs;

namespace Workspace.Service.Validators;

public class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username không được để trống.")
            .MaximumLength(50).WithMessage("Username tối đa 50 ký tự.");
    }
}

public class AssignLecturerRequestValidator : AbstractValidator<AssignLecturerRequest>
{
    public AssignLecturerRequestValidator()
    {
        RuleFor(x => x.LecturerId)
            .NotEmpty().WithMessage("Mã giảng viên không được để trống.");
    }
}
