using System.ComponentModel.DataAnnotations;

namespace AspireApp1.BacktestApi.Models;

public class BacktestSubmitRequest
{
    [Required]
    public Guid StrategyId { get; set; }

    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [StringLength(10, MinimumLength = 2)]
    public string Timeframe { get; set; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Required]
    public BacktestParameters Parameters { get; set; } = new();

    public decimal InitialCapital { get; set; } = 50000m;
}

public class BacktestParameters
{
    [Range(20, 500)]
    public int ZoneLookbackBars { get; set; } = 100;

    [Range(2, 10)]
    public int ZoneMinTouches { get; set; } = 2;

    [Range(0.1, 3.0)]
    public decimal ZoneWidthAtrMultiple { get; set; } = 0.5m;

    [Range(50, 2000)]
    public int ZoneMaxAgeBars { get; set; } = 500;

    public bool RequireConfirmation { get; set; } = true;

    [Range(0.5, 10.0)]
    public decimal StopLossAtrMultiple { get; set; } = 2.0m;

    [Range(0.5, 10.0)]
    public decimal TakeProfitRMultiple { get; set; } = 2.0m;

    [Range(0.1, 10.0)]
    public decimal RiskPerTradePct { get; set; } = 1.0m;

    [Range(1, 10)]
    public int MaxConcurrentTrades { get; set; } = 2;

    public List<string> SessionFilter { get; set; } = new() { "NY_AM" };

    [Range(0, 20)]
    public int LimitOrderOffsetTicks { get; set; } = 1;
}

public class BacktestSubmitResponse
{
    public Guid RunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int QueuePosition { get; set; }
    public DateTime? EstimatedStartTime { get; set; }
}

public class BacktestStatusResponse
{
    public Guid RunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public BacktestMetrics? Metrics { get; set; }
    public int QueuePosition { get; set; } = -1; // -1 = not queued, 0 = processing, >0 = position in queue
    public DateTime? EstimatedStartTime { get; set; }
}

public class BacktestMetrics
{
    public decimal WinRate { get; set; }
    public decimal AvgRMultiple { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal MaxDrawdownPct { get; set; }
    public decimal ProfitFactor { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal TotalPnl { get; set; }
    public decimal TotalPnlPct { get; set; }
    public decimal AvgWin { get; set; }
    public decimal AvgLoss { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossLoss { get; set; }
    public decimal AvgMae { get; set; }
    public decimal AvgMfe { get; set; }
    public int AvgHoldingBars { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }
}

public class BacktestListResponse
{
    public List<BacktestListItem> Data { get; set; } = new();
    public Pagination Pagination { get; set; } = new();
}

public class BacktestListItem
{
    public Guid RunId { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int Progress { get; set; }
    public decimal? FinalEquity { get; set; }
    public decimal? WinRate { get; set; }
    public int QueuePosition { get; set; } = -1; // -1 = not queued, 0 = processing, >0 = position
    public DateTime? EstimatedStartTime { get; set; }
}

public class Pagination
{
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public bool HasMore { get; set; }
}

public class EquityCurveResponse
{
    public Guid RunId { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalEquity { get; set; }
    public List<EquityPoint> DataPoints { get; set; } = new();
    public BacktestMetrics? Metrics { get; set; }
}

public class EquityPoint
{
    public DateTime Timestamp { get; set; }
    public decimal Equity { get; set; }
    public decimal DrawdownPct { get; set; }
}
