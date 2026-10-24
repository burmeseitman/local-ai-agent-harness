import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { Settings, Shield, Cpu, Database, Sliders, CheckCircle2, AlertCircle } from 'lucide-react';

export const SettingsPage: React.FC = () => {
  const { data: config, isLoading: configLoading } = useQuery({
    queryKey: ['config'],
    queryFn: api.getConfig,
  });

  const { data: health, isLoading: healthLoading } = useQuery({
    queryKey: ['health'],
    queryFn: api.getHealth,
  });

  return (
    <div className="p-6 max-w-5xl mx-auto w-full space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white flex items-center space-x-2">
            <Settings className="w-5 h-5 text-gray-400" />
            <span>Harness Configuration & Security Policies</span>
          </h1>
          <p className="text-sm text-gray-400 mt-1">
            Review live local AI runtime settings, security guardrails, and environment status.
          </p>
        </div>
        <div className="flex items-center space-x-2 text-xs">
          <span className="text-gray-400">System Health:</span>
          {healthLoading ? (
            <span className="text-gray-500 font-mono">Checking...</span>
          ) : health?.status === 'HEALTHY' ? (
            <span className="flex items-center text-emerald-400 font-semibold">
              <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> HEALTHY
            </span>
          ) : (
            <span className="flex items-center text-amber-400 font-semibold">
              <AlertCircle className="w-3.5 h-3.5 mr-1" /> {health?.status ?? 'DEGRADED'}
            </span>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Model Provider Config */}
        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5 space-y-4">
          <div className="flex items-center space-x-2 border-b border-gray-800 pb-3">
            <Cpu className="w-4 h-4 text-blue-400" />
            <h2 className="text-sm font-semibold text-white">AI Model Provider</h2>
          </div>

          <div className="space-y-3 text-xs">
            <div>
              <span className="text-gray-400 font-mono">AI_PROVIDER</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                {configLoading ? 'Loading...' : config?.aiProvider ?? 'ollama'}
              </p>
            </div>
            <div>
              <span className="text-gray-400 font-mono">OLLAMA_BASE_URL</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                {configLoading ? 'Loading...' : config?.ollamaBaseUrl ?? 'http://localhost:11434'}
              </p>
            </div>
            <div>
              <span className="text-gray-400 font-mono">ACTIVE_MODEL</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                {configLoading ? 'Loading...' : config?.modelName ?? 'llama3.2'}
              </p>
            </div>
          </div>
        </div>

        {/* Security Guardrails */}
        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5 space-y-4">
          <div className="flex items-center space-x-2 border-b border-gray-800 pb-3">
            <Shield className="w-4 h-4 text-emerald-400" />
            <h2 className="text-sm font-semibold text-white">Security & Guardrails</h2>
          </div>

          <div className="space-y-2.5 text-xs">
            <div className="flex items-center justify-between p-2 bg-gray-900 rounded border border-gray-800">
              <span className="text-gray-300">Auto Execute LOW Risk</span>
              <span className="text-emerald-400 font-mono font-bold">
                {config?.autoExecuteLowRisk !== false ? 'ENABLED' : 'DISABLED'}
              </span>
            </div>
            <div className="flex items-center justify-between p-2 bg-gray-900 rounded border border-gray-800">
              <span className="text-gray-300">Auto Execute MEDIUM Risk</span>
              <span className="text-emerald-400 font-mono font-bold">
                {config?.autoExecuteMediumRisk !== false ? 'AUDITED' : 'DISABLED'}
              </span>
            </div>
            <div className="flex items-center justify-between p-2 bg-gray-900 rounded border border-gray-800">
              <span className="text-gray-300">Block HIGH & CRITICAL Risk</span>
              <span className="text-rose-400 font-mono font-bold">
                {config?.blockCriticalRisk !== false ? 'ENFORCED' : 'OFF'}
              </span>
            </div>
            <div className="flex items-center justify-between p-2 bg-gray-900 rounded border border-gray-800">
              <span className="text-gray-300">Prompt & Code Injection Filter</span>
              <span className="text-emerald-400 font-mono font-bold">ACTIVE</span>
            </div>
          </div>
        </div>

        {/* Runtime Limits */}
        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5 space-y-4">
          <div className="flex items-center space-x-2 border-b border-gray-800 pb-3">
            <Sliders className="w-4 h-4 text-purple-400" />
            <h2 className="text-sm font-semibold text-white">Execution Loop Limits</h2>
          </div>

          <div className="space-y-3 text-xs">
            <div>
              <span className="text-gray-400">Max Loop Iterations</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                {config?.maxIterations ?? 10} (Hard guardrail against runaway loops)
              </p>
            </div>
            <div>
              <span className="text-gray-400">Tool Execution Timeout</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                {config?.toolTimeoutSeconds ?? 30} seconds (CancellationToken cancellation)
              </p>
            </div>
            <div>
              <span className="text-gray-400">Max Tool Result Size</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                {(config?.maxResultBytes ?? 65536).toLocaleString()} bytes (Output validation limit)
              </p>
            </div>
          </div>
        </div>

        {/* Storage Architecture */}
        <div className="bg-[#111827] border border-gray-800 rounded-xl p-5 space-y-4">
          <div className="flex items-center space-x-2 border-b border-gray-800 pb-3">
            <Database className="w-4 h-4 text-cyan-400" />
            <h2 className="text-sm font-semibold text-white">Storage & State Engine</h2>
          </div>

          <div className="space-y-3 text-xs">
            <div>
              <span className="text-gray-400 font-mono">DATABASE_PROVIDER</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                {config?.databaseProvider ?? 'sqlite'}
              </p>
            </div>
            <div>
              <span className="text-gray-400 font-mono">CONNECTION_STRING</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800 truncate">
                {config?.connectionString ?? 'Data Source=secure_harness.db'}
              </p>
            </div>
            <div>
              <span className="text-gray-400">Database Status</span>
              <p className="text-gray-300 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                {health?.components?.database ?? 'CONNECTED'}
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
