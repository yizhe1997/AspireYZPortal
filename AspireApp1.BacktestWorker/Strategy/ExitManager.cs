namespace AspireApp1.BacktestWorker.Strategy;

public class Trade
{
    public Guid Id { get; set; } = Guid.NewGuid();
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
    public ExitReason ExitReason { get; set; }
    public Zone? EntryZone { get; set; }
}

public enum ExitReason
{
    TakeProfit,
    StopLoss,
    TimeLimit
}

public class ExitManager
{
    public (bool ShouldExit, ExitReason? Reason, decimal ExitPrice) CheckExit(
        Position position, 
        BarData bar, 
        int barsHeld)
    {
        if (position.Side == TradeSide.Long)
        {
            // Check stop loss hit
            if (bar.Low <= position.StopLoss)
            {
                return (true, ExitReason.StopLoss, position.StopLoss);
            }

            // Check take profit hit
            if (bar.High >= position.TakeProfit)
            {
                return (true, ExitReason.TakeProfit, position.TakeProfit);
            }
        }
        else // Short
        {
            // Check stop loss hit
            if (bar.High >= position.StopLoss)
            {
                return (true, ExitReason.StopLoss, position.StopLoss);
            }

            // Check take profit hit
            if (bar.Low <= position.TakeProfit)
            {
                return (true, ExitReason.TakeProfit, position.TakeProfit);
            }
        }

        return (false, null, 0);
    }

    public Trade ClosePosition(Position position, BarData exitBar, ExitReason reason, decimal exitPrice)
    {
        decimal pnl;
        if (position.Side == TradeSide.Long)
        {
            pnl = (exitPrice - position.EntryPrice) * position.Quantity;
        }
        else
        {
            pnl = (position.EntryPrice - exitPrice) * position.Quantity;
        }

        var entryValue = position.EntryPrice * position.Quantity;
        var pnlPct = (pnl / entryValue) * 100m;
        var rMultiple = pnl / (position.InitialRisk * position.Quantity);

        // Calculate MAE and MFE
        var mae = CalculateMAE(position, entryValue);
        var mfe = CalculateMFE(position, entryValue);

        var barsHeld = (int)(exitBar.Timestamp - position.EntryTime).TotalHours; // Assuming 1h bars

        return new Trade
        {
            EntryTime = position.EntryTime,
            ExitTime = exitBar.Timestamp,
            Side = position.Side,
            EntryPrice = position.EntryPrice,
            ExitPrice = exitPrice,
            Quantity = position.Quantity,
            Pnl = pnl,
            PnlPct = pnlPct,
            RMultiple = rMultiple,
            Mae = mae,
            Mfe = mfe,
            HoldingBars = barsHeld,
            ExitReason = reason,
            EntryZone = position.EntryZone
        };
    }

    private decimal CalculateMAE(Position position, decimal entryValue)
    {
        // Maximum Adverse Excursion as percentage
        var worstEquity = position.LowestEquity;
        var mae = ((worstEquity - entryValue) / entryValue) * 100m;
        return mae;
    }

    private decimal CalculateMFE(Position position, decimal entryValue)
    {
        // Maximum Favorable Excursion as percentage
        var bestEquity = position.HighestEquity;
        var mfe = ((bestEquity - entryValue) / entryValue) * 100m;
        return mfe;
    }
}
