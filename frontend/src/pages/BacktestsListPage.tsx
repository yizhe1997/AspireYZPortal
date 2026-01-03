import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
const API_KEY = localStorage.getItem('apiKey') || 'dev_key_12345';

interface Backtest {
  run_id: string;
  strategy_name?: string;
  symbol: string;
  timeframe: string;
  status: string;
  progress: number;
  created_at: string;
  finished_at?: string;
  initial_capital: number;
  final_equity?: number;
  win_rate?: number;
  queue_position?: number;
  estimated_start_time?: string;
}

interface BacktestsResponse {
  data: Backtest[];
  pagination: {
    total: number;
    limit: number;
    offset: number;
  };
}

export const BacktestsListPage: React.FC = () => {
  const navigate = useNavigate();
  const [limit] = useState(10);
  const [offset, setOffset] = useState(0);
  const [strategy, setStrategy] = useState('');
  const [symbol, setSymbol] = useState('');
  const [cancellingId, setCancellingId] = useState<string | null>(null);

  const { data, isLoading, error } = useQuery<BacktestsResponse, Error>({
    queryKey: ['backtests', limit, offset, strategy, symbol],
    queryFn: async () => {
      const params = new URLSearchParams();
      params.append('limit', limit.toString());
      params.append('offset', offset.toString());
      if (strategy) params.append('strategy', strategy);
      if (symbol) params.append('symbol', symbol);

      const response = await fetch(
        `${API_BASE}/backtests?${params.toString()}`,
        {
          headers: { 'X-API-Key': API_KEY },
        }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch backtests: ${response.statusText}`);
      }

      return response.json();
    },
    staleTime: 30000,
    gcTime: 60000,
  });

  const statusColor = (status: string) => {
    const colors: Record<string, string> = {
      queued: 'bg-yellow-100 text-yellow-800',
      running: 'bg-blue-100 text-blue-800',
      completed: 'bg-green-100 text-green-800',
      failed: 'bg-red-100 text-red-800',
      cancelled: 'bg-gray-100 text-gray-800',
    };
    return colors[status] || 'bg-gray-100 text-gray-800';
  };

  const handleCancel = async (runId: string, event: React.MouseEvent) => {
    event.stopPropagation();
    
    if (!window.confirm('Are you sure you want to cancel this backtest?')) {
      return;
    }

    try {
      setCancellingId(runId);
      const response = await fetch(`${API_BASE}/backtests/${runId}/cancel`, {
        method: 'POST',
        headers: { 'X-API-Key': API_KEY },
      });

      if (!response.ok) {
        const error = await response.json();
        alert(`Failed to cancel: ${error.error}`);
        return;
      }

      // Refresh the list
      window.location.reload();
    } catch (err) {
      alert(`Error: ${err instanceof Error ? err.message : 'Unknown error'}`);
    } finally {
      setCancellingId(null);
    }
  };

  const handleRowClick = (runId: string) => {
    navigate(`/backtests/${runId}`);
  };

  const handlePrevious = () => {
    setOffset(Math.max(0, offset - limit));
  };

  const handleNext = () => {
    if (data) {
      setOffset(offset + limit);
    }
  };

  return (
    <div className="p-8 max-w-7xl mx-auto">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-4">Backtests</h1>

        {/* Filters */}
        <div className="grid grid-cols-2 gap-4 mb-6">
          <input
            type="text"
            placeholder="Strategy name"
            value={strategy}
            onChange={(e) => {
              setStrategy(e.target.value);
              setOffset(0);
            }}
            className="px-4 py-2 border rounded"
          />
          <input
            type="text"
            placeholder="Symbol"
            value={symbol}
            onChange={(e) => {
              setSymbol(e.target.value);
              setOffset(0);
            }}
            className="px-4 py-2 border rounded"
          />
        </div>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="text-center py-8">Loading backtests...</div>
      ) : error ? (
        <div className="text-center text-red-600 py-8">
          Error: {error.message}
        </div>
      ) : !data || data.data.length === 0 ? (
        <div className="text-center py-8 text-gray-600">
          No backtests found
        </div>
      ) : (
        <>
          <table className="w-full border-collapse">
            <thead>
              <tr className="border-b-2 border-gray-300">
                <th className="text-left p-3">Symbol</th>
                <th className="text-left p-3">Strategy</th>
                <th className="text-left p-3">Status</th>
                <th className="text-right p-3">Progress</th>
                <th className="text-right p-3">Queue</th>
                <th className="text-right p-3">Win Rate</th>
                <th className="text-right p-3">Final Equity</th>
                <th className="text-left p-3">Created</th>
                <th className="text-left p-3">Actions</th>
              </tr>
            </thead>
            <tbody>
              {data.data.map((backtest) => (
                <tr
                  key={backtest.run_id}
                  onClick={() => handleRowClick(backtest.run_id)}
                  className="border-b hover:bg-gray-50 cursor-pointer"
                >
                  <td className="p-3 font-medium">{backtest.symbol}</td>
                  <td className="p-3">{backtest.strategy_name || '-'}</td>
                  <td className="p-3">
                    <span
                      className={`px-2 py-1 rounded text-sm font-medium ${statusColor(backtest.status)}`}
                    >
                      {backtest.status}
                    </span>
                  </td>
                  <td className="p-3 text-right">
                    <div className="flex items-center gap-2">
                      <div className="w-32 bg-gray-200 rounded-full h-2">
                        <div
                          className="bg-blue-600 h-2 rounded-full"
                          style={{ width: `${backtest.progress}%` }}
                        />
                      </div>
                      <span className="text-sm">{backtest.progress}%</span>
                    </div>
                  </td>
                  <td className="p-3 text-right">
                    {backtest.queue_position !== undefined && backtest.queue_position > 0 ? (
                      <span className="text-sm bg-yellow-100 text-yellow-800 px-2 py-1 rounded">
                        #{backtest.queue_position}
                      </span>
                    ) : (
                      '-'
                    )}
                  </td>
                  <td className="p-3 text-right">
                    {backtest.win_rate ? `${(backtest.win_rate * 100).toFixed(2)}%` : '-'}
                  </td>
                  <td className="p-3 text-right">
                    {backtest.final_equity ? `$${backtest.final_equity.toFixed(2)}` : '-'}
                  </td>
                  <td className="p-3 text-sm">
                    {new Date(backtest.created_at).toLocaleDateString()}
                  </td>
                  <td className="p-3 text-sm" onClick={(e) => e.stopPropagation()}>
                    {(backtest.status === 'queued' || backtest.status === 'running') && (
                      <button
                        onClick={(e) => handleCancel(backtest.run_id, e)}
                        disabled={cancellingId === backtest.run_id}
                        className="px-2 py-1 bg-red-600 text-white text-xs rounded hover:bg-red-700 disabled:opacity-50"
                      >
                        {cancellingId === backtest.run_id ? 'Cancelling...' : 'Cancel'}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pagination */}
          <div className="mt-6 flex items-center justify-between">
            <div className="text-sm text-gray-600">
              Showing {offset + 1} to {Math.min(offset + limit, data.pagination.total)} of{' '}
              {data.pagination.total} results
            </div>
            <div className="flex gap-2">
              <button
                onClick={handlePrevious}
                disabled={offset === 0}
                className="px-4 py-2 border rounded disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Previous
              </button>
              <button
                onClick={handleNext}
                disabled={offset + limit >= data.pagination.total}
                className="px-4 py-2 border rounded disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Next
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
};
