using FluentValidation;
using Smart_Roots_Server.Infrastructure.Models;

namespace Smart_Roots_Server.Infrastructure.Validation {
    public class SensorStateValidator:AbstractValidator<SensorStates> {

        public SensorStateValidator() {
      
            RuleFor(s=>s.ECUp).GreaterThan(-1).LessThan(2);
            RuleFor(s => s.ECDown).GreaterThan(-1).LessThan(2);
            RuleFor(s => s.PHDown).GreaterThan(-1).LessThan(2);
            RuleFor(s => s.Pump).GreaterThan(-1).LessThan(2);
            RuleFor(s => s.ExtractorFan).GreaterThan(-1).LessThan(2);
            RuleFor(s => s.Fan).GreaterThan(-1).LessThan(2);
            RuleFor(s=>s.ECUp).GreaterThan(-1).LessThan(2);
            RuleFor(s=>s.Light).GreaterThan(-1).LessThan(2);
          
        }
    }
}
