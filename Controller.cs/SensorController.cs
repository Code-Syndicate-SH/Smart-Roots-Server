using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using MQTTnet;
using Smart_Roots_Server.Infrastructure.Models;
using System.Text;
using System.Text.Json;

namespace Smart_Roots_Server.Controller.cs {
    public static class SensorController {
        public static async Task GetReadings(HttpContext httpContext, IMqttClient mqttClient) {
            if (mqttClient == null || !mqttClient.IsConnected) {
                httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await httpContext.Response.WriteAsync("MQTT client is not connected.");
                return;
            }

            // IMPORTANT: do not subscribe here (subscription happens at startup)

            httpContext.Response.Headers.Add("Content-Type", "text/event-stream");
            httpContext.Response.Headers.Add("Cache-Control", "no-cache");
            httpContext.Response.Headers.Add("Connection", "keep-alive");

            var cts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);

            Func<MqttApplicationMessageReceivedEventArgs, Task> handler = async e => {
                var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                try {
                    await httpContext.Response.WriteAsync($"data: {payloadJson}\n\n", cts.Token);
                    await httpContext.Response.Body.FlushAsync(cts.Token);
                }
                catch (OperationCanceledException) {
                    // client disconnected
                }
            };

            mqttClient.ApplicationMessageReceivedAsync += handler;

            try { await Task.Delay(Timeout.Infinite, cts.Token); }
            catch (OperationCanceledException) { }
            finally { mqttClient.ApplicationMessageReceivedAsync -= handler; }
        }

        public static async Task<IResult> ToggleComponent([FromServices] IMqttClient mqttClient, [FromBody] SensorStates sensorStates,String id,  [FromServices] IValidator<SensorStates> validator,[FromServices] ILogger<SensorStates> logger) {
            if (mqttClient == null) return TypedResults.Problem();
            await validator.ValidateAndThrowAsync(sensorStates);
            
            string topicToggle = $"Toggle/{id}";
            try {
                var payload = JsonSerializer.Serialize(sensorStates);
                payload.ToString();
                await mqttClient.PublishStringAsync(topicToggle, payload);
            }
            catch (Exception ex) {
                logger.LogError(ex.Message);
               return  TypedResults.BadRequest();
            }
            return TypedResults.Ok();
        }
    }
}
