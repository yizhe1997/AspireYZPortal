using AspireApp1.Backtest.Data;
using AspireApp1.BacktestApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AspireApp1.BacktestApi.Services;

public class BacktestSubmissionService
{
    private readonly BacktestDbContext _db;
    private readonly ILogger<BacktestSubmissionService> _logger;

    public BacktestSubmissionService(BacktestDbContext db, ILogger<BacktestSubmissionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateSubmissionAsync(
        string symbol, 
        string timeframe, 
        DateOnly startDate, 
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        // Check date range
        if (endDate <= startDate)
        {
            return (false, "EndDate must be after StartDate");
        }

        var dateSpan = endDate.ToDateTime(TimeOnly.MinValue) - startDate.ToDateTime(TimeOnly.MinValue);
        if (dateSpan.TotalDays > 1095) // 3 years
        {
            return (false, "Date range cannot exceed 3 years");
        }

        // Check market data availability
        var hasData = await _db.MarketData
            .Where(m => m.Symbol == symbol && 
                       m.Timeframe == timeframe &&
                       m.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       m.Timestamp <= endDate.ToDateTime(TimeOnly.MinValue))
            .AnyAsync(cancellationToken);

        if (!hasData)
        {
            var available = await _db.MarketData
                .Where(m => m.Symbol == symbol && m.Timeframe == timeframe)
                .Select(m => m.Timestamp)
                .OrderBy(t => t)
                .Take(1)
                .FirstOrDefaultAsync(cancellationToken);

            var availableEnd = await _db.MarketData
                .Where(m => m.Symbol == symbol && m.Timeframe == timeframe)
                .Select(m => m.Timestamp)
                .OrderByDescending(t => t)
                .Take(1)
                .FirstOrDefaultAsync(cancellationToken);

            if (available == default)
            {
                return (false, $"No market data available for {symbol} {timeframe}");
            }

            return (false, $"No data for requested date range. Available: {DateOnly.FromDateTime(available)} to {DateOnly.FromDateTime(availableEnd)}");
        }

        return (true, null);
    }

    public string ComputeIdempotencyKey(BacktestSubmitRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            request.StrategyId,
            request.Symbol,
            request.Timeframe,
            request.StartDate,
            request.EndDate,
            request.Parameters
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<BacktestRun> CreateBacktestRunAsync(
        BacktestSubmitRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            StrategyId = request.StrategyId,
            Symbol = request.Symbol,
            Timeframe = request.Timeframe,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Parameters = JsonSerializer.Serialize(request.Parameters),
            InitialCapital = request.InitialCapital,
            Status = BacktestStatus.Queued,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        _db.BacktestRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created backtest run {RunId} for user {UserId}", run.Id, userId);

        return run;
    }
}
