namespace AspireApp1.Backtest.Data;

public class RunMetric
{
    public Guid RunId { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public decimal MetricValue { get; set; }
}
