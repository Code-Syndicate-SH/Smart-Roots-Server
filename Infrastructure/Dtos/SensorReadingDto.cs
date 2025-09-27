namespace Smart_Roots_Server.Infrastructure.Dtos
{
    public class SensorReadingDto
    {
        public string? MacAddress { get; set; }
        public double? Temperature { get; set; }
        public double? Ec { get; set; }
        public double? FlowRate { get; set; }
        public double? PH { get; set; }
        public double? Light { get; set; }
        public double? Humidity { get; set; }
        public DateTime? ts { get; set; }
        public DateTime? createdAt { get; set; }
    }
}
