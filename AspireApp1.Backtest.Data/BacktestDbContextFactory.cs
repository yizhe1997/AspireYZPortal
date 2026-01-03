using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AspireApp1.Backtest.Data;

public class BacktestDbContextFactory : IDesignTimeDbContextFactory<BacktestDbContext>
{
    public BacktestDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BacktestDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=backtestdb;Username=postgres;Password=postgres");

        return new BacktestDbContext(optionsBuilder.Options);
    }
}
