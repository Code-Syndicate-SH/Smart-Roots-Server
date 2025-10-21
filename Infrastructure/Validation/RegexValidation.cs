using System.Text.RegularExpressions;

namespace Smart_Roots_Server.Infrastructure.Validation {
    public class RegexValidation {

        public static bool IsValidMacAddress(string macAddress) {
            var regex = new Regex("^([0-9A-Fa-f]{2}([-:])){5}([0-9A-Fa-f]{2})$");
            return regex.IsMatch(macAddress);

        }
    }
}
