import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { BarChart3, Activity, CheckCircle, AlertTriangle, Clock, RefreshCw } from 'lucide-react';

export const MetricsPage: React.FC = () => {
  const { data: metrics, isLoading, refetch } = useQuery({
    queryKey: ['metrics'],
    queryFn: api.getMetrics,
    refetchInterval: 5000,
  });

  return (
    <div className="p-6 max-w-7xl mx-auto w-full space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white flex items-center space-x-2">
            <BarChart3 className="w-5 h-5 text-indigo-400" />
            <span>Telemetry & Performance Metrics</span>
          </h1>
          <p className="text-sm text-gray-400 mt-1">
            Operational metrics exposed by the OpenTelemetry Meter abstraction.
          </p>
        </div>
        <button
          onClick={() => refetch()}
          className="flex items-center px-3 py-1.5 bg-gray-800 hover:bg-gray-700 text-gray-200 rounded-lg text-xs font-mono transition"
        >
          <RefreshCw className="w-3.5 h-3.5 mr-1.5" /> Refresh
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5">
          <span className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Total Runs Counter</span>
          <div className="text-3xl font-bold text-white font-mono mt-2">{metrics?.totalRuns ?? 0}</div>
          <p className="text-xs text-gray-400 mt-1">Meter: harness.runs.total</p>
        </div>

        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5">
          <span className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Success Ratio</span>
          <div className="text-3xl font-bold text-emerald-400 font-mono mt-2">
            {metrics?.totalRuns ? Math.round((metrics.successfulRuns / metrics.totalRuns) * 100) : 100}%
          </div>
          <p className="text-xs text-gray-400 mt-1">Completed vs Failed</p>
        </div>

        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5">
          <span className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Average Duration</span>
          <div className="text-3xl font-bold text-purple-400 font-mono mt-2">
            {metrics?.averageLatencyMs ?? 0} ms
          </div>
          <p className="text-xs text-gray-400 mt-1">Meter: harness.run.duration_ms</p>
        </div>

        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5">
          <span className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Tool Invocations</span>
          <div className="text-3xl font-bold text-amber-400 font-mono mt-2">{metrics?.toolCalls ?? 0}</div>
          <p className="text-xs text-gray-400 mt-1">Meter: harness.tool_calls.total</p>
        </div>
      </div>

      {/* Observability Standards Explanation Card */}
      <div className="bg-[#111827] border border-gray-800 rounded-xl p-6 space-y-4">
        <h2 className="text-sm font-bold text-white uppercase tracking-wider flex items-center">
          <Activity className="w-4 h-4 mr-2 text-blue-400" />
          Harness Observability Architecture
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs text-gray-300">
          <div className="p-3 bg-gray-900/80 border border-gray-800 rounded-lg">
            <h3 className="font-semibold text-blue-400 mb-1">OpenTelemetry Spans</h3>
            <p className="text-gray-400">
              Each execution phase (Context, Plan, Act, Observe, Evaluate) creates structured traces with correlation IDs (RunId, TraceId).
            </p>
          </div>
          <div className="p-3 bg-gray-900/80 border border-gray-800 rounded-lg">
            <h3 className="font-semibold text-emerald-400 mb-1">Serilog Structured Logs</h3>
            <p className="text-gray-400">
              Output formats serialize enriched JSON metadata without sensitive tool argument leakage.
            </p>
          </div>
          <div className="p-3 bg-gray-900/80 border border-gray-800 rounded-lg">
            <h3 className="font-semibold text-purple-400 mb-1">Resilience Metrics</h3>
            <p className="text-gray-400">
              Tracks exponential backoff retries, validation rejections, and policy enforcement blocks.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};
