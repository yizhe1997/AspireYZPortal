#!/usr/bin/env python3
"""
Download Gold Futures (GC) 1H OHLCV data from Yahoo Finance for backtesting.

Usage:
    python download_gold_data.py
    python download_gold_data.py --start 2024-01-01 --end 2026-01-10
    python download_gold_data.py --output custom_gold.csv

Requirements:
    pip install yfinance pandas
"""

import argparse
import sys
from datetime import datetime, timedelta

try:
    import yfinance as yf
    import pandas as pd
except ImportError:
    print("ERROR: Required packages not installed.")
    print("Please run: pip install yfinance pandas")
    sys.exit(1)


def download_gold_data(start_date, end_date, output_file, interval='1h'):
    """
    Download Gold futures data from Yahoo Finance.
    
    Args:
        start_date: Start date (YYYY-MM-DD)
        end_date: End date (YYYY-MM-DD)
        output_file: Output CSV filename
        interval: Data interval (1h, 1d, etc)
    """
    print(f"📊 Downloading Gold (GC=F) data from Yahoo Finance...")
    print(f"   Period: {start_date} to {end_date}")
    print(f"   Interval: {interval}")
    
    try:
        # Download data
        gc = yf.download("GC=F", start=start_date, end=end_date, interval=interval, progress=False)
        
        if gc.empty:
            print("❌ ERROR: No data downloaded. Check date range or try again later.")
            sys.exit(1)
        
        # Reset index to get timestamp as column
        gc = gc.reset_index()
        
        # Rename columns to match backtest API format
        column_mapping = {
            'Datetime': 'timestamp',
            'Open': 'open',
            'High': 'high',
            'Low': 'low',
            'Close': 'close',
            'Volume': 'volume'
        }
        gc = gc.rename(columns=column_mapping)
        
        # Format timestamp to ISO 8601 with UTC
        gc['timestamp'] = gc['timestamp'].dt.strftime('%Y-%m-%dT%H:%M:%SZ')
        
        # Select only required columns
        gc = gc[['timestamp', 'open', 'high', 'low', 'close', 'volume']]
        
        # Remove any rows with NaN values
        gc = gc.dropna()
        
        # Save to CSV
        gc.to_csv(output_file, index=False)
        
        print(f"✅ Success!")
        print(f"   Bars downloaded: {len(gc)}")
        print(f"   Date range: {gc['timestamp'].min()} to {gc['timestamp'].max()}")
        print(f"   Output file: {output_file}")
        print(f"\n📤 Upload to API with:")
        print(f'   curl -X POST http://localhost:5000/api/market-data/upload \\')
        print(f'     -H "X-API-Key: dev_key_12345" \\')
        print(f'     -F "file=@{output_file}"')
        
    except Exception as e:
        print(f"❌ ERROR: {str(e)}")
        sys.exit(1)


def main():
    parser = argparse.ArgumentParser(
        description='Download Gold futures data for backtesting',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Download last 2 years
  python download_gold_data.py

  # Specify date range
  python download_gold_data.py --start 2024-01-01 --end 2025-12-31

  # Custom output file
  python download_gold_data.py --output my_gold_data.csv

  # Daily data instead of hourly
  python download_gold_data.py --interval 1d
        """
    )
    
    # Calculate default dates (last 2 years)
    end_default = datetime.now().strftime('%Y-%m-%d')
    start_default = (datetime.now() - timedelta(days=730)).strftime('%Y-%m-%d')
    
    parser.add_argument('--start', default=start_default, help='Start date (YYYY-MM-DD)')
    parser.add_argument('--end', default=end_default, help='End date (YYYY-MM-DD)')
    parser.add_argument('--output', default='GOLD_1H.csv', help='Output CSV filename')
    parser.add_argument('--interval', default='1h', choices=['1h', '1d', '1wk'], 
                        help='Data interval')
    
    args = parser.parse_args()
    
    download_gold_data(args.start, args.end, args.output, args.interval)


if __name__ == '__main__':
    main()
