using StackExchange.Redis;
using System.Text.Json;

namespace AspireApp1.BacktestApi.Queue;

public class RedisQueueProducer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisQueueProducer> _logger;
    private const string QueueKey = "backtests:queue";
    private const string IdempotencyPrefix = "backtests:idempotency:";

    public RedisQueueProducer(IConnectionMultiplexer redis, ILogger<RedisQueueProducer> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<EnqueueResult> EnqueueBacktestAsync(BacktestJobMessage message, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        // Check idempotency
        var idempotencyKey = $"{IdempotencyPrefix}{message.IdempotencyKey}";
        var exists = await db.StringGetAsync(idempotencyKey);
        
        if (exists.HasValue)
        {
            _logger.LogInformation("Duplicate job detected: {IdempotencyKey}", message.IdempotencyKey);
            return new EnqueueResult
            {
                Success = true,
                IsDuplicate = true,
                ExistingRunId = Guid.Parse(exists.ToString())
            };
        }

        // Enqueue job
        var json = JsonSerializer.Serialize(message);
        await db.ListLeftPushAsync(QueueKey, json);
        
        // Store idempotency key with 7-day TTL
        await db.StringSetAsync(idempotencyKey, message.RunId.ToString(), TimeSpan.FromDays(7));

        // Get queue position
        var queueDepth = await db.ListLengthAsync(QueueKey);
        
        _logger.LogInformation("Enqueued job {RunId}, queue depth: {Depth}", message.RunId, queueDepth);

        return new EnqueueResult
        {
            Success = true,
            QueuePosition = (int)queueDepth,
            EstimatedStartTime = DateTime.UtcNow.AddMinutes(queueDepth * 5) // Assume 5 min per job
        };
    }

    public async Task<int> GetQueueDepthAsync()
    {
        var db = _redis.GetDatabase();
        return (int)await db.ListLengthAsync(QueueKey);
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

public class EnqueueResult
{
    public bool Success { get; set; }
    public bool IsDuplicate { get; set; }
    public Guid? ExistingRunId { get; set; }
    public int QueuePosition { get; set; }
    public DateTime? EstimatedStartTime { get; set; }
}
