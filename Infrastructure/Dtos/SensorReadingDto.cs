namespace Smart_Roots_Server.Infrastructure.Dtos
{
    // Keep everything nullable so unexpected payloads never crash logging.
    public class SensorReadingDto
    {
        public string? MacAddress { get; set; }
        public double? Temperature { get; set; }
        public double? Ec { get; set; }
        public double? FlowRate { get; set; }
        public double? PH { get; set; }
        public double? Light { get; set; }
        public double? Humidity { get; set; }

        // If device provides timestamps, we’ll prefer them
        public System.DateTime? ts { get; set; }
        public System.DateTime? createdAt { get; set; }
    }
}
