
namespace Smart_Roots_Server {
    using FluentValidation;
    using MQTTnet;
    using Smart_Roots_Server.Data;
    using Smart_Roots_Server.Exceptions;
    using Smart_Roots_Server.Infrastructure.Models;
    using Smart_Roots_Server.Infrastructure.Validation;
    using Smart_Roots_Server.Routes;
    using Smart_Roots_Server.Services;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.Json;
    using System.Threading.Tasks;
    using MongoDB.Driver;
    using MongoDB.Bson;
    public class Program {
        public static async Task Main(string[] args) {
            //string certPath = "Certificate.cer";
            // var certificate = new X509Certificate2(certPath);
           
            var builder = WebApplication.CreateBuilder(args);
           
            builder.Services.AddProblemDetails(config =>
            config.CustomizeProblemDetails = context => {
                context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
            });
            var url = builder.Configuration.GetSection("SUPABASE:URL").Get<string>();
            var key = builder.Configuration.GetSection("SUPABASE:KEY").Get<string>();
            var supabaseOptions = new Supabase.SupabaseOptions {

                AutoRefreshToken = true,

                AutoConnectRealtime = true,
            };
            var supabase = new Supabase.Client(url, key, supabaseOptions);
            builder.Services.AddSingleton(provider => new Supabase.Client(url, key, supabaseOptions));
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
            builder.Services.AddScoped<IValidator<Image>, ImageValidator>();
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
            if (options.ChannelOptions is MqttClientTcpOptions tcpOptions) {
                tcpOptions.AddressFamily = AddressFamily.InterNetwork;
            }
            builder.Services.AddSingleton<IMqttClient>(new MqttClientFactory().CreateMqttClient());
            // mongo db
             
             string connectionUri = builder.Configuration.GetConnectionString("MongoDb")!;
            var settings = MongoClientSettings.FromConnectionString(connectionUri);
            // Set the ServerApi field of the settings object to set the version of the Stable API on the client
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);
            // Create a new client and connect to the server
            var client = new MongoClient(settings);
            // Send a ping to confirm a successful connection
            try {
                var result = client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                Console.WriteLine("Pinged your deployment. You successfully connected to MongoDB!");
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
            }
            //

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
                .WithTags("Images")
                .WithDescription("Images from espCam");

            app.MapGroup("/api/sensors")
                .MapSensorApis()
                .WithTags("Sensors")
                .WithDescription("Data from the sensors");

            app.Run();
        }
    }
}
