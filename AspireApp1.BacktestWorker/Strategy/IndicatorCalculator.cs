namespace AspireApp1.BacktestWorker.Strategy;

public class IndicatorCalculator
{
    public static decimal CalculateATR(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 14)
    {
        if (highs.Count < period + 1 || lows.Count < period + 1 || closes.Count < period + 1)
        {
            return 0;
        }

        var trueRanges = new List<decimal>();
        
        for (int i = 1; i < highs.Count; i++)
        {
            var high = highs[i];
            var low = lows[i];
            var prevClose = closes[i - 1];
            
            var tr1 = high - low;
            var tr2 = Math.Abs(high - prevClose);
            var tr3 = Math.Abs(low - prevClose);
            
            trueRanges.Add(Math.Max(tr1, Math.Max(tr2, tr3)));
        }

        if (trueRanges.Count < period)
        {
            return 0;
        }

        // Simple Moving Average of True Range
        var atr = trueRanges.Skip(trueRanges.Count - period).Take(period).Average();
        return atr;
    }

    public static List<decimal> CalculateATRSeries(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 14)
    {
        var atrSeries = new List<decimal>();
        
        for (int i = 0; i < highs.Count; i++)
        {
            var windowHighs = highs.Take(i + 1).ToList();
            var windowLows = lows.Take(i + 1).ToList();
            var windowCloses = closes.Take(i + 1).ToList();
            
            var atr = CalculateATR(windowHighs, windowLows, windowCloses, period);
            atrSeries.Add(atr);
        }

        return atrSeries;
    }
}
