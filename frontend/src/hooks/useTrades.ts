import { useQuery } from '@tanstack/react-query';

export interface Trade {
  trade_id: string;
  symbol: string;
  entry_time: string;
  exit_time: string;
  side: 'long' | 'short';
  entry_price: number;
  exit_price: number;
  quantity: number;
  pnl: number;
  pnl_pct: number;
  r_multiple: number;
  mae: number;
  mfe: number;
  holding_bars: number;
  zone_type?: string;
  zone_strength?: number;
  fill_type: string;
}

export interface TradesResponse {
  run_id: string;
  trades: Trade[];
  pagination: {
    total: number;
    limit: number;
    offset: number;
    has_more: boolean;
  };
}

interface UseTrades {
  runId: string;
  side?: 'long' | 'short';
  zoneType?: string;
  limit?: number;
  offset?: number;
}

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
const API_KEY = localStorage.getItem('apiKey') || 'dev_key_12345';

export const useTrades = ({
  runId,
  side,
  zoneType,
  limit = 100,
  offset = 0,
}: UseTrades) => {
  const queryKey = ['trades', runId, side, zoneType, limit, offset];

  const query = useQuery<TradesResponse, Error>({
    queryKey,
    queryFn: async () => {
      const params = new URLSearchParams();
      if (side) params.append('side', side);
      if (zoneType) params.append('zoneType', zoneType);
      params.append('limit', limit.toString());
      params.append('offset', offset.toString());

      const response = await fetch(
        `${API_BASE}/backtests/${runId}/trades?${params}`,
        {
          headers: {
            'X-API-Key': API_KEY,
          },
        }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch trades: ${response.statusText}`);
      }

      return response.json();
    },
    staleTime: 30000,
    gcTime: 60000,
  });

  return query;
};

export const downloadTradesCSV = async (runId: string) => {
  const response = await fetch(
    `${API_BASE}/backtests/${runId}/trades?export=true`,
    {
      headers: {
        'X-API-Key': API_KEY,
      },
    }
  );

  if (!response.ok) {
    throw new Error(`Failed to export trades: ${response.statusText}`);
  }

  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `trades_${runId}.csv`;
  link.click();
  URL.revokeObjectURL(url);
};
