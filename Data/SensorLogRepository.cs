using MongoDB.Driver;
using Smart_Roots_Server.Infrastructure.Models;

namespace Smart_Roots_Server.Data
{
    public interface ISensorLogRepository
    {
        Task InsertAsync(SensorLogs doc, CancellationToken ct = default);
    }

    public sealed class SensorLogRepository : ISensorLogRepository
    {
        private readonly IMongoCollection<SensorLogs> _col;
        private readonly ILogger<SensorLogRepository> _logger;

        public SensorLogRepository(IMongoClient client, IConfiguration cfg, ILogger<SensorLogRepository> logger)
        {
            _logger = logger;
            var db = client.GetDatabase(cfg.GetValue<string>("Mongo:Database") ?? "SmartRoots");
            _col = db.GetCollection<SensorLogs>(cfg.GetValue<string>("Mongo:SensorLogsCollection") ?? "sensor_logs");

            try
            {
                var keys = Builders<SensorLogs>.IndexKeys
                    .Ascending(x => x.MacAddress)
                    .Descending(x => x.Created_At);
                _col.Indexes.CreateOne(new CreateIndexModel<SensorLogs>(keys));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping Mongo index creation (Mongo not reachable right now).");
            }
        }

        public Task InsertAsync(SensorLogs doc, CancellationToken ct = default) =>
            _col.InsertOneAsync(doc, cancellationToken: ct);
    }
}
