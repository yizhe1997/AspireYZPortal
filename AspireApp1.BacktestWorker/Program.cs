using AspireApp1.BacktestWorker.Workers;
using AspireApp1.Backtest.Data;
using AspireApp1.BacktestWorker.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register DbContext
builder.Services.AddDbContext<BacktestDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("backtestdb")));

// Register Redis
builder.AddRedisClient("cache");

// Register services
builder.Services.AddScoped<RedisQueueConsumer>();

// Register worker
builder.Services.AddHostedService<BacktestProcessor>();

var host = builder.Build();
host.Run();
