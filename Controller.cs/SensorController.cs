using MQTTnet;
using Smart_Roots_Server.Data;
using Smart_Roots_Server.Infrastructure.Dtos;
using Smart_Roots_Server.Infrastructure.Models;
using System.Text;
using System.Text.Json;

namespace Smart_Roots_Server.Controller.cs
{
    public static class SensorController
    {
        public static async Task GetReadings(
            HttpContext httpContext,
            IMqttClient mqttClient,
            ISensorLogRepository repo,
            ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger("SensorSSE");

            if (mqttClient == null || !mqttClient.IsConnected)
            {
                httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await httpContext.Response.WriteAsync("MQTT client is not connected.");
                return;
            }

            // keep current topic to avoid breaking your smoke test
            var result = await mqttClient.SubscribeAsync("Esp32Cam");

            httpContext.Response.Headers.Add("Content-Type", "text/event-stream");
            httpContext.Response.Headers.Add("Cache-Control", "no-cache");
            httpContext.Response.Headers.Add("Connection", "keep-alive");

            var cts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);

            Func<MqttApplicationMessageReceivedEventArgs, Task> handler = async e =>
            {
                var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                // 1) LOG FIRST
                try
                {
                    var dto = JsonSerializer.Deserialize<SensorReadingDto>(payloadJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                    var doc = new SensorLogs
                    {
                        MacAddress = dto.MacAddress ?? "unknown",
                        Temperature = ToInt(dto.Temperature),
                        Ec = ToInt(dto.Ec),
                        FlowRate = ToInt(dto.FlowRate),
                        PH = ToInt(dto.PH),
                        Light = ToInt(dto.Light),
                        Humidity = ToInt(dto.Humidity),
                        Created_At = dto.ts ?? dto.createdAt ?? DateTime.UtcNow
                    };

                    await repo.InsertAsync(doc, cts.Token);
                }
                catch (Exception ex)
                {
                    // DB failures must NOT break the stream
                    logger.LogError(ex, "Failed to persist sensor reading. Continuing SSE.");
                }

                // 2) THEN SEND TO USER
                try
                {
                    await httpContext.Response.WriteAsync($"data: {payloadJson}\n\n", cts.Token);
                    await httpContext.Response.Body.FlushAsync(cts.Token);
                }
                catch (OperationCanceledException) { /* client disconnected */ }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to write SSE frame.");
                }
            };

            mqttClient.ApplicationMessageReceivedAsync += handler;

            try { await Task.Delay(Timeout.Infinite, cts.Token); }
            catch (OperationCanceledException) { }
            finally { mqttClient.ApplicationMessageReceivedAsync -= handler; }

            static int ToInt(double? v) => v.HasValue ? (int)Math.Round(v.Value) : 0;
        }
    }
}
