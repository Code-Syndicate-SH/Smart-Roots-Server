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

                    var log = ParseSafely(payloadJson);

                    await _repo.InsertAsync(log, ct);
                }
                catch (Exception ex)
                {
                    
                    _logger.LogError(ex, "Failed to persist sensor reading. Payload: {Payload}", payloadJson);
                }
            };
        }
        private SensorLogs ParseSafely(string json) {
            var log = new SensorLogs();

            try {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                log.MacAddress = root.GetStringSafe("MacAddress", "unknown");
                log.Temperature = root.GetDoubleSafe("Temperature");
                log.Ec = root.GetDoubleSafe("EC");
                log.FlowRate = root.GetDoubleSafe("FlowRate");
                log.PH = root.GetDoubleSafe("PH");
                log.Light = root.GetDoubleSafe("Light");
                log.Humidity = root.GetDoubleSafe("Humidity");
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Malformed JSON payload, using defaults where missing");
            }

            return log;
        }
    }

    // --- Safe parsing helpers for double + string ---
    internal static class JsonSafeExtensions {
        public static string GetStringSafe(this JsonElement root, string name, string fallback) {
            if (root.TryGetProperty(name, out var prop)) {
                try {
                    return prop.GetString() ?? fallback;
                }
                catch { }
            }
            return fallback;
        }

        public static double GetDoubleSafe(this JsonElement root, string name) {
            if (root.TryGetProperty(name, out var prop)) {
                try {
                    switch (prop.ValueKind) {
                        case JsonValueKind.Number:
                            if (prop.TryGetDouble(out var num))
                                return num;

                            break;
                        case JsonValueKind.String:
                            var s = prop.GetString()?.Replace(":", "").Trim();
                            if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var val))
                                return val;
                            break;
                    }
                }
                catch { }
            }
            return 0.0; // fallback for missing or malformed fields
        }



    }
}
