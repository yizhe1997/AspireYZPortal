using Microsoft.EntityFrameworkCore;

namespace AspireApp1.Backtest.Data;

public class BacktestDbContext : DbContext
{
    public BacktestDbContext(DbContextOptions<BacktestDbContext> options) : base(options)
    {
    }

    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<MarketData> MarketData => Set<MarketData>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<DetectedZone> DetectedZones => Set<DetectedZone>();
    public DbSet<RunMetric> RunMetrics => Set<RunMetric>();
    public DbSet<EquityCurvePoint> EquityCurve => Set<EquityCurvePoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Strategy
        modelBuilder.Entity<Strategy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CodeRef).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        // MarketData - composite key
        modelBuilder.Entity<MarketData>(entity =>
        {
            entity.HasKey(e => new { e.Symbol, e.Timeframe, e.Timestamp });
            entity.Property(e => e.Symbol).HasMaxLength(20);
            entity.Property(e => e.Timeframe).HasMaxLength(10);
            entity.Property(e => e.Open).HasPrecision(18, 8);
            entity.Property(e => e.High).HasPrecision(18, 8);
            entity.Property(e => e.Low).HasPrecision(18, 8);
            entity.Property(e => e.Close).HasPrecision(18, 8);
            entity.Property(e => e.Volume).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.Symbol, e.Timeframe, e.Timestamp });
        });

        // BacktestRun
        modelBuilder.Entity<BacktestRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Timeframe).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20)
                .HasConversion<string>();
            entity.Property(e => e.WorkerId).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.InitialCapital).HasPrecision(18, 2);
            entity.Property(e => e.Parameters).HasColumnType("jsonb");
            
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });

            entity.HasOne<Strategy>()
                .WithMany()
                .HasForeignKey(e => e.StrategyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Trade
        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Side).IsRequired().HasMaxLength(10)
                .HasConversion<string>();
            entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
            entity.Property(e => e.ExitPrice).HasPrecision(18, 8);
            entity.Property(e => e.Quantity).HasPrecision(18, 8);
            entity.Property(e => e.Pnl).HasPrecision(18, 2);
            entity.Property(e => e.PnlPct).HasPrecision(8, 4);
            entity.Property(e => e.RMultiple).HasPrecision(8, 4);
            entity.Property(e => e.Mae).HasPrecision(8, 4);
            entity.Property(e => e.Mfe).HasPrecision(8, 4);
            entity.Property(e => e.ZoneType).HasMaxLength(50);
            entity.Property(e => e.ZoneStrength).HasPrecision(8, 4);
            entity.Property(e => e.FillType).HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => new { e.RunId, e.EntryTime });
            entity.HasIndex(e => new { e.RunId, e.Side });
            entity.HasIndex(e => new { e.RunId, e.ZoneType });

            entity.HasOne<BacktestRun>()
                .WithMany()
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DetectedZone
        modelBuilder.Entity<DetectedZone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ZoneType).IsRequired().HasMaxLength(20)
                .HasConversion<string>();
            entity.Property(e => e.High).HasPrecision(18, 8);
            entity.Property(e => e.Low).HasPrecision(18, 8);
            entity.Property(e => e.Strength).HasPrecision(8, 4);

            entity.HasIndex(e => new { e.RunId, e.Timestamp });

            entity.HasOne<BacktestRun>()
                .WithMany()
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RunMetric - composite key
        modelBuilder.Entity<RunMetric>(entity =>
        {
            entity.HasKey(e => new { e.RunId, e.MetricName });
            entity.Property(e => e.MetricName).HasMaxLength(100);
            entity.Property(e => e.MetricValue).HasPrecision(18, 8);

            entity.HasOne<BacktestRun>()
                .WithMany()
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EquityCurvePoint - composite key
        modelBuilder.Entity<EquityCurvePoint>(entity =>
        {
            entity.HasKey(e => new { e.RunId, e.Timestamp });
            entity.Property(e => e.Equity).HasPrecision(18, 2);
            entity.Property(e => e.DrawdownPct).HasPrecision(8, 4);

            entity.HasOne<BacktestRun>()
                .WithMany()
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
