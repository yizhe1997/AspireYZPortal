using StackExchange.Redis;
using System.Text.Json;

namespace AspireApp1.BacktestWorker.Queue;

public class RedisQueueConsumer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisQueueConsumer> _logger;
    private const string QueueKey = "backtests:queue";
    private const string DeadLetterQueueKey = "backtests:dlq";
    private const string RetryPrefix = "backtests:retry:";

    public RedisQueueConsumer(IConnectionMultiplexer redis, ILogger<RedisQueueConsumer> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<BacktestJobMessage?> DequeueAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        try
        {
            // BRPOP blocks until item available or timeout
            var result = await db.ListRightPopAsync(QueueKey);
            
            if (!result.HasValue)
            {
                return null;
            }

            var message = JsonSerializer.Deserialize<BacktestJobMessage>(result.ToString());
            _logger.LogInformation("Dequeued job {RunId}", message?.RunId);
            
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dequeuing from Redis");
            return null;
        }
    }

    public async Task<int> GetRetryCountAsync(Guid runId)
    {
        var db = _redis.GetDatabase();
        var retryKey = $"{RetryPrefix}{runId}";
        var count = await db.StringGetAsync(retryKey);
        return count.HasValue ? (int)count : 0;
    }

    public async Task IncrementRetryCountAsync(Guid runId)
    {
        var db = _redis.GetDatabase();
        var retryKey = $"{RetryPrefix}{runId}";
        await db.StringIncrementAsync(retryKey);
        await db.KeyExpireAsync(retryKey, TimeSpan.FromDays(1));
    }

    public async Task MoveToDeadLetterQueueAsync(BacktestJobMessage message, string errorMessage)
    {
        var db = _redis.GetDatabase();
        
        var dlqEntry = new
        {
            Message = message,
            Error = errorMessage,
            MovedAt = DateTime.UtcNow
        };
        
        var json = JsonSerializer.Serialize(dlqEntry);
        await db.ListLeftPushAsync(DeadLetterQueueKey, json);
        
        _logger.LogWarning("Moved job {RunId} to DLQ: {Error}", message.RunId, errorMessage);
    }

    public async Task<int> GetDeadLetterQueueDepthAsync()
    {
        var db = _redis.GetDatabase();
        return (int)await db.ListLengthAsync(DeadLetterQueueKey);
    }
}

public class BacktestJobMessage
{
    public Guid RunId { get; set; }
    public Guid StrategyId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Parameters { get; set; } = "{}";
    public Guid UserId { get; set; }
    public decimal InitialCapital { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
