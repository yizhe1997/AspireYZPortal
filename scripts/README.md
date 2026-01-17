# Test Data Scripts

This folder contains scripts to download and prepare test data for the backtesting framework.

## Quick Start

### Linux/Mac/Windows (Python)

```bash
# Install dependencies first
python -m pip install yfinance pandas

# Download last 2 years of data
python download_gold_data.py

# Custom date range
python download_gold_data.py --start 2025-12-01 --end 2025-12-31

# Custom output file
python download_gold_data.py --output my_gold_data.csv

# Daily data instead of hourly
python download_gold_data.py --interval 1d
```

## Scripts

### `download_gold_data.py`

**Purpose**: Downloads Gold futures (GC) OHLCV data from Yahoo Finance

**Requirements**:
- Python 3.7+
- `yfinance` package
- `pandas` package

**Output**: CSV file with columns: `timestamp,open,high,low,close,volume`

**Data Source**: Yahoo Finance ticker `GC=F` (Gold Futures)

**Limitations**:
- Free data source (may have gaps)
- Limited to ~2 years of hourly data
- Not tick-level precision

### `download_and_upload.ps1`

**Purpose**: PowerShell automation script that downloads data and uploads to API

**Requirements**:
- Python 3.7+ with yfinance/pandas
- curl (for upload step)

**What it does**:
1. Checks Python installation
2. Installs yfinance/pandas if missing
3. Downloads Gold data via Python script
4. Uploads to Backtest API (optional)

**Parameters**:
- `-StartDate`: Start date (default: 2 years ago)
- `-EndDate`: End date (default: today)
- `-OutputFile`: CSV filename (default: GOLD_1H.csv)
- `-ApiUrl`: API endpoint (default: http://localhost:5000/api/market-data/upload)
- `-ApiKey`: API key (default: dev_key_12345)
- `-SkipUpload`: Skip upload step, only download

## Manual Upload

After downloading data, upload to the API:

```bash
curl -X POST http://localhost:5000/api/market-data/upload \
  -H "X-API-Key: dev_key_12345" \
  -F "file=@GOLD_1H.csv"
```

## Troubleshooting

### "Python not found"
Install Python from: https://www.python.org/downloads/

### "ModuleNotFoundError: No module named 'yfinance'"
Run: `pip install yfinance pandas`

### "No data downloaded"
- Check internet connection
- Try different date range (Yahoo Finance may have gaps)
- Use more recent dates (older data may not be available)

### "Upload failed"
- Ensure Aspire is running: `dotnet run --project AspireApp1.AppHost`
- Check API is accessible: `curl http://localhost:5000/health`
- Verify API key is correct

## Alternative Data Sources

If Yahoo Finance doesn't work, see the [USAGE_GUIDE.md](../specs/001-backtesting-service/USAGE_GUIDE.md) for alternative free data sources:

- Twelve Data (free tier)
- Polygon.io (free tier)
- AlphaVantage (free tier)
- MetaTrader 5 (free desktop app)
- Investing.com (manual download)

## Expected Data Format

CSV must have these columns (exact names):
```
timestamp,open,high,low,close,volume
```

Example:
```csv
timestamp,open,high,low,close,volume
2024-01-01T00:00:00Z,2050.25,2055.00,2048.00,2053.50,12500
2024-01-01T01:00:00Z,2053.50,2058.75,2052.00,2057.25,11200
```

**Requirements**:
- Timestamps: ISO 8601 format with UTC (YYYY-MM-DDTHH:MM:SSZ)
- Prices: Decimal numbers with up to 8 decimal precision
- Volume: Integer or decimal
- No duplicate timestamps
- Valid OHLC relationships (high >= low, close between high/low)

## Examples

### Example 1: Quick test with default settings
```powershell
.\download_and_upload.ps1
```
Downloads 2 years of data and uploads to API.

### Example 2: Download only, no upload
```bash
python download_gold_data.py --start 2024-01-01 --end 2024-12-31 --output test_data.csv
```

### Example 3: Custom date range with upload
```powershell
.\download_and_upload.ps1 -StartDate "2023-01-01" -EndDate "2024-12-31"
```

### Example 4: Download daily data instead of hourly
```bash
python download_gold_data.py --interval 1d --output GOLD_DAILY.csv
```

## Next Steps

After uploading data:

1. **Verify upload**:
```bash
curl http://localhost:5000/api/market-data \
  -H "X-API-Key: dev_key_12345"
```

2. **Submit a backtest**: See [USAGE_GUIDE.md](../specs/001-backtesting-service/USAGE_GUIDE.md) for complete workflow

3. **Monitor results**: Open http://localhost:3000 to view results in web UI
