using Smart_Roots_Server.Infrastructure.Models;
using System.Threading.Tasks;

namespace Smart_Roots_Server.Data {
    public class SupabaseSQLClient {
        private readonly Supabase.Client _supabaseClient;
        private readonly ILogger _logger;
        public SupabaseSQLClient(Supabase.Client supabaseClient, ILogger<SupabaseSQLClient> logger) {
                _supabaseClient  = supabaseClient;
                _logger = logger;          
        }

        public async Task<ImageMetaData? > GetLatestImage(string macaddress) {
            _logger.LogInformation("Fetched latest image for {MacAddress}", macaddress);
            var results =  await _supabaseClient.From<ImageMetaData>().Filter("macaddress", Supabase.Postgrest.Constants.Operator.Equals,macaddress ).Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending).Limit(1).Get();
            if(results.Model==null || results.Model == default!) {
                _logger.LogWarning("No available images to display");
            }
            return results.Model;
        }


    
    }
}
