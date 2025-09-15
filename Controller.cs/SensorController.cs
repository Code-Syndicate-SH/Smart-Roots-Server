using MQTTnet;

namespace Smart_Roots_Server.Controller.cs {
    public static class SensorController {
        // add the mongo db connection to store all the sensor logs
        public async static Task GetReadings(HttpContext httpContext,IMqttClient mqttClient) {
            if (mqttClient == null || !mqttClient.IsConnected) {

                return;
            }
            
            var result = await mqttClient.SubscribeAsync("Esp32Cam");
            httpContext.Response.Headers.Add("Content Type", "text/event-stream");
            httpContext.Response.Headers.Add("Cache-Control", "no-cache");
            httpContext.Response.Headers.Add("Connection", "keep-alive");
            await httpContext.Response.Body.FlushAsync();
            await httpContext.Response.WriteAsync(result.ToString());
            await httpContext.Response.Body.FlushAsync();
        }
    }
}
