namespace AspireApp1.Backtest.Data;

public enum BacktestStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public class BacktestRun
{
    public Guid Id { get; set; }
    public Guid StrategyId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Parameters { get; set; } = "{}";
    public BacktestStatus Status { get; set; }
    public int Progress { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? WorkerId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UserId { get; set; }
    public decimal InitialCapital { get; set; }
}
