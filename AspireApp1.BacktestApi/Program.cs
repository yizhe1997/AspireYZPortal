using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using AspireApp1.Backtest.Data;
using AspireApp1.BacktestApi;
using AspireApp1.BacktestApi.Middleware;
using AspireApp1.BacktestApi.Services;
using AspireApp1.BacktestApi.Queue;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Add HTTP logging for debugging
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;
});

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
builder.Services.AddScoped<ComparisonService>();
builder.Services.AddScoped<RedisQueueProducer>();

// Add controllers
builder.Services.AddControllers();

// Add observability
builder.Services.AddObservability(builder.Configuration);

var app = builder.Build();

// Apply database migrations automatically
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BacktestDbContext>();
    await dbContext.Database.MigrateAsync();
}

// HTTP logging
app.UseHttpLogging();

// Global error handling
app.UseGlobalErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}

// API Key authentication
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();
app.MapGet("/", () => Results.Ok("Backtest API running"));
app.MapGet("/health", () => Results.Ok("healthy"));

app.Run();
