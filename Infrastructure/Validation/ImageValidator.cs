using FluentValidation;
using Smart_Roots_Server.Infrastructure.Models;

namespace Smart_Roots_Server.Infrastructure.Validation {
    public class ImageValidator:AbstractValidator<Image> {

        public ImageValidator() {
            RuleFor(i => i.MacAddress).NotEmpty().NotNull().MinimumLength(10);
            RuleFor(i=>i.Base64).NotEmpty().NotNull().MinimumLength(30);
        }
    }
}
