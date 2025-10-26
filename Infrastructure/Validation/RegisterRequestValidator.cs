using FluentValidation;
using Smart_Roots_Server.Infrastructure.Dtos;

namespace Smart_Roots_Server.Infrastructure.Validation
{
    public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

            RuleFor(x => x.Role)
                .Must(r => r is null || r.Equals("user", System.StringComparison.OrdinalIgnoreCase) || r.Equals("researcher", System.StringComparison.OrdinalIgnoreCase))
                .WithMessage("Role must be 'user' or 'researcher' (admin cannot register).");
        }
    }
}
