import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { StatusBadge } from '../components/StatusBadge';
import { Activity, Search, RefreshCw, Layers } from 'lucide-react';

export const TracesPage: React.FC = () => {
  const [filter, setFilter] = useState('');

  const { data: traces, isLoading, refetch } = useQuery({
    queryKey: ['traces'],
    queryFn: () => api.getAllTraces(150),
    refetchInterval: 5000,
  });

  const filtered = traces?.filter(t =>
    !filter ||
    t.operation.toLowerCase().includes(filter.toLowerCase()) ||
    t.runId.toLowerCase().includes(filter.toLowerCase()) ||
    (t.details && t.details.toLowerCase().includes(filter.toLowerCase()))
  );

  return (
    <div className="p-6 max-w-7xl mx-auto w-full space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white flex items-center space-x-2">
            <Activity className="w-5 h-5 text-purple-400" />
            <span>OpenTelemetry & Audit Traces</span>
          </h1>
          <p className="text-sm text-gray-400 mt-1">
            Fine-grained execution spans: context builds, policy evaluations, and tool calls.
          </p>
        </div>
        <button
          onClick={() => refetch()}
          className="flex items-center px-3 py-1.5 bg-gray-800 hover:bg-gray-700 text-gray-200 rounded-lg text-xs font-mono transition"
        >
          <RefreshCw className="w-3.5 h-3.5 mr-1.5" /> Refresh
        </button>
      </div>

      <div className="relative">
        <Search className="w-4 h-4 text-gray-400 absolute left-3 top-2.5" />
        <input
          type="text"
          placeholder="Filter traces by operation, Run ID, or keywords..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className="w-full bg-[#111827] border border-gray-800 rounded-lg pl-9 pr-3 py-2 text-xs text-gray-200 placeholder-gray-500 focus:outline-none focus:border-blue-500 font-mono"
        />
      </div>

      <div className="bg-[#111827] border border-gray-800 rounded-xl overflow-hidden">
        <table className="w-full text-left text-xs">
          <thead className="bg-[#0e1422] text-gray-400 uppercase tracking-wider border-b border-gray-800">
            <tr>
              <th className="p-3.5 font-semibold">Operation</th>
              <th className="p-3.5 font-semibold">Run ID</th>
              <th className="p-3.5 font-semibold">Latency</th>
              <th className="p-3.5 font-semibold">Status</th>
              <th className="p-3.5 font-semibold">Details</th>
              <th className="p-3.5 font-semibold text-right">Timestamp</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-800/60 font-mono">
            {isLoading ? (
              <tr>
                <td colSpan={6} className="p-6 text-center text-gray-500 font-sans">Loading traces...</td>
              </tr>
            ) : filtered?.length === 0 ? (
              <tr>
                <td colSpan={6} className="p-6 text-center text-gray-500 font-sans">No traces matched filter.</td>
              </tr>
            ) : (
              filtered?.map((t, idx) => (
                <tr key={idx} className="hover:bg-gray-800/40 transition">
                  <td className="p-3.5 text-blue-400 font-bold">{t.operation}</td>
                  <td className="p-3.5 text-gray-400">{t.runId ? t.runId.substring(0, 8) + '...' : '-'}</td>
                  <td className="p-3.5 text-gray-300">{Math.round(t.latencyMs)}ms</td>
                  <td className="p-3.5"><StatusBadge status={t.status} size="sm" /></td>
                  <td className="p-3.5 text-gray-300 font-sans max-w-md truncate">{t.details || '-'}</td>
                  <td className="p-3.5 text-right text-gray-500">{new Date(t.timestamp).toLocaleTimeString()}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
