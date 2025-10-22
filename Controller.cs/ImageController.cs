using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Smart_Roots_Server.Data;
using Smart_Roots_Server.Infrastructure;
using Smart_Roots_Server.Infrastructure.Models;
using Smart_Roots_Server.Infrastructure.Validation;



namespace Smart_Roots_Server.Controller.cs {
    public static class ImageController {
        const string META_DATA_PREFIX = "data:image/jpeg;base64,";
        public async static Task<IResult> Create(HttpContext httpContext, [FromServices] ILogger<ImageResult> logger, [FromServices] IValidator<Image> validator, [FromBody] Image image, [FromServices] SupabaseStorageContext supabaseStorage) {
            if (image == null) {
               
                return TypedResults.BadRequest("Invalid image sent");
            }

            await validator.ValidateAndThrowAsync(image);
            if (image.Base64.StartsWith(META_DATA_PREFIX)){
              image.Base64 =   image.Base64.Substring(META_DATA_PREFIX.Length);
            }

           
            byte[] imageBytes = default!;
            try {
                 imageBytes = Convert.FromBase64String(image.Base64);
            }catch(Exception ex) {
             
                return TypedResults.BadRequest("Image may have been malformed");
            }
            string url = await supabaseStorage.UploadImageToBucket("Esp32Cam", imageBytes, image.MacAddress);
            if (url == null) {
                throw new();
            }
            return TypedResults.Ok(new { ImageLink = url });
        }
        public static async Task<IResult> GetLatestImageAsync([FromServices] ILogger<ImageMetaData> logger, [FromServices] SupabaseSQLClient supabaseSQLClient,[FromBody] string macaddress) {
            if (!RegexValidation.IsValidMacAddress(macaddress)) return TypedResults.BadRequest("Invalid tent address.");

            var latestImageData =await supabaseSQLClient.GetLatestImage(macaddress);
            if (latestImageData == null) return TypedResults.BadRequest("No images to be found for the current system");
            return TypedResults.Ok(new { Url = latestImageData.PublicURl });
        }
    }
}
