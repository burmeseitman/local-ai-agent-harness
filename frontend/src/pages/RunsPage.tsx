import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api } from '../services/api';
import { StatusBadge } from '../components/StatusBadge';
import { PlaySquare, Clock, Repeat, ArrowRight } from 'lucide-react';

export const RunsPage: React.FC = () => {
  const { data: runs, isLoading } = useQuery({
    queryKey: ['runs'],
    queryFn: api.getAllRuns,
    refetchInterval: 5000,
  });

  return (
    <div className="p-6 max-w-7xl mx-auto w-full space-y-6">
      <div>
        <h1 className="text-xl font-bold text-white flex items-center space-x-2">
          <PlaySquare className="w-5 h-5 text-blue-400" />
          <span>Agent Execution Runs</span>
        </h1>
        <p className="text-sm text-gray-400 mt-1">
          Historical record of all autonomous reasoning loops and harness runs.
        </p>
      </div>

      <div className="bg-[#111827] border border-gray-800 rounded-xl overflow-hidden">
        <table className="w-full text-left text-xs">
          <thead className="bg-[#0e1422] text-gray-400 uppercase tracking-wider border-b border-gray-800">
            <tr>
              <th className="p-3.5 font-semibold">Run ID</th>
              <th className="p-3.5 font-semibold">User Prompt</th>
              <th className="p-3.5 font-semibold">Status</th>
              <th className="p-3.5 font-semibold">Loops</th>
              <th className="p-3.5 font-semibold">Latency</th>
              <th className="p-3.5 font-semibold">Started</th>
              <th className="p-3.5 font-semibold text-right">Details</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-800/60 font-mono">
            {isLoading ? (
              <tr>
                <td colSpan={7} className="p-6 text-center text-gray-500 font-sans">
                  Loading agent execution history...
                </td>
              </tr>
            ) : runs?.length === 0 ? (
              <tr>
                <td colSpan={7} className="p-6 text-center text-gray-500 font-sans">
                  No execution runs found.
                </td>
              </tr>
            ) : (
              runs?.map((run) => (
                <tr key={run.id} className="hover:bg-gray-800/40 transition">
                  <td className="p-3.5 text-blue-400 font-bold">
                    <Link to={`/runs/${run.id}`}>{run.id.substring(0, 10)}...</Link>
                  </td>
                  <td className="p-3.5 text-gray-200 font-sans max-w-md truncate">
                    {run.userPrompt}
                  </td>
                  <td className="p-3.5">
                    <StatusBadge status={run.status} size="sm" />
                  </td>
                  <td className="p-3.5 text-gray-400">{run.loopCount}</td>
                  <td className="p-3.5 text-gray-300">{Math.round(run.latencyMs)}ms</td>
                  <td className="p-3.5 text-gray-500">
                    {new Date(run.startTime).toLocaleTimeString()}
                  </td>
                  <td className="p-3.5 text-right font-sans">
                    <Link
                      to={`/runs/${run.id}`}
                      className="inline-flex items-center text-xs text-blue-400 hover:text-blue-300 font-medium"
                    >
                      Graph View <ArrowRight className="w-3.5 h-3.5 ml-1" />
                    </Link>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
