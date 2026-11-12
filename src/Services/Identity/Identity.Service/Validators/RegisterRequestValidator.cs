using System.Text;
using FluentValidation;
using Identity.Service.DTOs;

namespace Identity.Service.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Tên đăng nhập không được để trống.")
            .MaximumLength(50).WithMessage("Tên đăng nhập tối đa 50 ký tự.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có độ dài tối thiểu 8 ký tự.")
            .Must(password => string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) <= 72)
            .WithMessage("Mật khẩu không được vượt quá 72 bytes UTF-8.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ và tên tối đa 100 ký tự.");
    }
}
