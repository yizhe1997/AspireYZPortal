namespace AspireApp1.Backtest.Data;

public enum ZoneType
{
    Supply,
    Demand
}

public class DetectedZone
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public DateTime Timestamp { get; set; }
    public ZoneType ZoneType { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Strength { get; set; }
    public int Touches { get; set; }
    public DateTime CreatedAt { get; set; }
}
