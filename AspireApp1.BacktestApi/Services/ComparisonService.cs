using AspireApp1.Backtest.Data;
using Microsoft.EntityFrameworkCore;

namespace AspireApp1.BacktestApi.Services;

public class ComparisonService
{
    private readonly BacktestDbContext _db;
    private readonly ILogger<ComparisonService> _logger;
    private const int EquityCurveDownsampleTo = 100; // Downsample equity curves to 100 points

    public ComparisonService(BacktestDbContext db, ILogger<ComparisonService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Compare multiple backtest runs, returning metrics, parameters diff, and downsampled equity curves.
    /// </summary>
    public async Task<ComparisonResult?> CompareRunsAsync(Guid[] runIds, CancellationToken cancellationToken = default)
    {
        if (runIds.Length == 0)
        {
            return null;
        }

        // Get all runs
        var runs = await _db.BacktestRuns
            .Where(r => runIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (runs.Count == 0)
        {
            return null;
        }

        // Get metrics for each run
        var runMetrics = await _db.RunMetrics
            .Where(m => runIds.Contains(m.RunId))
            .ToListAsync(cancellationToken);

        var metricsByRun = runMetrics
            .GroupBy(m => m.RunId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(m => m.MetricName, m => m.MetricValue));

        // Get equity curves for each run and downsample
        var runEquityData = await _db.EquityCurve
            .Where(e => runIds.Contains(e.RunId))
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        var equityByRun = runEquityData
            .GroupBy(e => e.RunId)
            .ToDictionary(g => g.Key, g => DownsampleEquityCurve(g.ToList()));

        // Build comparison data
        var runComparisons = runs.Select(r =>
        {
            metricsByRun.TryGetValue(r.Id, out var metrics);
            equityByRun.TryGetValue(r.Id, out var equityPoints);

            return new RunComparison
            {
                RunId = r.Id,
                Symbol = r.Symbol,
                Timeframe = r.Timeframe,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                Status = r.Status.ToString().ToLowerInvariant(),
                CreatedAt = r.CreatedAt,
                Parameters = r.Parameters,
                Metrics = metrics,
                EquityPoints = equityPoints ?? new List<EquityPointComparison>()
            };
        }).ToList();

        // Get parameter keys from all runs
        var parameterDiff = ExtractParameterDiff(runComparisons);

        return new ComparisonResult
        {
            Runs = runComparisons,
            ParameterDiff = parameterDiff,
            Count = runComparisons.Count
        };
    }

    /// <summary>
    /// Downsample equity curve to a maximum of EquityCurveDownsampleTo points.
    /// </summary>
    private List<EquityPointComparison> DownsampleEquityCurve(List<EquityCurvePoint> points)
    {
        if (points.Count <= EquityCurveDownsampleTo)
        {
            return points.Select(p => new EquityPointComparison
            {
                Timestamp = p.Timestamp,
                Equity = p.Equity
            }).ToList();
        }

        var result = new List<EquityPointComparison>();
        var step = (double)points.Count / EquityCurveDownsampleTo;

        for (int i = 0; i < EquityCurveDownsampleTo; i++)
        {
            var index = (int)(i * step);
            if (index >= points.Count) index = points.Count - 1;

            var point = points[index];
            result.Add(new EquityPointComparison
            {
                Timestamp = point.Timestamp,
                Equity = point.Equity
            });
        }

        // Always include the last point
        if (points.Count > 0 && result.Last().Timestamp != points.Last().Timestamp)
        {
            result.Add(new EquityPointComparison
            {
                Timestamp = points.Last().Timestamp,
                Equity = points.Last().Equity
            });
        }

        return result;
    }

    /// <summary>
    /// Extract parameter differences across runs.
    /// </summary>
    private Dictionary<string, List<object>> ExtractParameterDiff(List<RunComparison> runs)
    {
        var diff = new Dictionary<string, List<object>>();

        if (runs.Count == 0)
        {
            return diff;
        }

        foreach (var run in runs)
        {
            if (string.IsNullOrEmpty(run.Parameters))
            {
                continue;
            }

            // Parse JSON parameters
            var json = System.Text.Json.JsonDocument.Parse(run.Parameters).RootElement;

            foreach (var prop in json.EnumerateObject())
            {
                if (!diff.ContainsKey(prop.Name))
                {
                    diff[prop.Name] = new List<object>();
                }

                var value = prop.Value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Number => (object)prop.Value.GetDecimal(),
                    System.Text.Json.JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    System.Text.Json.JsonValueKind.True => true,
                    System.Text.Json.JsonValueKind.False => false,
                    _ => prop.Value.GetRawText()
                };

                diff[prop.Name].Add(value);
            }
        }

        return diff;
    }
}

public class ComparisonResult
{
    public List<RunComparison> Runs { get; set; } = new();
    public Dictionary<string, List<object>> ParameterDiff { get; set; } = new();
    public int Count { get; set; }
}

public class RunComparison
{
    public Guid RunId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Parameters { get; set; } = "{}";
    public Dictionary<string, decimal>? Metrics { get; set; }
    public List<EquityPointComparison> EquityPoints { get; set; } = new();
}

public class EquityPointComparison
{
    public DateTime Timestamp { get; set; }
    public decimal Equity { get; set; }
}
