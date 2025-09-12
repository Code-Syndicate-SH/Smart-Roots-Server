using Microsoft.AspNetCore.Mvc;
using Smart_Roots_Server.Infrastructure;
using Smart_Roots_Server.Infrastructure.Dtos;
using Supabase.Interfaces;
using Supabase.Storage;
using Supabase.Storage.Interfaces;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Smart_Roots_Server.Data {
    public sealed class SupabaseStorageContext {
        private readonly Supabase.Client _client;
        private readonly ILogger<SupabaseStorageContext> _logger;
        public SupabaseStorageContext(Supabase.Client supabaseClient, ILogger<SupabaseStorageContext> logger) {
            _client = supabaseClient;
            _logger = logger;
        }

        public async Task<string> UploadImageToBucket(string bucketName, byte[] imageBytes, string macAddress) {
            string imagePath = $"{macAddress}/{Guid.NewGuid()}.jpg";
            _logger.LogInformation("Attempting to upload image to supabse");
            await _client.Storage.From(bucketName).Upload(imageBytes, imagePath);
            string url =  _client.Storage.From(bucketName).GetPublicUrl(imagePath);
            if (url == null) {
               throw new ArgumentNullException(nameof(url));
            }
            return url;
        }

     /*   public async Task<ImageResult> FetchPublicURL() {
            Task<List<FileObject>> files = _client.Storage.From("Esp32Cam").List().Result;
            if(files is null) {
                FileDto fileDto = new(new byte[0], "", default);
                return new(fileDto, false);
            }
        
        }
     */

    }
}
