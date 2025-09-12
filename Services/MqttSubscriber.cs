using Microsoft.AspNetCore.Mvc;
using MQTTnet;

namespace Smart_Roots_Server.Services {
    public sealed class MqttSubscriber {
        private readonly IMqttClient _mqttClient;
        private readonly ILogger<MqttSubscriber> _logger;
        public MqttSubscriber( ILogger<MqttSubscriber> logger,  IMqttClient mqttClient) {
        _mqttClient = mqttClient;
        _logger = logger;
        }
    
        public async Task SubscribeAsync(string topic) {
            if (_mqttClient == null) {
                _logger.LogCritical("Unable to connect to client");
            } // create an event to trigger on this
            _logger.LogInformation("Trying to subscribe to topic");
            await  _mqttClient.SubscribeAsync(topic);
            
        }
    }
}
