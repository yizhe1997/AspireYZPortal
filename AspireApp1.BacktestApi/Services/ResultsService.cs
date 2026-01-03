using AspireApp1.Backtest.Data;
using CsvHelper;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace AspireApp1.BacktestApi.Services;

public class TradeDto
{
    public Guid TradeId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public string Side { get; set; } = string.Empty;
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal Pnl { get; set; }
    public decimal PnlPct { get; set; }
    public decimal RMultiple { get; set; }
    public decimal Mae { get; set; }
    public decimal Mfe { get; set; }
    public int HoldingBars { get; set; }
    public string? ZoneType { get; set; }
    public decimal? ZoneStrength { get; set; }
    public string FillType { get; set; } = string.Empty;
}

public class TradesQueryResult
{
    public List<TradeDto> Trades { get; set; } = new();
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public bool HasMore { get; set; }
}

public class ResultsService
{
    private readonly BacktestDbContext _db;
    private readonly ILogger<ResultsService> _logger;

    public ResultsService(BacktestDbContext db, ILogger<ResultsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TradesQueryResult> GetTradesAsync(
        Guid runId,
        string? side = null,
        string? zoneType = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        offset = Math.Max(0, offset);

        var query = _db.Trades.Where(t => t.RunId == runId);

        // Filter by side
        if (!string.IsNullOrWhiteSpace(side))
        {
            var sideEnum = side.ToLowerInvariant() == "long" ? TradeSide.Long : TradeSide.Short;
            query = query.Where(t => t.Side == sideEnum);
        }

        // Filter by zone type
        if (!string.IsNullOrWhiteSpace(zoneType))
        {
            query = query.Where(t => t.ZoneType == zoneType);
        }

        var total = await query.CountAsync(cancellationToken);

        var trades = await query
            .OrderBy(t => t.EntryTime)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var dtos = trades.Select(t => new TradeDto
        {
            TradeId = t.Id,
            Symbol = t.Symbol,
            EntryTime = t.EntryTime,
            ExitTime = t.ExitTime,
            Side = t.Side.ToString().ToLowerInvariant(),
            EntryPrice = t.EntryPrice,
            ExitPrice = t.ExitPrice,
            Quantity = t.Quantity,
            Pnl = t.Pnl,
            PnlPct = t.PnlPct,
            RMultiple = t.RMultiple,
            Mae = t.Mae,
            Mfe = t.Mfe,
            HoldingBars = t.HoldingBars,
            ZoneType = t.ZoneType,
            ZoneStrength = t.ZoneStrength,
            FillType = t.FillType
        }).ToList();

        return new TradesQueryResult
        {
            Trades = dtos,
            Total = total,
            Limit = limit,
            Offset = offset,
            HasMore = offset + limit < total
        };
    }

    public async Task<string> ExportTradesAsCsvAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var trades = await _db.Trades
            .Where(t => t.RunId == runId)
            .OrderBy(t => t.EntryTime)
            .ToListAsync(cancellationToken);

        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write header
        csv.WriteField("trade_id");
        csv.WriteField("symbol");
        csv.WriteField("entry_time");
        csv.WriteField("exit_time");
        csv.WriteField("side");
        csv.WriteField("entry_price");
        csv.WriteField("exit_price");
        csv.WriteField("quantity");
        csv.WriteField("pnl");
        csv.WriteField("pnl_pct");
        csv.WriteField("r_multiple");
        csv.WriteField("mae");
        csv.WriteField("mfe");
        csv.WriteField("holding_bars");
        csv.WriteField("zone_type");
        csv.WriteField("zone_strength");
        csv.WriteField("fill_type");
        csv.NextRecord();

        // Write records
        foreach (var trade in trades)
        {
            csv.WriteField(trade.Id);
            csv.WriteField(trade.Symbol);
            csv.WriteField(trade.EntryTime);
            csv.WriteField(trade.ExitTime);
            csv.WriteField(trade.Side);
            csv.WriteField(trade.EntryPrice);
            csv.WriteField(trade.ExitPrice);
            csv.WriteField(trade.Quantity);
            csv.WriteField(trade.Pnl);
            csv.WriteField(trade.PnlPct);
            csv.WriteField(trade.RMultiple);
            csv.WriteField(trade.Mae);
            csv.WriteField(trade.Mfe);
            csv.WriteField(trade.HoldingBars);
            csv.WriteField(trade.ZoneType);
            csv.WriteField(trade.ZoneStrength);
            csv.WriteField(trade.FillType);
            csv.NextRecord();
        }

        return writer.ToString();
    }
}
