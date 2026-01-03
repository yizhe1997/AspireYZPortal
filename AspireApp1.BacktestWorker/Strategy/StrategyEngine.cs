using AspireApp1.Backtest.Data;
using Microsoft.EntityFrameworkCore;

namespace AspireApp1.BacktestWorker.Strategy;

public class StrategyParameters
{
    public int ZoneLookbackBars { get; set; }
    public int MinZoneTouches { get; set; }
    public decimal ZoneWidthAtrMultiple { get; set; }
    public int MaxZoneAgeBars { get; set; }
    public decimal StopLossAtrMultiple { get; set; }
    public decimal TakeProfitRMultiple { get; set; }
    public decimal RiskPerTradePct { get; set; }
    public int MaxConcurrentTrades { get; set; }
    public int LimitOrderOffsetTicks { get; set; }
    public bool IncludeAsianSession { get; set; }
    public bool IncludeLondonSession { get; set; }
    public bool IncludeNewYorkSession { get; set; }
}

public class StrategyEngine
{
    private readonly BacktestDbContext _dbContext;
    private readonly StrategyParameters _params;
    private readonly decimal _initialCapital;

    private readonly ZoneDetector _zoneDetector;
    private readonly OrderManager _orderManager;
    private readonly ExitManager _exitManager;
    private readonly SessionFilter _sessionFilter;

    public StrategyEngine(
        BacktestDbContext dbContext,
        StrategyParameters parameters,
        decimal initialCapital)
    {
        _dbContext = dbContext;
        _params = parameters;
        _initialCapital = initialCapital;

        _zoneDetector = new ZoneDetector(
            parameters.ZoneLookbackBars,
            parameters.MinZoneTouches,
            parameters.ZoneWidthAtrMultiple,
            parameters.MaxZoneAgeBars);

        _orderManager = new OrderManager(
            parameters.StopLossAtrMultiple,
            parameters.TakeProfitRMultiple,
            parameters.RiskPerTradePct,
            parameters.MaxConcurrentTrades,
            parameters.LimitOrderOffsetTicks);

        _exitManager = new ExitManager();

        _sessionFilter = new SessionFilter(
            parameters.IncludeAsianSession,
            parameters.IncludeLondonSession,
            parameters.IncludeNewYorkSession);
    }

    public async Task<(List<Trade> Trades, BacktestMetrics Metrics, List<EquityPoint> EquityCurve, List<Zone> Zones)> 
        ExecuteBacktest(
                string symbol,
                string timeframe,
            DateTime startDate,
            DateTime endDate,
            IProgress<int>? progress = null)
    {
        // Load market data
        var marketData = await _dbContext.MarketData
                .Where(md => md.Symbol == symbol
                    && md.Timeframe == timeframe
                    && md.Timestamp >= startDate 
                && md.Timestamp <= endDate)
            .OrderBy(md => md.Timestamp)
            .ToListAsync();

        if (marketData.Count == 0)
        {
            throw new InvalidOperationException("No market data found for specified date range");
        }

        // Convert to BarData
        var bars = marketData.Select(md => new BarData
        {
            Timestamp = md.Timestamp,
            Open = md.Open,
            High = md.High,
            Low = md.Low,
            Close = md.Close,
            Volume = md.Volume
        }).ToList();

        // Calculate ATR series
        var highs = bars.Select(b => b.High).ToList();
        var lows = bars.Select(b => b.Low).ToList();
        var closes = bars.Select(b => b.Close).ToList();
        var atrSeries = IndicatorCalculator.CalculateATRSeries(highs, lows, closes, 14);

        // Initialize state
        var completedTrades = new List<Trade>();
        var activePositions = new List<Position>();
        var allDetectedZones = new List<Zone>();
        var currentEquity = _initialCapital;

        var totalBars = bars.Count;

        // Main backtest loop
        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var atr = atrSeries[i];

            if (atr == 0) continue; // Skip if ATR not ready

            // Report progress every 5% or every 100 bars
            if (progress != null && (i % Math.Max(1, totalBars / 20) == 0 || i % 100 == 0))
            {
                var progressPct = (int)((i / (double)totalBars) * 100);
                progress.Report(progressPct);
            }

            // Check exits for active positions
            var positionsToClose = new List<(Position Position, ExitReason Reason, decimal ExitPrice)>();
            
            foreach (var position in activePositions)
            {
                _orderManager.UpdatePositionExtremes(position, bar);
                
                var barsHeld = i - bars.FindIndex(b => b.Timestamp == position.EntryTime);
                var (shouldExit, reason, exitPrice) = _exitManager.CheckExit(position, bar, barsHeld);

                if (shouldExit && reason.HasValue)
                {
                    positionsToClose.Add((position, reason.Value, exitPrice));
                }
            }

            // Close positions
            foreach (var (position, reason, exitPrice) in positionsToClose)
            {
                var trade = _exitManager.ClosePosition(position, bar, reason, exitPrice);
                completedTrades.Add(trade);
                activePositions.Remove(position);
                currentEquity += trade.Pnl;
            }

            // Detect zones
            if (i >= _params.ZoneLookbackBars)
            {
                var detectedZones = _zoneDetector.DetectZones(bars, i, atr);
                
                foreach (var zone in detectedZones)
                {
                    // Check if similar zone already exists
                    var existingZone = allDetectedZones.FirstOrDefault(z =>
                        z.Type == zone.Type &&
                        Math.Abs(z.High - zone.High) < atr * 0.1m &&
                        Math.Abs(z.Low - zone.Low) < atr * 0.1m);

                    if (existingZone == null)
                    {
                        allDetectedZones.Add(zone);
                    }
                }
            }

            // Filter active zones
            var activeZones = _zoneDetector.FilterActiveZones(allDetectedZones, i);

            // Check for entry signals (only during active sessions)
            if (_sessionFilter.IsInActiveSession(bar.Timestamp) && 
                _orderManager.CanEnterTrade(activePositions))
            {
                // Check demand zones for long entries
                foreach (var zone in activeZones.Where(z => z.Type == ZoneType.Demand))
                {
                    var position = _orderManager.TryEnterLong(bar, zone, atr, currentEquity);
                    if (position != null)
                    {
                        activePositions.Add(position);
                        
                        if (!_orderManager.CanEnterTrade(activePositions))
                        {
                            break; // Max positions reached
                        }
                    }
                }

                // Check supply zones for short entries
                foreach (var zone in activeZones.Where(z => z.Type == ZoneType.Supply))
                {
                    if (!_orderManager.CanEnterTrade(activePositions))
                    {
                        break; // Max positions reached
                    }

                    var position = _orderManager.TryEnterShort(bar, zone, atr, currentEquity);
                    if (position != null)
                    {
                        activePositions.Add(position);
                    }
                }
            }
        }

        // Close any remaining open positions at end
        foreach (var position in activePositions)
        {
            var lastBar = bars[^1];
            var trade = _exitManager.ClosePosition(position, lastBar, ExitReason.TimeLimit, lastBar.Close);
            completedTrades.Add(trade);
            currentEquity += trade.Pnl;
        }

        // Calculate final metrics
        var equityCurve = MetricsCalculator.BuildEquityCurve(completedTrades, _initialCapital);
        var metrics = MetricsCalculator.Calculate(completedTrades, equityCurve, _initialCapital);

        progress?.Report(100);

        return (completedTrades, metrics, equityCurve, allDetectedZones);
    }
}
