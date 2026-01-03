using AspireApp1.BacktestApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AspireApp1.BacktestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketDataController : ControllerBase
{
    private readonly MarketDataService _marketDataService;
    private readonly ILogger<MarketDataController> _logger;

    public MarketDataController(MarketDataService marketDataService, ILogger<MarketDataController> logger)
    {
        _marketDataService = marketDataService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string symbol,
        [FromForm] string timeframe,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "File is required" });
        }

        // Enforce file size limit (50 MB)
        const long maxFileSize = 50 * 1024 * 1024;
        if (file.Length > maxFileSize)
        {
            return StatusCode(413, new { error = "File exceeds 50 MB limit" });
        }

        _logger.LogInformation("Uploading market data: {Symbol}/{Timeframe}, file size: {Size}", symbol, timeframe, file.Length);

        await using var stream = file.OpenReadStream();
        var result = await _marketDataService.UploadAsync(stream, symbol, timeframe, cancellationToken);

        if (result.Errors.Any())
        {
            _logger.LogWarning("Upload errors for {Symbol}/{Timeframe}: {Errors}", symbol, timeframe, string.Join("; ", result.Errors));
            return BadRequest(new
            {
                errors = result.Errors,
                inserted = result.RowsInserted,
                skipped = result.RowsSkipped
            });
        }

        return Accepted(new
        {
            message = $"Upload accepted. Processed {result.RowsInserted} rows.",
            inserted = result.RowsInserted,
            skipped = result.RowsSkipped,
            gaps = result.Gaps
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] string? symbol = null,
        [FromQuery] string? timeframe = null,
        CancellationToken cancellationToken = default)
    {
        var data = await _marketDataService.GetAvailableAsync(symbol, timeframe, cancellationToken);

        return Ok(new { data });
    }
}
