using Smart_Roots_Server.Infrastructure.Models;

namespace Smart_Roots_Server.Data
{
    public sealed class NoOpSensorLogRepository : ISensorLogRepository
    {
        public Task InsertAsync(SensorLogs _, CancellationToken __ = default)
            => Task.CompletedTask;
    }
}
