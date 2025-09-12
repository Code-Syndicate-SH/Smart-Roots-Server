using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Smart_Roots_Server.Data;
using Smart_Roots_Server.Infrastructure.Models;



namespace Smart_Roots_Server.Controller.cs {
    public static class ImageController {

        public async static Task<IResult> Create(HttpContext httpContext, [FromServices] ILogger logger, [FromServices] IValidator<Image> validator, [FromBody] Image image, [FromServices] SupabaseStorageContext supabaseStorage) {
            if (image == null) {
                logger.LogError("Invalid image sent to the system");
                return TypedResults.BadRequest("Invalid image sent");
            }

            await validator.ValidateAndThrowAsync(image);
            logger.LogInformation("Seems to be a valid image sent over");
            byte[] imageBytes = Convert.FromBase64String(image.Base64);
            if (imageBytes is null) {
                logger.LogError("Error decoding image");
                logger.LogError("Malform image or invalid binary");
                return TypedResults.BadRequest("Image may have been malformed");

            }
            string url = await supabaseStorage.UploadImageToBucket("Esp32Cam", imageBytes, image.MacAddress);
            if (url == null) {
                throw new();
            }
            return TypedResults.Ok(new { ImageLink = url });
        }
    }
}
