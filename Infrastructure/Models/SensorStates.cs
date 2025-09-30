using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Smart_Roots_Server.Infrastructure.Models {
    public class SensorStates {

     
        public int Fan { get; set; }
        public int EC { get; set; }
        public int Pump { get; set; }
        public int PH { get; set; }
        public int Light { get; set; }
        public int ExtractorFan { get; set; }

    }
}
