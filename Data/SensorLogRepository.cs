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

            var dbName = cfg.GetValue<string>("Mongo:Database") ?? "SmartRoots";
            var colName = cfg.GetValue<string>("Mongo:SensorLogsCollection") ?? "sensor_logs";

            var db = client.GetDatabase(dbName);
            _col = db.GetCollection<SensorLogs>(colName);

            // Helpful index; never block startup if Mongo is unreachable.
            try
            {
                var keys = Builders<SensorLogs>.IndexKeys
                    .Ascending(x => x.MacAddress)
                    .Descending(x => x.Created_At);

                _col.Indexes.CreateOne(new CreateIndexModel<SensorLogs>(keys));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping Mongo index creation (Mongo not reachable).");
            }
        }

        public Task InsertAsync(SensorLogs doc, CancellationToken ct = default) =>
            _col.InsertOneAsync(doc, cancellationToken: ct);
    }
}
