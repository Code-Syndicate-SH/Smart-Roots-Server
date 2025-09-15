using Smart_Roots_Server.Controller.cs;

namespace Smart_Roots_Server.Routes {
    public static class SensorRoute {
        public static RouteGroupBuilder MapSensorApis(this RouteGroupBuilder group) {
            
            group.MapGet("/events", SensorController.GetReadings);
            return group;
        }
    }
}
