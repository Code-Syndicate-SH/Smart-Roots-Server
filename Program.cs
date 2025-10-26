namespace Smart_Roots_Server
{
    using FluentValidation;
    using MongoDB.Driver;
    using MQTTnet;                         // NOTE: only root MQTTnet namespace
    using Smart_Roots_Server.Data;
    using Smart_Roots_Server.Exceptions;
    using Smart_Roots_Server.Infrastructure.Dtos;
    using Smart_Roots_Server.Infrastructure.Models;
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
            builder.Services.AddSingleton<SupabaseSQLClient>();
            // --- MongoDB DI ---
            string connectionUri = builder.Configuration.GetConnectionString("MongoDb")!;
            var mongoSettings = MongoClientSettings.FromConnectionString(connectionUri);
            mongoSettings.ServerApi = new ServerApi(ServerApiVersion.V1);
            // Explicitly enforce TLS 1.2 (Atlas requires this)
            mongoSettings.SslSettings = new SslSettings {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
            };

            // Give Atlas a bit more breathing room (especially if on slow network / Raspberry Pi)
            mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            mongoSettings.ConnectTimeout = TimeSpan.FromSeconds(10);
            mongoSettings.SocketTimeout = TimeSpan.FromSeconds(30);

            // Optional: retry write operations automatically
            mongoSettings.RetryWrites = true;

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
            builder.Services.AddScoped<IValidator<SensorStates>, SensorStateValidator>();
            builder.Services.AddScoped<IValidator<Image>, ImageValidator>();
            // API services
            builder.Services.AddAuthorization();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            var port = Environment.GetEnvironmentVariable("PORT");
            // builder.WebHost.UseUrls($"http://*:{port}");

            // validators
            builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
            builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
            builder.Services.AddScoped<IValidator<TentUpsertDto>, TentUpsertDtoValidator>();

            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<ISupabaseAuthService, SupabaseAuthService>();
            builder.Services.AddSingleton<ITentRepository, TentRepositorySupabase>();


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
            _ =mqttSubscriber.SubscribeAsync(emqxTopic);
            app.MapGet("/", () => {
                return TypedResults.Ok("Server is up");
            });
            app.MapGet("/health", () => Results.Ok(new {
                status = "Healthy",
                environment = app.Environment.EnvironmentName,
                startedAt = DateTime.UtcNow
            }));
            // Routes
            app.MapGroup("/api/images")
               .MapImagesApi()
               .WithTags("Images")
               .WithDescription("Images from espCam");

            app.MapGroup("/api/sensors")
               .MapSensorApis()
               .WithTags("Sensors")
               .WithDescription("Data from the sensors");

            app.MapGroup("/api/auth")
   .MapAuthApis()
   .WithTags("Auth")
   .WithDescription("Supabase authentication (register/login)");

            app.MapGroup("/api/tents")
               .MapTentApis()
               .WithTags("Tents")
               .WithDescription("Public read, protected write for Tent entities");



            app.Run();
        }
    }
}
