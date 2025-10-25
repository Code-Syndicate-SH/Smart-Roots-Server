using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Smart_Roots_Server.Infrastructure.Models
{
    public class SensorLogs
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("MacAddress")]
        public string MacAddress { get; set; } = "unknown";

        [BsonElement("Temperature")] public double Temperature { get; set; }
        [BsonElement("Ec")] public double Ec { get; set; }
        [BsonElement("FlowRate")] public double FlowRate { get; set; }
        [BsonElement("PH")] public double PH { get; set; }
        [BsonElement("Light")] public double Light { get; set; }
        [BsonElement("Humidity")] public double Humidity { get; set; }

        [BsonElement("Created_At")]
        public DateTime Created_At { get; set; } = DateTime.UtcNow;
    }
}
