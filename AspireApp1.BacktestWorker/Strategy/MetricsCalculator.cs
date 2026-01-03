namespace AspireApp1.BacktestWorker.Strategy;

public class BacktestMetrics
{
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalPnl { get; set; }
    public decimal TotalPnlPct { get; set; }
    public decimal AvgWin { get; set; }
    public decimal AvgLoss { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal AvgRMultiple { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal MaxDrawdownPct { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal AvgMae { get; set; }
    public decimal AvgMfe { get; set; }
    public int AvgHoldingBars { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }
}

public class EquityPoint
{
    public DateTime Timestamp { get; set; }
    public decimal Equity { get; set; }
    public decimal DrawdownPct { get; set; }
}

public class MetricsCalculator
{
    public static BacktestMetrics Calculate(List<Trade> trades, List<EquityPoint> equityCurve, decimal initialCapital)
    {
        if (trades.Count == 0)
        {
            return new BacktestMetrics();
        }

        var winningTrades = trades.Where(t => t.Pnl > 0).ToList();
        var losingTrades = trades.Where(t => t.Pnl < 0).ToList();

        var totalPnl = trades.Sum(t => t.Pnl);
        var totalPnlPct = (totalPnl / initialCapital) * 100m;
        
        var winRate = trades.Count > 0 ? (decimal)winningTrades.Count / trades.Count * 100m : 0;
        
        var avgWin = winningTrades.Any() ? winningTrades.Average(t => t.Pnl) : 0;
        var avgLoss = losingTrades.Any() ? losingTrades.Average(t => t.Pnl) : 0;
        
        var grossProfit = winningTrades.Sum(t => t.Pnl);
        var grossLoss = Math.Abs(losingTrades.Sum(t => t.Pnl));
        var profitFactor = grossLoss > 0 ? grossProfit / grossLoss : 0;

        var avgRMultiple = trades.Average(t => t.RMultiple);
        var avgMae = trades.Average(t => t.Mae);
        var avgMfe = trades.Average(t => t.Mfe);
        var avgHoldingBars = (int)trades.Average(t => t.HoldingBars);

        var largestWin = winningTrades.Any() ? winningTrades.Max(t => t.Pnl) : 0;
        var largestLoss = losingTrades.Any() ? losingTrades.Min(t => t.Pnl) : 0;

        var (maxDrawdown, maxDrawdownPct) = CalculateMaxDrawdown(equityCurve);
        var sharpeRatio = CalculateSharpeRatio(trades, initialCapital);

        return new BacktestMetrics
        {
            TotalTrades = trades.Count,
            WinningTrades = winningTrades.Count,
            LosingTrades = losingTrades.Count,
            WinRate = winRate,
            TotalPnl = totalPnl,
            TotalPnlPct = totalPnlPct,
            AvgWin = avgWin,
            AvgLoss = avgLoss,
            ProfitFactor = profitFactor,
            AvgRMultiple = avgRMultiple,
            MaxDrawdown = maxDrawdown,
            MaxDrawdownPct = maxDrawdownPct,
            SharpeRatio = sharpeRatio,
            AvgMae = avgMae,
            AvgMfe = avgMfe,
            AvgHoldingBars = avgHoldingBars,
            LargestWin = largestWin,
            LargestLoss = largestLoss
        };
    }

    private static (decimal MaxDrawdown, decimal MaxDrawdownPct) CalculateMaxDrawdown(List<EquityPoint> equityCurve)
    {
        if (equityCurve.Count == 0)
        {
            return (0, 0);
        }

        var maxEquity = equityCurve[0].Equity;
        var maxDrawdown = 0m;
        var maxDrawdownPct = 0m;

        foreach (var point in equityCurve)
        {
            maxEquity = Math.Max(maxEquity, point.Equity);
            var drawdown = maxEquity - point.Equity;
            var drawdownPct = maxEquity > 0 ? (drawdown / maxEquity) * 100m : 0;

            maxDrawdown = Math.Max(maxDrawdown, drawdown);
            maxDrawdownPct = Math.Max(maxDrawdownPct, drawdownPct);
        }

        return (maxDrawdown, maxDrawdownPct);
    }

    private static decimal CalculateSharpeRatio(List<Trade> trades, decimal initialCapital)
    {
        if (trades.Count < 2)
        {
            return 0;
        }

        var returns = trades.Select(t => t.PnlPct).ToList();
        var avgReturn = returns.Average();
        var stdDev = CalculateStandardDeviation(returns);

        if (stdDev == 0)
        {
            return 0;
        }

        // Annualized Sharpe Ratio (assuming ~252 trading days)
        var sharpe = (avgReturn / stdDev) * (decimal)Math.Sqrt(252);
        return sharpe;
    }

    private static decimal CalculateStandardDeviation(List<decimal> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var avg = values.Average();
        var sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
        var variance = sumOfSquares / (values.Count - 1);
        
        return (decimal)Math.Sqrt((double)variance);
    }

    public static List<EquityPoint> BuildEquityCurve(List<Trade> trades, decimal initialCapital)
    {
        var curve = new List<EquityPoint>();
        var currentEquity = initialCapital;
        var maxEquity = initialCapital;

        // Add initial point
        curve.Add(new EquityPoint
        {
            Timestamp = trades.FirstOrDefault()?.EntryTime ?? DateTime.UtcNow,
            Equity = initialCapital,
            DrawdownPct = 0
        });

        foreach (var trade in trades.OrderBy(t => t.ExitTime))
        {
            currentEquity += trade.Pnl;
            maxEquity = Math.Max(maxEquity, currentEquity);
            
            var drawdownPct = maxEquity > 0 ? ((maxEquity - currentEquity) / maxEquity) * 100m : 0;

            curve.Add(new EquityPoint
            {
                Timestamp = trade.ExitTime,
                Equity = currentEquity,
                DrawdownPct = drawdownPct
            });
        }

        return curve;
    }
}
