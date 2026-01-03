namespace AspireApp1.Backtest.Data;

public class EquityCurvePoint
{
    public Guid RunId { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal Equity { get; set; }
    public decimal DrawdownPct { get; set; }
}
