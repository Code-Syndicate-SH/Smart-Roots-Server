using FluentValidation;
using Smart_Roots_Server.Infrastructure.Dtos;

namespace Smart_Roots_Server.Infrastructure.Validation
{
    public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }
}
