using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel;

namespace Smart_Roots_Server.Infrastructure.Models {
    public class SensorStates {

        [DisplayName("fan")]
        public int Fan { get; set; }
        [DisplayName("ecUp")]
        public int ECUp{ get; set; }
        [DisplayName("ecDown")]
        public int ECDown{ get; set; }
        [DisplayName("pump")]
        public int Pump { get; set; }
        [DisplayName("pHUp")]
        public int PHUp { get; set; }
        [DisplayName("pHDown")]
        public int PHDown { get; set; }
        [DisplayName("light")]
        public int Light { get; set; }
        [DisplayName("extractorFan")]
        public int ExtractorFan { get; set; }
    }
}
