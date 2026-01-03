using AspireApp1.Backtest.Data;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AspireApp1.BacktestApi.Services;

public class MarketDataUploadResult
{
    public int RowsInserted { get; set; }
    public int RowsSkipped { get; set; }
    public List<string> Gaps { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class MarketDataSummary
{
    public string Symbol { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int BarCount { get; set; }
}

public class MarketDataService
{
    private readonly BacktestDbContext _db;
    private readonly ILogger<MarketDataService> _logger;

    public MarketDataService(BacktestDbContext db, ILogger<MarketDataService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<MarketDataUploadResult> UploadAsync(
        Stream csvStream,
        string symbol,
        string timeframe,
        CancellationToken cancellationToken)
    {
        var result = new MarketDataUploadResult();

        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(symbol) || symbol.Length > 20)
            {
                result.Errors.Add("Symbol must be 1-20 characters");
                return result;
            }

            if (!new[] { "1h", "4h", "1d" }.Contains(timeframe))
            {
                result.Errors.Add("Timeframe must be 1h, 4h, or 1d");
                return result;
            }

            symbol = symbol.ToUpperInvariant();

            // Parse CSV
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            };

            var rowsToInsert = new List<MarketData>();
            var seenTimestamps = new HashSet<DateTime>();
            var rowCount = 0;

            using (var reader = new StreamReader(csvStream))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                csv.Context.RegisterClassMap<MarketDataMap>();

                try
                {
                    await csv.ReadAsync();
                    csv.ReadHeader();

                    while (await csv.ReadAsync())
                    {
                        rowCount++;

                        // Enforce row limit
                        if (rowCount > 100_000)
                        {
                            result.Errors.Add("CSV exceeds 100,000 row limit");
                            return result;
                        }

                        try
                        {
                            var timestamp = csv.GetField<DateTime>("timestamp");
                            var open = csv.GetField<decimal>("open");
                            var high = csv.GetField<decimal>("high");
                            var low = csv.GetField<decimal>("low");
                            var close = csv.GetField<decimal>("close");
                            var volume = csv.GetField<decimal>("volume");

                            // Validation
                            if (high < low || high < open || high < close || low > open || low > close)
                            {
                                result.Errors.Add($"Row {rowCount}: Invalid OHLC relationship");
                                continue;
                            }

                            if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                            {
                                result.Errors.Add($"Row {rowCount}: All prices must be > 0");
                                continue;
                            }

                            if (volume < 0)
                            {
                                result.Errors.Add($"Row {rowCount}: Volume cannot be negative");
                                continue;
                            }

                            // Check for duplicates
                            if (seenTimestamps.Contains(timestamp))
                            {
                                result.RowsSkipped++;
                                continue;
                            }

                            seenTimestamps.Add(timestamp);

                            rowsToInsert.Add(new MarketData
                            {
                                Symbol = symbol,
                                Timeframe = timeframe,
                                Timestamp = timestamp,
                                Open = open,
                                High = high,
                                Low = low,
                                Close = close,
                                Volume = volume
                            });
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Row {rowCount}: {ex.Message}");
                        }

                        // Batch insert every 1000 rows
                        if (rowsToInsert.Count >= 1000)
                        {
                            await BatchInsertAsync(rowsToInsert, symbol, timeframe, cancellationToken);
                            result.RowsInserted += rowsToInsert.Count;
                            rowsToInsert.Clear();
                        }
                    }
                }
                catch (HeaderValidationException ex)
                {
                    result.Errors.Add($"CSV header error: {ex.Message}");
                    return result;
                }
            }

            // Insert remaining rows
            if (rowsToInsert.Count > 0)
            {
                await BatchInsertAsync(rowsToInsert, symbol, timeframe, cancellationToken);
                result.RowsInserted += rowsToInsert.Count;
            }

            // Detect gaps
            result.Gaps = await DetectGapsAsync(symbol, timeframe, cancellationToken);

            _logger.LogInformation(
                "Uploaded {Inserted} rows to {Symbol}/{Timeframe}, skipped {Skipped} duplicates",
                result.RowsInserted, symbol, timeframe, result.RowsSkipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading market data");
            result.Errors.Add($"Upload failed: {ex.Message}");
        }

        return result;
    }

    public async Task<List<MarketDataSummary>> GetAvailableAsync(
        string? symbol = null,
        string? timeframe = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.MarketData.AsQueryable();

        if (!string.IsNullOrWhiteSpace(symbol))
        {
            symbol = symbol.ToUpperInvariant();
            query = query.Where(m => m.Symbol == symbol);
        }

        if (!string.IsNullOrWhiteSpace(timeframe))
        {
            query = query.Where(m => m.Timeframe == timeframe);
        }

        var summaries = await query
            .GroupBy(m => new { m.Symbol, m.Timeframe })
            .Select(g => new MarketDataSummary
            {
                Symbol = g.Key.Symbol,
                Timeframe = g.Key.Timeframe,
                StartDate = DateOnly.FromDateTime(g.Min(m => m.Timestamp)),
                EndDate = DateOnly.FromDateTime(g.Max(m => m.Timestamp)),
                BarCount = g.Count()
            })
            .OrderBy(s => s.Symbol)
            .ThenBy(s => s.Timeframe)
            .ToListAsync(cancellationToken);

        return summaries;
    }

    private async Task BatchInsertAsync(
        List<MarketData> rows,
        string symbol,
        string timeframe,
        CancellationToken cancellationToken)
    {
        // Use bulk insert or batch inserts to avoid constraint violations on duplicates
        // For now, add all and let DB constraints handle duplicates (OnConflictIgnore-like behavior)
        // A more efficient approach would be UPSERT or raw SQL, but this works for MVP
        
        foreach (var row in rows)
        {
            // Check if already exists (to handle race conditions)
            var existing = await _db.MarketData
                .FirstOrDefaultAsync(
                    m => m.Symbol == symbol && m.Timeframe == timeframe && m.Timestamp == row.Timestamp,
                    cancellationToken);

            if (existing == null)
            {
                _db.MarketData.Add(row);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<string>> DetectGapsAsync(
        string symbol,
        string timeframe,
        CancellationToken cancellationToken)
    {
        var gaps = new List<string>();

        try
        {
            var bars = await _db.MarketData
                .Where(m => m.Symbol == symbol && m.Timeframe == timeframe)
                .OrderBy(m => m.Timestamp)
                .Select(m => m.Timestamp)
                .ToListAsync(cancellationToken);

            if (bars.Count < 2)
            {
                return gaps;
            }

            var expectedInterval = GetBarInterval(timeframe);

            for (int i = 1; i < bars.Count; i++)
            {
                var timeDiff = bars[i] - bars[i - 1];
                if (timeDiff != expectedInterval)
                {
                    var gapStart = DateOnly.FromDateTime(bars[i - 1]);
                    var gapEnd = DateOnly.FromDateTime(bars[i]);
                    gaps.Add($"Gap from {gapStart} to {gapEnd}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting gaps for {Symbol}/{Timeframe}", symbol, timeframe);
        }

        return gaps;
    }

    private static TimeSpan GetBarInterval(string timeframe) => timeframe switch
    {
        "1h" => TimeSpan.FromHours(1),
        "4h" => TimeSpan.FromHours(4),
        "1d" => TimeSpan.FromDays(1),
        _ => TimeSpan.FromHours(1)
    };
}

// CsvHelper mapping
public class MarketDataMap : ClassMap<MarketData>
{
    public MarketDataMap()
    {
        Map(m => m.Timestamp).Name("timestamp");
        Map(m => m.Open).Name("open");
        Map(m => m.High).Name("high");
        Map(m => m.Low).Name("low");
        Map(m => m.Close).Name("close");
        Map(m => m.Volume).Name("volume");
    }
}
