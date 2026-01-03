namespace AspireApp1.BacktestWorker.Workers;

public class BacktestParametersDto
{
    public int zone_lookback_bars { get; set; }
    public int min_zone_touches { get; set; }
    public decimal zone_width_atr_multiple { get; set; }
    public int max_zone_age_bars { get; set; }
    public decimal stoploss_atr_multiple { get; set; }
    public decimal takeprofit_r_multiple { get; set; }
    public decimal risk_per_trade_pct { get; set; }
    public int max_concurrent_trades { get; set; }
    public int limit_order_offset_ticks { get; set; }
    public bool include_asian_session { get; set; }
    public bool include_london_session { get; set; }
    public bool include_newyork_session { get; set; }
}
