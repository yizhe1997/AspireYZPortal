import React, { useState } from 'react';
import { useTrades, downloadTradesCSV, type Trade } from '../hooks/useTrades';

interface TradesTableProps {
  runId: string;
}

export const TradesTable: React.FC<TradesTableProps> = ({ runId }) => {
  const [side, setSide] = useState<'long' | 'short' | undefined>();
  const [zoneType, setZoneType] = useState<string>();
  const [limit] = useState(100);
  const [offset, setOffset] = useState(0);
  const [exporting, setExporting] = useState(false);

  const { data, isLoading, error } = useTrades({
    runId,
    side,
    zoneType,
    limit,
    offset,
  });

  const handleExport = async () => {
    try {
      setExporting(true);
      await downloadTradesCSV(runId);
    } catch (err) {
      console.error('Export failed:', err);
      alert('Failed to export trades');
    } finally {
      setExporting(false);
    }
  };

  if (isLoading) {
    return <div className="text-center py-8">Loading trades...</div>;
  }

  if (error) {
    return (
      <div className="text-red-600 py-8">
        Error loading trades: {error.message}
      </div>
    );
  }

  if (!data) {
    return <div className="text-center py-8">No trades data</div>;
  }

  const { trades, pagination } = data;

  const formatPrice = (price: number) => price.toFixed(2);
  const formatPnl = (pnl: number) => {
    const sign = pnl >= 0 ? '+' : '';
    const color = pnl >= 0 ? 'text-green-600' : 'text-red-600';
    return <span className={color}>{sign}${pnl.toFixed(2)}</span>;
  };

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex gap-4 flex-wrap items-center bg-gray-50 p-4 rounded">
        <div>
          <label className="block text-sm font-medium text-gray-700">Side</label>
          <select
            value={side || ''}
            onChange={(e) => {
              setSide(e.target.value ? (e.target.value as 'long' | 'short') : undefined);
              setOffset(0);
            }}
            className="mt-1 px-3 py-2 border border-gray-300 rounded text-sm"
          >
            <option value="">All</option>
            <option value="long">Long</option>
            <option value="short">Short</option>
          </select>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700">Zone</label>
          <select
            value={zoneType || ''}
            onChange={(e) => {
              setZoneType(e.target.value || undefined);
              setOffset(0);
            }}
            className="mt-1 px-3 py-2 border border-gray-300 rounded text-sm"
          >
            <option value="">All</option>
            <option value="supply">Supply</option>
            <option value="demand">Demand</option>
          </select>
        </div>

        <button
          onClick={handleExport}
          disabled={exporting || trades.length === 0}
          className="px-4 py-2 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
        >
          {exporting ? 'Exporting...' : 'Export CSV'}
        </button>
      </div>

      {/* Table */}
      <div className="overflow-x-auto border border-gray-200 rounded">
        <table className="w-full text-sm">
          <thead className="bg-gray-100 border-b">
            <tr>
              <th className="px-4 py-2 text-left">Entry</th>
              <th className="px-4 py-2 text-left">Exit</th>
              <th className="px-4 py-2 text-center">Side</th>
              <th className="px-4 py-2 text-right">Entry</th>
              <th className="px-4 py-2 text-right">Exit</th>
              <th className="px-4 py-2 text-right">PnL</th>
              <th className="px-4 py-2 text-right">PnL%</th>
              <th className="px-4 py-2 text-right">R</th>
              <th className="px-4 py-2 text-right">MAE</th>
              <th className="px-4 py-2 text-right">MFE</th>
              <th className="px-4 py-2 text-center">Bars</th>
              <th className="px-4 py-2 text-center">Zone</th>
            </tr>
          </thead>
          <tbody>
            {trades.map((trade: Trade) => (
              <tr key={trade.trade_id} className="border-b hover:bg-gray-50">
                <td className="px-4 py-2 text-xs whitespace-nowrap">
                  {new Date(trade.entry_time).toLocaleString()}
                </td>
                <td className="px-4 py-2 text-xs whitespace-nowrap">
                  {new Date(trade.exit_time).toLocaleString()}
                </td>
                <td className="px-4 py-2 text-center font-medium">
                  <span className={trade.side === 'long' ? 'text-green-600' : 'text-red-600'}>
                    {trade.side.toUpperCase()}
                  </span>
                </td>
                <td className="px-4 py-2 text-right">${formatPrice(trade.entry_price)}</td>
                <td className="px-4 py-2 text-right">${formatPrice(trade.exit_price)}</td>
                <td className="px-4 py-2 text-right">{formatPnl(trade.pnl)}</td>
                <td className="px-4 py-2 text-right">
                  <span className={trade.pnl_pct >= 0 ? 'text-green-600' : 'text-red-600'}>
                    {trade.pnl_pct >= 0 ? '+' : ''}{trade.pnl_pct.toFixed(2)}%
                  </span>
                </td>
                <td className="px-4 py-2 text-right">{trade.r_multiple.toFixed(2)}R</td>
                <td className="px-4 py-2 text-right text-orange-600">
                  {trade.mae.toFixed(2)}%
                </td>
                <td className="px-4 py-2 text-right text-blue-600">
                  {trade.mfe.toFixed(2)}%
                </td>
                <td className="px-4 py-2 text-center">{trade.holding_bars}</td>
                <td className="px-4 py-2 text-center text-xs">
                  {trade.zone_type ? (
                    <>
                      <div>{trade.zone_type}</div>
                      <div className="text-gray-500">
                        {trade.zone_strength ? trade.zone_strength.toFixed(2) : '—'}
                      </div>
                    </>
                  ) : (
                    '—'
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {trades.length === 0 && (
        <div className="text-center py-8 text-gray-500">
          No trades found matching filters
        </div>
      )}

      {/* Pagination */}
      <div className="flex items-center justify-between text-sm">
        <div className="text-gray-600">
          Showing {offset + 1} to {Math.min(offset + limit, pagination.total)} of{' '}
          {pagination.total} trades
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => setOffset(Math.max(0, offset - limit))}
            disabled={offset === 0}
            className="px-3 py-1 border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50"
          >
            Previous
          </button>
          <button
            onClick={() => setOffset(offset + limit)}
            disabled={!pagination.has_more}
            className="px-3 py-1 border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50"
          >
            Next
          </button>
        </div>
      </div>
    </div>
  );
};
