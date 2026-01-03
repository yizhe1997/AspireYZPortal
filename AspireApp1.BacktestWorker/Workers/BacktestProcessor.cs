using AspireApp1.Backtest.Data;
using AspireApp1.BacktestWorker.Queue;
using AspireApp1.BacktestWorker.Strategy;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DataTrade = AspireApp1.Backtest.Data.Trade;
using StrategyTrade = AspireApp1.BacktestWorker.Strategy.Trade;
using DataZoneType = AspireApp1.Backtest.Data.ZoneType;
using StrategyZoneType = AspireApp1.BacktestWorker.Strategy.ZoneType;

namespace AspireApp1.BacktestWorker.Workers;

public class BacktestProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BacktestProcessor> _logger;
    private const int MaxRetries = 3;

    public BacktestProcessor(IServiceProvider serviceProvider, ILogger<BacktestProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backtest processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var consumer = scope.ServiceProvider.GetRequiredService<RedisQueueConsumer>();
                var db = scope.ServiceProvider.GetRequiredService<BacktestDbContext>();

                // Dequeue job (blocks for up to 5 seconds)
                var job = await consumer.DequeueAsync(TimeSpan.FromSeconds(5), stoppingToken);

                if (job == null)
                {
                    continue;
                }

                _logger.LogInformation("Processing job {RunId}", job.RunId);

                // Get retry count
                var retryCount = await consumer.GetRetryCountAsync(job.RunId);

                try
                {
                    // Update status to running
                    var run = await db.BacktestRuns.FindAsync(new object[] { job.RunId }, stoppingToken);
                    if (run == null)
                    {
                        _logger.LogWarning("Run {RunId} not found in database", job.RunId);
                        continue;
                    }

                    run.Status = BacktestStatus.Running;
                    run.StartedAt = DateTime.UtcNow;
                    run.WorkerId = Environment.MachineName;
                    run.Progress = 0;
                    await db.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Executing backtest {RunId}", job.RunId);

                        // Deserialize parameters from JSON string
                        var paramsJson = JsonSerializer.Deserialize<BacktestParametersDto>(job.Parameters);
                        if (paramsJson == null)
                        {
                            throw new InvalidOperationException("Failed to deserialize backtest parameters");
                        }

                        var parameters = new StrategyParameters
                        {
                            ZoneLookbackBars = paramsJson.zone_lookback_bars,
                            MinZoneTouches = paramsJson.min_zone_touches,
                            ZoneWidthAtrMultiple = paramsJson.zone_width_atr_multiple,
                            MaxZoneAgeBars = paramsJson.max_zone_age_bars,
                            StopLossAtrMultiple = paramsJson.stoploss_atr_multiple,
                            TakeProfitRMultiple = paramsJson.takeprofit_r_multiple,
                            RiskPerTradePct = paramsJson.risk_per_trade_pct,
                            MaxConcurrentTrades = paramsJson.max_concurrent_trades,
                            LimitOrderOffsetTicks = paramsJson.limit_order_offset_ticks,
                            IncludeAsianSession = paramsJson.include_asian_session,
                            IncludeLondonSession = paramsJson.include_london_session,
                            IncludeNewYorkSession = paramsJson.include_newyork_session
                        };

                        // Convert DateOnly to DateTime
                        var startDateTime = job.StartDate.ToDateTime(TimeOnly.MinValue);
                        var endDateTime = job.EndDate.ToDateTime(TimeOnly.MaxValue);

                        // Execute strategy engine with progress reporting
                        var engine = new StrategyEngine(db, parameters, job.InitialCapital);

                        var lastProgressSaved = -1;
                        var lastSaveAt = DateTime.UtcNow;
                        var progress = new Progress<int>(pct =>
                        {
                            // Throttle DB writes: save on first update, on +5% deltas, or every 10 seconds
                            var shouldSave = pct == 100 || pct - lastProgressSaved >= 5 || (DateTime.UtcNow - lastSaveAt) >= TimeSpan.FromSeconds(10);
                            if (!shouldSave)
                            {
                                return;
                            }

                            run.Progress = pct;
                            db.SaveChanges();
                            lastProgressSaved = pct;
                            lastSaveAt = DateTime.UtcNow;
                        });

                        var (trades, metrics, equityCurve, zones) = await engine.ExecuteBacktest(
                            job.Symbol,
                            job.Timeframe,
                            startDateTime,
                            endDateTime,
                            progress);

                        // Persist results
                        _logger.LogInformation("Persisting {TradeCount} trades and {ZoneCount} zones", 
                            trades.Count, zones.Count);

                        // Save trades
                        foreach (var trade in trades)
                        {
                                db.Trades.Add(new DataTrade
                            {
                                Id = Guid.NewGuid(),
                                RunId = run.Id,
                                Symbol = job.Symbol,
                                EntryTime = trade.EntryTime,
                                ExitTime = trade.ExitTime,
                                        Side = trade.Side == Strategy.TradeSide.Long ? Backtest.Data.TradeSide.Long : Backtest.Data.TradeSide.Short,
                                EntryPrice = trade.EntryPrice,
                                ExitPrice = trade.ExitPrice,
                                Quantity = trade.Quantity,
                                Pnl = trade.Pnl,
                                PnlPct = trade.PnlPct,
                                RMultiple = trade.RMultiple,
                                Mae = trade.Mae,
                                Mfe = trade.Mfe,
                                HoldingBars = trade.HoldingBars,
                                ZoneType = trade.EntryZone?.Type.ToString(),
                                ZoneStrength = trade.EntryZone?.Strength
                            });
                        }

                        // Save detected zones
                        foreach (var zone in zones)
                        {
                            db.DetectedZones.Add(new DetectedZone
                            {
                                Id = Guid.NewGuid(),
                                RunId = run.Id,
                                Timestamp = zone.Timestamp,
                                    ZoneType = zone.Type == StrategyZoneType.Supply ? DataZoneType.Supply : DataZoneType.Demand,
                                High = zone.High,
                                Low = zone.Low,
                                Strength = zone.Strength,
                                Touches = zone.Touches,
                                CreatedAt = DateTime.UtcNow
                            });
                        }

                        // Save metrics
                        var metricsToStore = new Dictionary<string, decimal>
                        {
                            { "total_trades", metrics.TotalTrades },
                            { "winning_trades", metrics.WinningTrades },
                            { "losing_trades", metrics.LosingTrades },
                            { "win_rate", metrics.WinRate },
                            { "total_pnl", metrics.TotalPnl },
                            { "total_pnl_pct", metrics.TotalPnlPct },
                            { "avg_win", metrics.AvgWin },
                            { "avg_loss", metrics.AvgLoss },
                            { "profit_factor", metrics.ProfitFactor },
                            { "avg_r_multiple", metrics.AvgRMultiple },
                            { "max_drawdown", metrics.MaxDrawdown },
                            { "max_drawdown_pct", metrics.MaxDrawdownPct },
                            { "sharpe_ratio", metrics.SharpeRatio },
                            { "avg_mae", metrics.AvgMae },
                            { "avg_mfe", metrics.AvgMfe },
                            { "avg_holding_bars", metrics.AvgHoldingBars },
                            { "largest_win", metrics.LargestWin },
                            { "largest_loss", metrics.LargestLoss }
                        };

                        foreach (var (metricName, metricValue) in metricsToStore)
                        {
                            db.RunMetrics.Add(new RunMetric
                            {
                                RunId = run.Id,
                                MetricName = metricName,
                                MetricValue = metricValue
                            });
                        }

                        // Save equity curve
                        foreach (var point in equityCurve)
                        {
                            db.EquityCurve.Add(new EquityCurvePoint
                            {
                                RunId = run.Id,
                                Timestamp = point.Timestamp,
                                Equity = point.Equity,
                                DrawdownPct = point.DrawdownPct
                            });
                        }

                        // Mark completed
                    run.Status = BacktestStatus.Completed;
                    run.FinishedAt = DateTime.UtcNow;
                    run.Progress = 100;
                    await db.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Completed backtest {RunId}", job.RunId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing job {RunId}", job.RunId);

                    // Update run status
                    var run = await db.BacktestRuns.FindAsync(new object[] { job.RunId }, stoppingToken);
                    if (run != null)
                    {
                        if (retryCount >= MaxRetries)
                        {
                            run.Status = BacktestStatus.Failed;
                            run.ErrorMessage = ex.Message;
                            run.FinishedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);

                            await consumer.MoveToDeadLetterQueueAsync(job, ex.Message);
                        }
                        else
                        {
                            // Increment retry and re-enqueue
                            await consumer.IncrementRetryCountAsync(job.RunId);
                            _logger.LogWarning("Retry {Count}/{Max} for job {RunId}", retryCount + 1, MaxRetries, job.RunId);
                            
                            // Re-queue logic would go here (simplified for now)
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in backtest processor loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Backtest processor stopped");
    }
}
