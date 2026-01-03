namespace AspireApp1.Backtest.Data;

public enum TradeSide
{
    Long,
    Short
}

public class Trade
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public TradeSide Side { get; set; }
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
    public string FillType { get; set; } = "limit";
    public string? Notes { get; set; }
}
