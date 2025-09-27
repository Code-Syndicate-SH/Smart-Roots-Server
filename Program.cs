namespace Smart_Roots_Server
{
    using FluentValidation;
    using MQTTnet;                         // NOTE: only root MQTTnet namespace
    using MongoDB.Driver;
    using Smart_Roots_Server.Data;
    using Smart_Roots_Server.Exceptions;
    using Smart_Roots_Server.Infrastructure.Validation;
    using Smart_Roots_Server.Routes;
    using Smart_Roots_Server.Services;
    using System.Net.Sockets;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ProblemDetails + global exception handler
            builder.Services.AddProblemDetails(config =>
                config.CustomizeProblemDetails = ctx =>
                    ctx.ProblemDetails.Extensions.TryAdd("requestId", ctx.HttpContext.TraceIdentifier));
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

            // --- Supabase (unchanged; assumes keys are provided via user-secrets/appsettings) ---
            var url = builder.Configuration["SUPABASE:URL"];
            var key = builder.Configuration["SUPABASE:KEY"];
            var supabaseOptions = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true,
            };
            builder.Services.AddSingleton(_ => new Supabase.Client(url, key, supabaseOptions));
            builder.Services.AddSingleton<SupabaseStorageContext>();

            // --- MongoDB DI ---
            string connectionUri = builder.Configuration.GetConnectionString("MongoDb")!;
            var mongoSettings = MongoClientSettings.FromConnectionString(connectionUri);
            mongoSettings.ServerApi = new ServerApi(ServerApiVersion.V1);
            mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(3); // fail fast in dev

            builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings));
            builder.Services.AddSingleton<ISensorLogRepository, SensorLogRepository>();

            // --- EMQX settings from config ---
            var emqxHost = builder.Configuration["Emqx:Host"] ?? "localhost";
            var emqxPort = builder.Configuration.GetValue("Emqx:Port", 1883);
            var emqxUser = builder.Configuration["Emqx:Username"];
            var emqxPass = builder.Configuration["Emqx:Password"];
            var emqxTopic = builder.Configuration["Emqx:Topic"] ?? "Readings/#";
            var emqxUseTls = builder.Configuration.GetValue("Emqx:UseTls", true);

            // MQTT client + subscriber service
            builder.Services.AddSingleton<IMqttClient>(new MqttClientFactory().CreateMqttClient());
            builder.Services.AddSingleton<MqttSubscriber>();

            // API services
            builder.Services.AddAuthorization();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseExceptionHandler();
            app.UseAuthorization();

            // --- Connect MQTT and subscribe once at startup ---
            var mqttClient = app.Services.GetRequiredService<IMqttClient>();

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(emqxHost, emqxPort)
                .WithCredentials(emqxUser, emqxPass)
                .WithCleanSession(true);

            if (emqxUseTls)
            {
                optionsBuilder.WithTlsOptions(o =>
                {
                    // Dev: accept any cert. Replace with proper CA validation later.
                    o.WithCertificateValidationHandler(_ => true);
                });
            }

            var options = optionsBuilder.Build();

            // Some MQTTnet versions expose ChannelOptions without the typed TCP class;
            // omitting AddressFamily tweak for compatibility with your package.

            await mqttClient.ConnectAsync(options);
            while (!mqttClient.IsConnected)
            {
                Console.WriteLine("MQTT not connected yet; retrying…");
                await mqttClient.ReconnectAsync(CancellationToken.None);
            }

            var mqttSubscriber = app.Services.GetRequiredService<MqttSubscriber>();
            await mqttSubscriber.SubscribeAsync(emqxTopic);

            // Routes
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
