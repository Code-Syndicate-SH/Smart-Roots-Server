using Microsoft.AspNetCore.Http.HttpResults;
using MQTTnet;
using System.Text;

namespace Smart_Roots_Server.Controller.cs {
    public static class SensorController {
        // add the mongo db connection to store all the sensor logs
        public async static Task GetReadings(HttpContext httpContext,IMqttClient mqttClient) {
            if (mqttClient == null || !mqttClient.IsConnected) {
                throw new Exception("Unable to fetch Sensor Readings");
            }
            var result = await mqttClient.SubscribeAsync("Esp32Cam");
            httpContext.Response.Headers.Add("Content-Type", "text/event-stream");
            httpContext.Response.Headers.Add("Cache-Control", "no-cache");
            httpContext.Response.Headers.Add("Connection", "keep-alive");
            mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                // SSE message (raw JSON goes after `data:`)
                var sseMessage = $"data: {payloadJson}\n\n";

                await httpContext.Response.WriteAsync(sseMessage);
                await httpContext.Response.Body.FlushAsync();
            };

            // Keep the request open indefinitely (until client disconnects)
            await Task.Delay(Timeout.Infinite, httpContext.RequestAborted);

        }
    }
}
