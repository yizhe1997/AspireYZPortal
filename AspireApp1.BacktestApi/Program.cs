using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using AspireApp1.Backtest.Data;
using AspireApp1.BacktestApi;
using AspireApp1.BacktestApi.Middleware;
using AspireApp1.BacktestApi.Services;
using AspireApp1.BacktestApi.Queue;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register DbContext with connection string from Aspire
builder.Services.AddDbContext<BacktestDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("backtestdb")));

// Register Redis
builder.AddRedisClient("cache");

// Register services
builder.Services.AddScoped<BacktestSubmissionService>();
builder.Services.AddScoped<MarketDataService>();
builder.Services.AddScoped<ResultsService>();
builder.Services.AddScoped<QueueService>();
builder.Services.AddScoped<RedisQueueProducer>();

// Add controllers
builder.Services.AddControllers();

// Add observability
builder.Services.AddObservability(builder.Configuration);

var app = builder.Build();

// Global error handling
app.UseGlobalErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// API Key authentication
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();
app.MapGet("/", () => Results.Ok("Backtest API running"));
app.MapGet("/health", () => Results.Ok("healthy"));

app.Run();
