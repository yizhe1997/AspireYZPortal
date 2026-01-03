using AspireApp1.Backtest.Data;
using AspireApp1.BacktestApi.Models;
using AspireApp1.BacktestApi.Services;
using AspireApp1.BacktestApi.Queue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Text.Json;

namespace AspireApp1.BacktestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BacktestsController : ControllerBase
{
    private readonly BacktestDbContext _db;
    private readonly BacktestSubmissionService _submissionService;
    private readonly ResultsService _resultsService;
    private readonly RedisQueueProducer _queueProducer;
    private readonly QueueService _queueService;
    private readonly ILogger<BacktestsController> _logger;

    public BacktestsController(
        BacktestDbContext db,
        BacktestSubmissionService submissionService,
        ResultsService resultsService,
        RedisQueueProducer queueProducer,
        QueueService queueService,
        ILogger<BacktestsController> logger)
    {
        _db = db;
        _submissionService = submissionService;
        _resultsService = resultsService;
        _queueProducer = queueProducer;
        _queueService = queueService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitBacktest([FromBody] BacktestSubmitRequest request, CancellationToken cancellationToken)
    {
        // Validate request
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate date range and data availability
        var (isValid, errorMessage) = await _submissionService.ValidateSubmissionAsync(
            request.Symbol, 
            request.Timeframe, 
            request.StartDate, 
            request.EndDate, 
            cancellationToken);

        if (!isValid)
        {
            return BadRequest(new { error = errorMessage });
        }

        // Compute idempotency key
        var idempotencyKey = _submissionService.ComputeIdempotencyKey(request);

        // TODO: Extract userId from API key context
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Hardcoded for MVP

        // Create backtest run
        var run = await _submissionService.CreateBacktestRunAsync(request, userId, cancellationToken);

        // Enqueue job
        var jobMessage = new BacktestJobMessage
        {
            RunId = run.Id,
            StrategyId = request.StrategyId,
            Symbol = request.Symbol,
            Timeframe = request.Timeframe,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Parameters = JsonSerializer.Serialize(request.Parameters),
            UserId = userId,
            InitialCapital = request.InitialCapital,
            IdempotencyKey = idempotencyKey
        };

        var enqueueResult = await _queueProducer.EnqueueBacktestAsync(jobMessage, cancellationToken);

        if (enqueueResult.IsDuplicate)
        {
            return Ok(new BacktestSubmitResponse
            {
                RunId = enqueueResult.ExistingRunId!.Value,
                Status = "queued",
                QueuePosition = 0,
                EstimatedStartTime = null
            });
        }

        return Accepted(new BacktestSubmitResponse
        {
            RunId = run.Id,
            Status = run.Status.ToString().ToLowerInvariant(),
            QueuePosition = enqueueResult.QueuePosition,
            EstimatedStartTime = enqueueResult.EstimatedStartTime
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBacktestStatus(Guid id, CancellationToken cancellationToken)
    {
        var run = await _db.BacktestRuns.FindAsync(new object[] { id }, cancellationToken);

        if (run == null)
        {
            return NotFound(new { error = "Backtest run not found" });
        }

        var response = new BacktestStatusResponse
        {
            RunId = run.Id,
            Status = run.Status.ToString().ToLowerInvariant(),
            Progress = run.Progress,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            ErrorMessage = run.ErrorMessage
        };

        // Add queue position for queued or running runs
        if (run.Status == BacktestStatus.Queued || run.Status == BacktestStatus.Running)
        {
            var (queuePos, estimatedStart) = await _queueService.GetQueuePositionAsync(run.Id);
            response.QueuePosition = queuePos;
            response.EstimatedStartTime = estimatedStart;
        }

        // Load metrics if completed
        if (run.Status == BacktestStatus.Completed)
        {
            var metrics = await _db.RunMetrics
                .Where(m => m.RunId == run.Id)
                .ToDictionaryAsync(m => m.MetricName, m => m.MetricValue, cancellationToken);

            response.Metrics = MapMetrics(metrics);
        }

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> ListBacktests(
        [FromQuery] string? status = null,
        [FromQuery] Guid? strategyId = null,
        [FromQuery] string? symbol = null,
        [FromQuery] string? timeframe = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        // TODO: Filter by userId from API key context
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);

        var query = _db.BacktestRuns.Where(r => r.UserId == userId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<BacktestStatus>(status, true, out var statusEnum))
        {
            query = query.Where(r => r.Status == statusEnum);
        }

        if (strategyId.HasValue)
        {
            query = query.Where(r => r.StrategyId == strategyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(symbol))
        {
            query = query.Where(r => r.Symbol == symbol);
        }

        if (!string.IsNullOrWhiteSpace(timeframe))
        {
            query = query.Where(r => r.Timeframe == timeframe);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var runs = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var strategyIds = runs.Select(r => r.StrategyId).Distinct().ToList();
        var strategies = await _db.Strategies
            .Where(s => strategyIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var runIds = runs.Select(r => r.Id).ToList();
        var metricsByRun = await _db.RunMetrics
            .Where(m => runIds.Contains(m.RunId) && (m.MetricName == "win_rate" || m.MetricName == "total_pnl"))
            .GroupBy(m => m.RunId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.ToDictionary(m => m.MetricName, m => m.MetricValue),
                cancellationToken);

        // Get queue positions for all queued/running runs
        var queueInfoByRunId = new Dictionary<Guid, (int Position, DateTime? EstimatedStart)>();
        foreach (var runId in runIds.Where(id => runs.Any(r => r.Id == id && (r.Status == BacktestStatus.Queued || r.Status == BacktestStatus.Running))))
        {
            var queueInfo = await _queueService.GetQueuePositionAsync(runId);
            queueInfoByRunId[runId] = queueInfo;
        }

        var items = runs.Select(r =>
        {
            metricsByRun.TryGetValue(r.Id, out var runMetrics);

            decimal? finalEquity = null;
            decimal? winRate = null;

            if (r.Status == BacktestStatus.Completed && runMetrics != null)
            {
                winRate = runMetrics.GetValueOrDefault("win_rate");
                finalEquity = r.InitialCapital + runMetrics.GetValueOrDefault("total_pnl");
            }

            var item = new BacktestListItem
            {
                RunId = r.Id,
                StrategyName = strategies.GetValueOrDefault(r.StrategyId) ?? string.Empty,
                Symbol = r.Symbol,
                Timeframe = r.Timeframe,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                Status = r.Status.ToString().ToLowerInvariant(),
                CreatedAt = r.CreatedAt,
                FinishedAt = r.FinishedAt,
                Progress = r.Progress,
                FinalEquity = finalEquity,
                WinRate = winRate
            };

            // Add queue position if applicable
            if (queueInfoByRunId.TryGetValue(r.Id, out var queueInfo))
            {
                item.QueuePosition = queueInfo.Position;
                item.EstimatedStartTime = queueInfo.EstimatedStart;
            }

            return item;
        }).ToList();

        return Ok(new BacktestListResponse
        {
            Data = items,
            Pagination = new Pagination
            {
                Total = totalCount,
                Limit = limit,
                Offset = offset,
                HasMore = offset + limit < totalCount
            }
        });
    }

    [HttpGet("{id:guid}/equity")]
    public async Task<IActionResult> GetEquity(Guid id, CancellationToken cancellationToken)
    {
        var run = await _db.BacktestRuns.FindAsync(new object[] { id }, cancellationToken);

        if (run == null)
        {
            return NotFound(new { error = "Backtest run not found" });
        }

        if (run.Status != BacktestStatus.Completed)
        {
            return Conflict(new { error = "Backtest run not yet completed" });
        }

        var equityPoints = await _db.EquityCurve
            .Where(e => e.RunId == id)
            .OrderBy(e => e.Timestamp)
            .Select(e => new EquityPoint
            {
                Timestamp = e.Timestamp,
                Equity = e.Equity,
                DrawdownPct = e.DrawdownPct
            })
            .ToListAsync(cancellationToken);

        var metrics = await _db.RunMetrics
            .Where(m => m.RunId == id)
            .ToDictionaryAsync(m => m.MetricName, m => m.MetricValue, cancellationToken);

        var response = new EquityCurveResponse
        {
            RunId = run.Id,
            InitialCapital = run.InitialCapital,
            FinalEquity = equityPoints.Count > 0 ? equityPoints[^1].Equity : run.InitialCapital,
            DataPoints = equityPoints,
            Metrics = metrics.Count == 0 ? null : MapMetrics(metrics)
        };

        return Ok(response);
    }

    private static BacktestMetrics MapMetrics(Dictionary<string, decimal> metrics)
    {
        var totalTrades = ToInt(metrics.GetValueOrDefault("total_trades"));
        var winningTrades = ToInt(metrics.GetValueOrDefault("winning_trades"));
        var losingTrades = ToInt(metrics.GetValueOrDefault("losing_trades"));
        var avgWin = metrics.GetValueOrDefault("avg_win");
        var avgLoss = metrics.GetValueOrDefault("avg_loss");

        var grossProfit = avgWin * winningTrades;
        var grossLoss = Math.Abs(avgLoss * losingTrades);

        return new BacktestMetrics
        {
            WinRate = metrics.GetValueOrDefault("win_rate"),
            AvgRMultiple = metrics.GetValueOrDefault("avg_r_multiple"),
            SharpeRatio = metrics.GetValueOrDefault("sharpe_ratio"),
            MaxDrawdown = metrics.GetValueOrDefault("max_drawdown"),
            MaxDrawdownPct = metrics.GetValueOrDefault("max_drawdown_pct"),
            ProfitFactor = metrics.GetValueOrDefault("profit_factor"),
            TotalTrades = totalTrades,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            TotalPnl = metrics.GetValueOrDefault("total_pnl"),
            TotalPnlPct = metrics.GetValueOrDefault("total_pnl_pct"),
            AvgWin = avgWin,
            AvgLoss = avgLoss,
            GrossProfit = grossProfit,
            GrossLoss = grossLoss,
            AvgMae = metrics.GetValueOrDefault("avg_mae"),
            AvgMfe = metrics.GetValueOrDefault("avg_mfe"),
            AvgHoldingBars = ToInt(metrics.GetValueOrDefault("avg_holding_bars")),
            LargestWin = metrics.GetValueOrDefault("largest_win"),
            LargestLoss = metrics.GetValueOrDefault("largest_loss")
        };
    }

    private static int ToInt(decimal value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    [HttpGet("{id:guid}/trades")]
    public async Task<IActionResult> GetTrades(
        Guid id,
        [FromQuery] string? side = null,
        [FromQuery] string? zoneType = null,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        [FromQuery] bool? export = false,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.BacktestRuns.FindAsync(new object[] { id }, cancellationToken);

        if (run == null)
        {
            return NotFound(new { error = "Backtest run not found" });
        }

        if (run.Status != BacktestStatus.Completed)
        {
            return Conflict(new { error = "Backtest run not yet completed" });
        }

        // Export as CSV if requested
        if (export.HasValue && export.Value)
        {
            var csv = await _resultsService.ExportTradesAsCsvAsync(id, cancellationToken);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"trades_{id}.csv");
        }

        // Return paginated trades
        var result = await _resultsService.GetTradesAsync(id, side, zoneType, limit, offset, cancellationToken);

        return Ok(new
        {
            run_id = id,
            trades = result.Trades,
            pagination = new
            {
                total = result.Total,
                limit = result.Limit,
                offset = result.Offset,
                has_more = result.HasMore
            }
        });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelBacktest(Guid id, CancellationToken cancellationToken)
    {
        var run = await _db.BacktestRuns.FindAsync(new object[] { id }, cancellationToken);

        if (run == null)
        {
            return NotFound(new { error = "Backtest run not found" });
        }

        // Only allow cancelling queued or running jobs
        if (run.Status != BacktestStatus.Queued && run.Status != BacktestStatus.Running)
        {
            return Conflict(new { error = $"Cannot cancel backtest with status {run.Status}" });
        }

        // Mark as cancelled in Redis
        var cancelled = await _queueService.CancelRunAsync(id);

        if (!cancelled)
        {
            return StatusCode(500, new { error = "Failed to cancel backtest" });
        }

        // Update database status
        run.Status = BacktestStatus.Cancelled;
        run.FinishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Backtest {RunId} cancelled", id);

        return Ok(new { status = "cancelled", run_id = id });
    }

    [HttpGet("queue/depth")]
    public async Task<IActionResult> GetQueueDepth()
    {
        var depth = await _queueService.GetQueueDepthAsync();
        return Ok(new { queue_depth = depth });
    }
}