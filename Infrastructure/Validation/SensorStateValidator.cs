using FluentValidation;
using Smart_Roots_Server.Infrastructure.Models;

namespace Smart_Roots_Server.Infrastructure.Validation {
    public class SensorStateValidator:AbstractValidator<SensorStates> {

        public SensorStateValidator() {
      
            RuleFor(s=>s.EC).GreaterThan(-1).LessThan(1400);
            RuleFor(s => s.PH).GreaterThan(-14).LessThan(14);
            RuleFor(s => s.Pump).GreaterThan(-1).LessThan(2);
            RuleFor(s => s.ExtractorFan).GreaterThan(-1).LessThan(101);
            RuleFor(s => s.Fan).GreaterThan(-1).LessThan(100);
          
        }
    }
}
