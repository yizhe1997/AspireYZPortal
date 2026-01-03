import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from 'recharts';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
const API_KEY = localStorage.getItem('apiKey') || 'dev_key_12345';

interface RunComparison {
  run_id: string;
  symbol: string;
  timeframe: string;
  start_date: string;
  end_date: string;
  status: string;
  created_at: string;
  parameters: string;
  metrics?: Record<string, number>;
  equity_points: { timestamp: string; equity: number }[];
}

interface ComparisonResult {
  runs: RunComparison[];
  parameter_diff: Record<string, unknown[]>;
  count: number;
}

interface BacktestItem {
  run_id: string;
  symbol: string;
  timeframe: string;
  strategy_name?: string;
  created_at: string;
  status: string;
}

const colors = ['#8884d8', '#82ca9d', '#ffc658', '#ff7c7c', '#8dd1e1'];

export const BacktestComparePage: React.FC = () => {
  const [selectedRunIds, setSelectedRunIds] = useState<string[]>([]);
  const [showComparison, setShowComparison] = useState(false);

  // Get available backtests
  const { data: backtestsList } = useQuery({
    queryKey: ['backtests-for-compare', 50, 0],
    queryFn: async () => {
      const response = await fetch(`${API_BASE}/backtests?limit=50&offset=0`, {
        headers: { 'X-API-Key': API_KEY },
      });
      if (!response.ok) throw new Error('Failed to fetch backtests');
      return response.json();
    },
    staleTime: 30000,
  });

  // Compare selected runs
  const { data: comparison, isLoading: isComparing } = useQuery<
    ComparisonResult,
    Error
  >({
    queryKey: ['compare-backtests', selectedRunIds],
    queryFn: async () => {
      const response = await fetch(`${API_BASE}/backtests/compare`, {
        method: 'POST',
        headers: {
          'X-API-Key': API_KEY,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ run_ids: selectedRunIds }),
      });
      if (!response.ok) throw new Error('Failed to compare backtests');
      return response.json();
    },
    enabled: showComparison && selectedRunIds.length > 0,
    staleTime: 60000,
  });

  const handleSelectRun = (runId: string) => {
    if (selectedRunIds.includes(runId)) {
      setSelectedRunIds(selectedRunIds.filter((id) => id !== runId));
    } else {
      if (selectedRunIds.length < 10) {
        setSelectedRunIds([...selectedRunIds, runId]);
      } else {
        alert('Maximum 10 runs can be compared');
      }
    }
  };

  const handleCompare = () => {
    if (selectedRunIds.length === 0) {
      alert('Select at least one backtest to compare');
      return;
    }
    setShowComparison(true);
  };

  // Format equity data for chart
  const chartData = comparison?.runs[0]?.equity_points?.map((point, index) => {
    const dataPoint: Record<string, unknown> = {
      time: new Date(point.timestamp).toLocaleDateString(),
    };

    comparison.runs.forEach((run, runIndex) => {
      const equityPoint = run.equity_points[index];
      if (equityPoint) {
        dataPoint[`run_${runIndex}`] = equityPoint.equity;
      }
    });

    return dataPoint;
  }) || [];

  return (
    <div className="p-8 max-w-7xl mx-auto">
      <h1 className="text-3xl font-bold mb-8">Compare Backtests</h1>

      {/* Selection Phase */}
      {!showComparison ? (
        <div className="space-y-6">
          <div>
            <h2 className="text-xl font-semibold mb-4">
              Select backtests to compare
              {selectedRunIds.length > 0 && ` (${selectedRunIds.length}/10)`}
            </h2>

            {backtestsList?.data?.length === 0 ? (
              <div className="text-gray-600">No backtests available</div>
            ) : (
              <div className="grid gap-2">
                {backtestsList?.data?.map((backtest: BacktestItem) => (
                  <label
                    key={backtest.run_id}
                    className="flex items-center p-3 border rounded hover:bg-gray-50 cursor-pointer"
                  >
                    <input
                      type="checkbox"
                      checked={selectedRunIds.includes(backtest.run_id)}
                      onChange={() =>
                        handleSelectRun(backtest.run_id)
                      }
                      className="mr-3"
                    />
                    <div className="flex-1">
                      <div className="font-medium">
                        {backtest.symbol}/{backtest.timeframe}
                      </div>
                      <div className="text-sm text-gray-600">
                        {backtest.strategy_name || 'N/A'} •{' '}
                        {new Date(backtest.created_at).toLocaleDateString()}
                      </div>
                    </div>
                    <span
                      className={`px-2 py-1 rounded text-sm ${
                        backtest.status === 'completed'
                          ? 'bg-green-100 text-green-800'
                          : 'bg-gray-100 text-gray-800'
                      }`}
                    >
                      {backtest.status}
                    </span>
                  </label>
                ))}
              </div>
            )}
          </div>

          <div className="flex gap-2">
            <button
              onClick={handleCompare}
              disabled={selectedRunIds.length === 0}
              className="px-6 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
            >
              Compare Selected
            </button>
            {selectedRunIds.length > 0 && (
              <button
                onClick={() => setSelectedRunIds([])}
                className="px-6 py-2 border rounded hover:bg-gray-50"
              >
                Clear Selection
              </button>
            )}
          </div>
        </div>
      ) : isComparing ? (
        <div className="text-center py-12">
          <div className="text-lg">Comparing backtests...</div>
        </div>
      ) : comparison ? (
        <div className="space-y-8">
          {/* Back button */}
          <button
            onClick={() => setShowComparison(false)}
            className="px-4 py-2 border rounded hover:bg-gray-50"
          >
            ← Back to Selection
          </button>

          {/* Equity Curve Comparison */}
          <div className="bg-white p-6 rounded border">
            <h2 className="text-xl font-semibold mb-4">Equity Curves</h2>
            {chartData.length > 0 ? (
              <ResponsiveContainer width="100%" height={400}>
                <LineChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="time" />
                  <YAxis />
                  <Tooltip />
                  <Legend />
                  {comparison.runs.map((run, index) => (
                    <Line
                      key={run.run_id}
                      type="monotone"
                      dataKey={`run_${index}`}
                      stroke={colors[index % colors.length]}
                      name={`${run.symbol}/${run.timeframe} (${run.created_at.split('T')[0]})`}
                      dot={false}
                      isAnimationActive={false}
                    />
                  ))}
                </LineChart>
              </ResponsiveContainer>
            ) : (
              <div className="text-gray-600">No equity data available</div>
            )}
          </div>

          {/* Parameters Comparison */}
          {Object.keys(comparison.parameter_diff).length > 0 && (
            <div className="bg-white p-6 rounded border overflow-x-auto">
              <h2 className="text-xl font-semibold mb-4">Parameters Comparison</h2>
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-2">Parameter</th>
                    {comparison.runs.map((run, index) => (
                      <th key={run.run_id} className="text-left p-2">
                        Run {index + 1}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {Object.entries(comparison.parameter_diff).map(
                    ([param, values]) => (
                      <tr key={param} className="border-b hover:bg-gray-50">
                        <td className="p-2 font-medium">{param}</td>
                        {values.map((value, index) => (
                          <td
                            key={index}
                            className={`p-2 ${
                              values.length > 1 &&
                              values.some((v) => v !== values[0])
                                ? 'bg-yellow-50'
                                : ''
                            }`}
                          >
                            {String(value)}
                          </td>
                        ))}
                      </tr>
                    )
                  )}
                </tbody>
              </table>
            </div>
          )}

          {/* Metrics Comparison */}
          <div className="bg-white p-6 rounded border overflow-x-auto">
            <h2 className="text-xl font-semibold mb-4">Metrics Comparison</h2>
            {comparison.runs.some((r) => r.metrics) ? (
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-2">Metric</th>
                    {comparison.runs.map((run, index) => (
                      <th key={run.run_id} className="text-left p-2">
                        Run {index + 1}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {Array.from(
                    new Set(
                      comparison.runs
                        .flatMap((r) => Object.keys(r.metrics || {}))
                    )
                  ).map((metric) => (
                    <tr key={metric} className="border-b hover:bg-gray-50">
                      <td className="p-2 font-medium">{metric}</td>
                      {comparison.runs.map((run) => (
                        <td key={run.run_id} className="p-2">
                          {run.metrics?.[metric]?.toFixed(4) || 'N/A'}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <div className="text-gray-600">No metrics available</div>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
};
