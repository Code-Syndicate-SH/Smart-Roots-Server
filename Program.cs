
namespace Smart_Roots_Server {
    using Microsoft.Extensions.Logging;
    using MQTTnet;
    using Smart_Roots_Server.Data;
    using Smart_Roots_Server.Exceptions;
    using Smart_Roots_Server.Routes;
    using Smart_Roots_Server.Services;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    public class Program {
        public static async Task Main(string[] args) {
            //string certPath = "Certificate.cer";
            // var certificate = new X509Certificate2(certPath);
            var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
            var key = Environment.GetEnvironmentVariable("SUPABASE_KEY");
            var supabaseOptions = new Supabase.SupabaseOptions {
                AutoConnectRealtime = true,
            };
            var supabase = new Supabase.Client(url, key, supabaseOptions);
            
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddSingleton(provider => new Supabase.Client(url, key, supabaseOptions));
            builder.Services.AddProblemDetails(config =>
            config.CustomizeProblemDetails = context => {
                context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
            });
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            string broker = "e902c05a.ala.eu-central-1.emqxsl.com";
            int port = 8883;
            //string clientId = Guid.NewGuid().ToString();
            string topic = "Images";
            string username = "ShravanRamjathan";
            string password = "EmwDW3HGRDsg8Je";
            builder.Services.AddSingleton<MqttSubscriber>();
            builder.Services.AddSingleton<SupabaseStorageContext>();
            // Create a MQTT client factory

            // Create MQTT client options

            var options = new MqttClientOptionsBuilder()
               .WithTcpServer(broker, port) // MQTT broker address and port
               .WithCredentials(username, password) // Set username and password

               .WithCleanSession(true)
               .WithTlsOptions(
                   o => {

                       o.WithCertificateValidationHandler(_ => true);
                   }
               )
               .Build();
            builder.Services.AddSingleton<IMqttClient>(new MqttClientFactory().CreateMqttClient());


            Console.WriteLine(" connect");
            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment()) {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            
            var mqttClient = app.Services.GetRequiredService<IMqttClient>();
            await mqttClient.ConnectAsync(options);
            while (!mqttClient.IsConnected) {
                Console.WriteLine("cant connect");
                await mqttClient.ReconnectAsync(CancellationToken.None);
            }
            var mqttSubscriber = app.Services.GetRequiredService<MqttSubscriber>();
            await mqttSubscriber.SubscribeAsync(topic);
            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapGroup("/api/images")
                .MapImagesApi()
                .WithTags("public")
                .WithDescription("Images from espCam");


            app.Run();
        }
    }
}
