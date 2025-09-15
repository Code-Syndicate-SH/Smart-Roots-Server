using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Smart_Roots_Server.Infrastructure.Models {
    public class SensorLogs {

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string MacAddress { get; set; } // this is where the data will be coming from

        [BsonElement("Temperature")]
        public int Temperature { get; set; }

        [BsonElement("Ec")]
        public int Ec { get; set; }
        [BsonElement("FlowRate")]
        public int FlowRate { get; set; }
        [BsonElement("PH")]
        public int PH { get; set; }
        [BsonElement("Light")]
        public int Light { get; set; }
        [BsonElement("Humidity")]
        public int Humidity { get; set; }

        public DateTime Created_At { get; set; }
        
    }
}
