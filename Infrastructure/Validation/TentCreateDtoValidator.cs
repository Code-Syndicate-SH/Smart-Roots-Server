using FluentValidation;
using Smart_Roots_Server.Infrastructure.Dtos;
using System.Text.RegularExpressions;

namespace Smart_Roots_Server.Infrastructure.Validation
{
    public sealed class TentCreateDtoValidator : AbstractValidator<TentCreateDto>
    {
        private static readonly Regex MacRegex = new(@"^([0-9A-Fa-f]{2}([-:])){5}([0-9A-Fa-f]{2})$", RegexOptions.Compiled);

        public TentCreateDtoValidator()
        {
            RuleFor(x => x.MacAddress)
                .NotEmpty().WithMessage("MacAddress is required.")
                .Must(mac => MacRegex.IsMatch(NormalizeMac(mac)))
                .WithMessage("MacAddress must be 6 octets (e.g., AA:BB:CC:DD:EE:FF).");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

            RuleFor(x => x.TentType)
                .Must(t => t is null || t.Equals("veg", System.StringComparison.OrdinalIgnoreCase) || t.Equals("fodder", System.StringComparison.OrdinalIgnoreCase))
                .WithMessage("tentType must be 'veg' or 'fodder'.");
        }

        public static string NormalizeMac(string mac)
        {
            var octets = Regex.Matches(mac ?? string.Empty, "[0-9A-Fa-f]{2}");
            return octets.Count == 6 ? string.Join(":", octets.Select(m => m.Value.ToUpperInvariant())) : mac ?? string.Empty;
        }
    }
}
