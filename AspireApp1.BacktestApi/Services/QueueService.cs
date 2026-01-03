using StackExchange.Redis;

namespace AspireApp1.BacktestApi.Services;

public class QueueService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<QueueService> _logger;
    private const string QueueKey = "backtests:queue";
    private const string ProcessingKey = "backtests:processing";
    private const string CancelledKey = "backtests:cancelled";
    private const int AvgBacktestDurationSeconds = 300; // 5 minutes average

    public QueueService(IConnectionMultiplexer redis, ILogger<QueueService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Get queue position and estimated start time for a backtest run.
    /// </summary>
    public async Task<(int QueuePosition, DateTime? EstimatedStartTime)> GetQueuePositionAsync(Guid runId)
    {
        try
        {
            var db = _redis.GetDatabase();

            // Check if run is currently processing
            var isProcessing = await db.SetContainsAsync(ProcessingKey, runId.ToString());
            if (isProcessing)
            {
                return (0, null); // Currently processing
            }

            // Check if run is cancelled
            var isCancelled = await db.SetContainsAsync(CancelledKey, runId.ToString());
            if (isCancelled)
            {
                return (-1, null); // Cancelled
            }

            // Get queue position
            var queueLength = await db.ListLengthAsync(QueueKey);
            var queueItems = await db.ListRangeAsync(QueueKey, 0, -1);

            int position = -1;
            for (int i = 0; i < queueItems.Length; i++)
            {
                if (queueItems[i].ToString() == runId.ToString())
                {
                    position = i;
                    break;
                }
            }

            if (position == -1)
            {
                return (-1, null); // Not in queue
            }

            // Estimate start time: (position * avg_duration) + now
            var estimatedSeconds = position * AvgBacktestDurationSeconds;
            var estimatedStart = DateTime.UtcNow.AddSeconds(estimatedSeconds);

            return (position, estimatedStart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue position for run {RunId}", runId);
            return (-1, null);
        }
    }

    /// <summary>
    /// Mark a run as cancelled and remove from queue if present.
    /// </summary>
    public async Task<bool> CancelRunAsync(Guid runId)
    {
        try
        {
            var db = _redis.GetDatabase();

            // Remove from queue if present
            var removed = await db.ListRemoveAsync(QueueKey, runId.ToString());

            // Add to cancelled set
            await db.SetAddAsync(CancelledKey, runId.ToString());

            _logger.LogInformation("Run {RunId} cancelled, queue removed: {Removed}", runId, removed > 0);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling run {RunId}", runId);
            return false;
        }
    }

    /// <summary>
    /// Check if a run is cancelled.
    /// </summary>
    public async Task<bool> IsRunCancelledAsync(Guid runId)
    {
        try
        {
            var db = _redis.GetDatabase();
            return await db.SetContainsAsync(CancelledKey, runId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cancelled status for run {RunId}", runId);
            return false;
        }
    }

    /// <summary>
    /// Get queue depth (number of pending jobs).
    /// </summary>
    public async Task<int> GetQueueDepthAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var length = await db.ListLengthAsync(QueueKey);
            return (int)length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue depth");
            return 0;
        }
    }

    /// <summary>
    /// Mark a run as processing.
    /// </summary>
    public async Task<bool> MarkAsProcessingAsync(Guid runId)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.SetAddAsync(ProcessingKey, runId.ToString());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking run {RunId} as processing", runId);
            return false;
        }
    }

    /// <summary>
    /// Mark a run as no longer processing.
    /// </summary>
    public async Task<bool> UnmarkAsProcessingAsync(Guid runId)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.SetRemoveAsync(ProcessingKey, runId.ToString());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unmarking run {RunId} as processing", runId);
            return false;
        }
    }
}
