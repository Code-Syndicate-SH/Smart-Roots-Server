using Smart_Roots_Server.Controller.cs;

namespace Smart_Roots_Server.Routes {
    public static  class ImageRoute {

        public static RouteGroupBuilder MapImagesApi(this RouteGroupBuilder group) {
            group.MapPost("/", ImageController.Create);
            group.MapGet("/{macaddress}", ImageController.GetLatestImageAsync);
            return group;
        }
    }
}
