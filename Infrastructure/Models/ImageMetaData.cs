using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace Smart_Roots_Server.Infrastructure.Models {
    [Table("ImageMetaData")]
    public class ImageMetaData:BaseModel {
        [PrimaryKey("id")]
        public int Id { get; set; }
        [Column("create_at")]
        public DateTime CreatedAt { get; set; }
        [Column("mac_address")]
        public string MacAddress { get; set; }

        [Column("public_url")]
        public string PublicURl { get; set; }
        [Column("last_fetched")]
        public DateTime LastFetched { get; set; }
    }

}
