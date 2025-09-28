using System.Text;
using System.Text.Json;
using MQTTnet;
using Smart_Roots_Server.Data;
using Smart_Roots_Server.Infrastructure.Dtos;
using Smart_Roots_Server.Infrastructure.Models;

namespace Smart_Roots_Server.Services
{
    public sealed class MqttSubscriber
    {
        private readonly IMqttClient _mqttClient;
        private readonly ILogger<MqttSubscriber> _logger;
        private readonly ISensorLogRepository _repo;

        public MqttSubscriber(ILogger<MqttSubscriber> logger, IMqttClient mqttClient, ISensorLogRepository repo)
        {
            _mqttClient = mqttClient;
            _logger = logger;
            _repo = repo;
        }

        public async Task SubscribeAsync(string topic, CancellationToken ct = default)
        {
            if (_mqttClient == null)
            {
                _logger.LogCritical("MQTT client is null");
                return;
            }

            _logger.LogInformation("Subscribing to topic {Topic}", topic);
            await _mqttClient.SubscribeAsync(topic, cancellationToken: ct);

            // Persist EVERY incoming message; never block the data path on DB issues.
            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                try
                {
                    var dto = JsonSerializer.Deserialize<SensorReadingDto>(
                        payloadJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    ) ?? new();

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

                    await _repo.InsertAsync(doc, ct);
                }
                catch (Exception ex)
                {
                    // Log and continue; user-facing streams must not be affected
                    _logger.LogError(ex, "Failed to persist sensor reading. Payload: {Payload}", payloadJson);
                }
            };
        }

        private static int ToInt(double? v) => v.HasValue ? (int)Math.Round(v.Value) : 0;
    }
}
