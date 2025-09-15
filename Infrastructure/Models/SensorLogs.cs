using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Smart_Roots_Server.Infrastructure.Models {
    public class SensorLogs {
      
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string? Id { get; set; }

            [BsonElement("Name")]
            public int Temperature { get; set; };

            public int Ec { get; set; }

            public int FlowRate { get; set; }

            public int PH { get; set; }
        public int Light {  get; set; }

        
    }
}
