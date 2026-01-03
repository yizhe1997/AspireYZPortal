import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { TradesTable } from '../components/TradesTable';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
const API_KEY = localStorage.getItem('apiKey') || 'dev_key_12345';

interface BacktestDetail {
  run_id: string;
  strategy_name?: string;
  symbol: string;
  timeframe: string;
  start_date: string;
  end_date: string;
  status: string;
  progress: number;
  created_at: string;
  finished_at?: string;
  initial_capital: number;
  final_equity?: number;
  win_rate?: number;
  queue_position?: number;
  estimated_start_time?: string;
  metrics?: {
    win_rate: number;
    avg_r_multiple: number;
    sharpe_ratio: number;
    max_drawdown: number;
    max_drawdown_pct: number;
    profit_factor: number;
    total_trades: number;
    winning_trades: number;
    losing_trades: number;
    total_pnl: number;
    total_pnl_pct: number;
  };
}

export const BacktestDetailPage: React.FC = () => {
  const { runId } = useParams<{ runId: string }>();
  const [activeTab, setActiveTab] = useState<'metrics' | 'equity' | 'trades'>('metrics');
  const [cancelling, setCancelling] = useState(false);

  const { data, isLoading, error } = useQuery<BacktestDetail, Error>({
    queryKey: ['backtest', runId],
    queryFn: async () => {
      if (!runId) throw new Error('No run ID');

      const response = await fetch(`${API_BASE}/backtests/${runId}`, {
        headers: { 'X-API-Key': API_KEY },
      });

      if (!response.ok) {
        throw new Error(`Failed to fetch backtest: ${response.statusText}`);
      }

      return response.json();
    },
    staleTime: 30000,
    gcTime: 60000,
  });

  if (isLoading) {
    return <div className="p-8 text-center">Loading backtest details...</div>;
  }

  if (error) {
    return (
      <div className="p-8 text-center text-red-600">
        Error: {error.message}
      </div>
    );
  }

  if (!data || !runId) {
    return <div className="p-8 text-center">No backtest data found</div>;
  }

  const statusColor = {
    queued: 'bg-yellow-100 text-yellow-800',
    running: 'bg-blue-100 text-blue-800',
    completed: 'bg-green-100 text-green-800',
    failed: 'bg-red-100 text-red-800',
    cancelled: 'bg-gray-100 text-gray-800',
  }[data.status] || 'bg-gray-100 text-gray-800';

  const canCancel = data.status === 'queued' || data.status === 'running';

  const handleCancel = async () => {
    if (!window.confirm('Are you sure you want to cancel this backtest?')) {
      return;
    }

    try {
      setCancelling(true);
      const response = await fetch(`${API_BASE}/backtests/${runId}/cancel`, {
        method: 'POST',
        headers: { 'X-API-Key': API_KEY },
      });

      if (!response.ok) {
        const error = await response.json();
        alert(`Failed to cancel: ${error.error}`);
        return;
      }

      alert('Backtest cancelled successfully');
      // Refresh the page
      window.location.reload();
    } catch (err) {
      alert(`Error: ${err instanceof Error ? err.message : 'Unknown error'}`);
    } finally {
      setCancelling(false);
    }
  };

  return (
    <div className="p-8 max-w-7xl mx-auto">
      {/* Header */}
      <div className="mb-8">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h1 className="text-3xl font-bold">
              {data.symbol}/{data.timeframe}
            </h1>
            {data.strategy_name && (
              <p className="text-gray-600 mt-1">{data.strategy_name}</p>
            )}
          </div>
          <div className="flex items-center gap-4">
            <div className={`px-4 py-2 rounded font-medium ${statusColor}`}>
              {data.status.toUpperCase()}
            </div>
            {canCancel && (
              <button
                onClick={handleCancel}
                disabled={cancelling}
                className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50"
              >
                {cancelling ? 'Cancelling...' : 'Cancel'}
              </button>
            )}
          </div>
        </div>

        {/* Queue info */}
        {data.queue_position !== undefined && data.queue_position > 0 && (
          <div className="mb-4 p-3 bg-yellow-50 border border-yellow-200 rounded">
            <div className="font-medium text-yellow-900">
              Queue Position: {data.queue_position}
            </div>
            {data.estimated_start_time && (
              <div className="text-sm text-yellow-800">
                Estimated start: {new Date(data.estimated_start_time).toLocaleString()}
              </div>
            )}
          </div>
        )}

        {data.progress > 0 && (
          <div className="mb-4">
            <div className="flex justify-between text-sm mb-2">
              <span>Progress</span>
              <span>{data.progress}%</span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className="bg-blue-600 h-2 rounded-full transition-all"
                style={{ width: `${data.progress}%` }}
              />
            </div>
          </div>
        )}

        <div className="text-sm text-gray-600">
          <p>
            {new Date(data.start_date).toLocaleDateString()} to{' '}
            {new Date(data.end_date).toLocaleDateString()}
          </p>
          <p>Created: {new Date(data.created_at).toLocaleString()}</p>
          {data.finished_at && (
            <p>Completed: {new Date(data.finished_at).toLocaleString()}</p>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="border-b mb-6">
        <div className="flex gap-8">
          {(['metrics', 'equity', 'trades'] as const).map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`pb-2 font-medium transition-colors ${
                activeTab === tab
                  ? 'border-b-2 border-blue-600 text-blue-600'
                  : 'text-gray-600 hover:text-gray-900'
              }`}
            >
              {tab.charAt(0).toUpperCase() + tab.slice(1)}
            </button>
          ))}
        </div>
      </div>

      {/* Tab Content */}
      <div>
        {activeTab === 'metrics' && data.metrics && (
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
            <MetricCard label="Total Trades" value={data.metrics.total_trades} />
            <MetricCard
              label="Win Rate"
              value={`${data.metrics.win_rate.toFixed(2)}%`}
            />
            <MetricCard
              label="Profit Factor"
              value={data.metrics.profit_factor.toFixed(2)}
            />
            <MetricCard
              label="Sharpe Ratio"
              value={data.metrics.sharpe_ratio.toFixed(2)}
            />
            <MetricCard
              label="Max Drawdown"
              value={`${data.metrics.max_drawdown_pct.toFixed(2)}%`}
              highlight="negative"
            />
            <MetricCard
              label="Avg R Multiple"
              value={data.metrics.avg_r_multiple.toFixed(2)}
            />
            <MetricCard
              label="Total PnL"
              value={`$${data.metrics.total_pnl.toFixed(2)}`}
              highlight={data.metrics.total_pnl >= 0 ? 'positive' : 'negative'}
            />
            <MetricCard
              label="Return"
              value={`${data.metrics.total_pnl_pct.toFixed(2)}%`}
              highlight={data.metrics.total_pnl_pct >= 0 ? 'positive' : 'negative'}
            />
            <MetricCard
              label="Winning Trades"
              value={`${data.metrics.winning_trades}/${data.metrics.total_trades}`}
            />
          </div>
        )}

        {activeTab === 'equity' && (
          <div className="bg-gray-50 p-4 rounded text-center text-gray-600">
            Equity chart coming soon
          </div>
        )}

        {activeTab === 'trades' && (
          <TradesTable runId={runId} />
        )}
      </div>
    </div>
  );
};

interface MetricCardProps {
  label: string;
  value: string | number;
  highlight?: 'positive' | 'negative';
}

const MetricCard: React.FC<MetricCardProps> = ({
  label,
  value,
  highlight,
}) => {
  const bgColor = highlight === 'positive'
    ? 'bg-green-50'
    : highlight === 'negative'
      ? 'bg-red-50'
      : 'bg-gray-50';

  const textColor = highlight === 'positive'
    ? 'text-green-700'
    : highlight === 'negative'
      ? 'text-red-700'
      : 'text-gray-900';

  return (
    <div className={`p-4 rounded border ${bgColor}`}>
      <div className="text-sm text-gray-600 mb-1">{label}</div>
      <div className={`text-2xl font-bold ${textColor}`}>{value}</div>
    </div>
  );
};
