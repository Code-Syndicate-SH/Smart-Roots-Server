using Smart_Roots_Server.Controller.cs;

namespace Smart_Roots_Server.Routes {
    public static class SensorRoute {
        public static RouteGroupBuilder MapSensorApis(this RouteGroupBuilder group) {
            
            group.MapGet("/", SensorController.GetReadings);
            group.MapPut("/toggle/{id}", SensorController.ToggleComponent);
            group.MapGet("/{macAddress}", SensorController.GetReadingWithMacAddress);
            return group;
        }
    }
}
