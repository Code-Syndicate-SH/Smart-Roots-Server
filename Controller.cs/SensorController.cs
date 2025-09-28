using MQTTnet;
using System.Text;

namespace Smart_Roots_Server.Controller.cs
{
    public static class SensorController
    {
        public static async Task GetReadings(HttpContext httpContext, IMqttClient mqttClient)
        {
            if (mqttClient == null || !mqttClient.IsConnected)
            {
                httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await httpContext.Response.WriteAsync("MQTT client is not connected.");
                return;
            }

            // IMPORTANT: do not subscribe here (subscription happens at startup)

            httpContext.Response.Headers.Add("Content-Type", "text/event-stream");
            httpContext.Response.Headers.Add("Cache-Control", "no-cache");
            httpContext.Response.Headers.Add("Connection", "keep-alive");

            var cts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);

            Func<MqttApplicationMessageReceivedEventArgs, Task> handler = async e =>
            {
                var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                try
                {
                    await httpContext.Response.WriteAsync($"data: {payloadJson}\n\n", cts.Token);
                    await httpContext.Response.Body.FlushAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // client disconnected
                }
            };

            mqttClient.ApplicationMessageReceivedAsync += handler;

            try { await Task.Delay(Timeout.Infinite, cts.Token); }
            catch (OperationCanceledException) { }
            finally { mqttClient.ApplicationMessageReceivedAsync -= handler; }
        }
    }
}
