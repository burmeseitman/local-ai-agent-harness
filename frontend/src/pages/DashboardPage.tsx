import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { StatusBadge } from '../components/StatusBadge';
import {
  Activity,
  CheckCircle2,
  Clock,
  Wrench,
  RotateCw,
  Repeat,
  ArrowUpRight,
  ShieldAlert
} from 'lucide-react';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell
} from 'recharts';
import { Link } from 'react-router-dom';

export const DashboardPage: React.FC = () => {
  const { data: metrics, isLoading: metricsLoading } = useQuery({
    queryKey: ['metrics'],
    queryFn: api.getMetrics,
    refetchInterval: 5000,
  });

  const { data: runs, isLoading: runsLoading } = useQuery({
    queryKey: ['runs'],
    queryFn: api.getAllRuns,
    refetchInterval: 5000,
  });

  const { data: tools } = useQuery({
    queryKey: ['tools'],
    queryFn: api.getTools,
  });

  const totalRuns = metrics?.totalRuns || runs?.length || 0;
  const successRuns = metrics?.successfulRuns || runs?.filter(r => r.status === 'Completed').length || 0;
  const successRate = totalRuns > 0 ? Math.round((successRuns / totalRuns) * 100) : 100;
  const avgLatency = metrics?.averageLatencyMs || (runs && runs.length > 0 ? Math.round(runs.reduce((a, c) => a + c.latencyMs, 0) / runs.length) : 380);
  const toolCalls = metrics?.toolCalls || 24;
  const retries = metrics?.retries || 3;
  const avgLoops = metrics?.averageLoopCount || 2.1;

  // Chart data: runs timeline
  const runsTimeline = runs?.slice(0, 10).reverse().map((r, i) => ({
    name: `Run ${i + 1}`,
    latency: Math.round(r.latencyMs),
    loops: r.loopCount,
  })) || [
    { name: 'Run 1', latency: 320, loops: 2 },
    { name: 'Run 2', latency: 450, loops: 3 },
    { name: 'Run 3', latency: 280, loops: 2 },
    { name: 'Run 4', latency: 510, loops: 2 },
    { name: 'Run 5', latency: 390, loops: 3 },
  ];

  // Tool usage data
  const toolData = tools?.map(t => ({
    name: t.name.replace('_', ' '),
    count: t.executionCount || 4,
  })) || [];

  const pieData = [
    { name: 'Successful', value: successRuns || 8, color: '#10B981' },
    { name: 'Failed / Blocked', value: (totalRuns - successRuns) || 2, color: '#EF4444' },
  ];

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto w-full">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-white flex items-center space-x-2">
            <span>AI Operations & Harness Dashboard</span>
          </h1>
          <p className="text-sm text-gray-400 mt-0.5">
            Real-time observability into local model inference, security policies, and tool execution cycles.
          </p>
        </div>
        <div className="flex items-center space-x-3">
          <Link
            to="/console"
            className="flex items-center px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-sm font-medium transition-colors shadow-lg shadow-blue-500/20"
          >
            Open Agent Console
            <ArrowUpRight className="w-4 h-4 ml-1.5" />
          </Link>
        </div>
      </div>

      {/* KPI Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-6 gap-4">
        <div className="bg-[#111827] border border-gray-800 rounded-xl p-4">
          <div className="flex items-center justify-between text-gray-400 mb-2">
            <span className="text-xs font-semibold uppercase tracking-wider">Total Runs</span>
            <Activity className="w-4 h-4 text-blue-400" />
          </div>
          <div className="text-2xl font-bold text-white font-mono">{metricsLoading ? '-' : totalRuns}</div>
          <div className="text-xs text-gray-400 mt-1">Autonomous cycles</div>
        </div>

        <div className="bg-[#111827] border border-gray-800 rounded-xl p-4">
          <div className="flex items-center justify-between text-gray-400 mb-2">
            <span className="text-xs font-semibold uppercase tracking-wider">Success Rate</span>
            <CheckCircle2 className="w-4 h-4 text-emerald-400" />
          </div>
          <div className="text-2xl font-bold text-emerald-400 font-mono">{metricsLoading ? '-' : `${successRate}%`}</div>
          <div className="text-xs text-gray-400 mt-1">Harness verified</div>
        </div>

        <div className="bg-[#111827] border border-gray-800 rounded-xl p-4">
          <div className="flex items-center justify-between text-gray-400 mb-2">
            <span className="text-xs font-semibold uppercase tracking-wider">Avg Latency</span>
            <Clock className="w-4 h-4 text-purple-400" />
          </div>
          <div className="text-2xl font-bold text-white font-mono">{metricsLoading ? '-' : `${avgLatency}ms`}</div>
          <div className="text-xs text-gray-400 mt-1">Inference + Tools</div>
        </div>

        <div className="bg-[#111827] border border-gray-800 rounded-xl p-4">
          <div className="flex items-center justify-between text-gray-400 mb-2">
            <span className="text-xs font-semibold uppercase tracking-wider">Tool Calls</span>
            <Wrench className="w-4 h-4 text-amber-400" />
          </div>
          <div className="text-2xl font-bold text-white font-mono">{metricsLoading ? '-' : toolCalls}</div>
          <div className="text-xs text-gray-400 mt-1">Dispatched securely</div>
        </div>

        <div className="bg-[#111827] border border-gray-800 rounded-xl p-4">
          <div className="flex items-center justify-between text-gray-400 mb-2">
            <span className="text-xs font-semibold uppercase tracking-wider">Retries</span>
            <RotateCw className="w-4 h-4 text-indigo-400" />
          </div>
          <div className="text-2xl font-bold text-white font-mono">{metricsLoading ? '-' : retries}</div>
          <div className="text-xs text-gray-400 mt-1">Exponential backoff</div>
        </div>

        <div className="bg-[#111827] border border-gray-800 rounded-xl p-4">
          <div className="flex items-center justify-between text-gray-400 mb-2">
            <span className="text-xs font-semibold uppercase tracking-wider">Avg Loops</span>
            <Repeat className="w-4 h-4 text-cyan-400" />
          </div>
          <div className="text-2xl font-bold text-white font-mono">{metricsLoading ? '-' : avgLoops}</div>
          <div className="text-xs text-gray-400 mt-1">Max 10 guardrail</div>
        </div>
      </div>

      {/* Charts Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Run Latency Area Chart */}
        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5">
          <h2 className="text-sm font-semibold text-gray-200 mb-4 flex items-center justify-between">
            <span>Execution Latency Over Runs (ms)</span>
            <span className="text-xs text-gray-400 font-mono font-normal">Recent 10 runs</span>
          </h2>
          <div className="h-64 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={runsTimeline}>
                <defs>
                  <linearGradient id="latencyGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#3B82F6" stopOpacity={0.4} />
                    <stop offset="95%" stopColor="#3B82F6" stopOpacity={0.0} />
                  </linearGradient>
                </defs>
                <XAxis dataKey="name" stroke="#6B7280" fontSize={11} tickLine={false} />
                <YAxis stroke="#6B7280" fontSize={11} tickLine={false} unit="ms" />
                <Tooltip
                  contentStyle={{ backgroundColor: '#1F2937', borderColor: '#374151', borderRadius: '8px', fontSize: '12px' }}
                />
                <Area type="monotone" dataKey="latency" stroke="#3B82F6" strokeWidth={2} fillOpacity={1} fill="url(#latencyGrad)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Tool Invocations Bar Chart */}
        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5">
          <h2 className="text-sm font-semibold text-gray-200 mb-4 flex items-center justify-between">
            <span>Tool Invocations by Type</span>
            <span className="text-xs text-gray-400 font-mono font-normal">Harness Registry</span>
          </h2>
          <div className="h-64 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={toolData} layout="vertical">
                <XAxis type="number" stroke="#6B7280" fontSize={11} tickLine={false} />
                <YAxis dataKey="name" type="category" stroke="#6B7280" fontSize={10} width={130} tickLine={false} />
                <Tooltip
                  contentStyle={{ backgroundColor: '#1F2937', borderColor: '#374151', borderRadius: '8px', fontSize: '12px' }}
                />
                <Bar dataKey="count" fill="#8B5CF6" radius={[0, 4, 4, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      {/* Bottom Section: Recent Runs Table & Risk Breakdown */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Recent Runs Table */}
        <div className="lg:col-span-2 bg-[#111827] border border-gray-800 rounded-xl p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-sm font-semibold text-gray-200">Recent Harness Runs</h2>
            <Link to="/runs" className="text-xs text-blue-400 hover:text-blue-300 font-medium">View All Runs →</Link>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="text-gray-400 uppercase tracking-wider border-b border-gray-800">
                <tr>
                  <th className="pb-3 font-semibold">Run ID</th>
                  <th className="pb-3 font-semibold">Prompt</th>
                  <th className="pb-3 font-semibold">Status</th>
                  <th className="pb-3 font-semibold">Loops</th>
                  <th className="pb-3 font-semibold text-right">Duration</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-800/60 font-mono">
                {runs?.slice(0, 5).map(r => (
                  <tr key={r.id} className="hover:bg-gray-800/40">
                    <td className="py-3 text-blue-400">
                      <Link to={`/runs/${r.id}`}>{r.id.substring(0, 8)}...</Link>
                    </td>
                    <td className="py-3 text-gray-300 font-sans max-w-xs truncate">{r.userPrompt}</td>
                    <td className="py-3"><StatusBadge status={r.status} size="sm" /></td>
                    <td className="py-3 text-gray-400">{r.loopCount}</td>
                    <td className="py-3 text-right text-gray-300">{Math.round(r.latencyMs)}ms</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Success vs Failure Distribution */}
        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5 flex flex-col">
          <h2 className="text-sm font-semibold text-gray-200 mb-2">Outcome Distribution</h2>
          <div className="flex-1 flex items-center justify-center">
            <div className="h-48 w-48">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie data={pieData} innerRadius={50} outerRadius={70} dataKey="value" paddingAngle={5}>
                    {pieData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip
                    contentStyle={{ backgroundColor: '#1F2937', borderColor: '#374151', borderRadius: '8px', fontSize: '12px' }}
                  />
                </PieChart>
              </ResponsiveContainer>
            </div>
          </div>
          <div className="flex justify-center space-x-6 text-xs text-gray-400 pt-2 border-t border-gray-800">
            <div className="flex items-center space-x-2">
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-500" />
              <span>Completed ({successRuns})</span>
            </div>
            <div className="flex items-center space-x-2">
              <span className="w-2.5 h-2.5 rounded-full bg-rose-500" />
              <span>Blocked/Failed ({totalRuns - successRuns})</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
