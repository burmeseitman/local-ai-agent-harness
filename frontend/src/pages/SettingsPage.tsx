import React from 'react';
import { Settings, Shield, Cpu, Database, Sliders, CheckCircle } from 'lucide-react';

export const SettingsPage: React.FC = () => {
  return (
    <div className="p-6 max-w-5xl mx-auto w-full space-y-6">
      <div>
        <h1 className="text-xl font-bold text-white flex items-center space-x-2">
          <Settings className="w-5 h-5 text-gray-400" />
          <span>Harness Configuration & Security Policies</span>
        </h1>
        <p className="text-sm text-gray-400 mt-1">
          Review local AI runtime settings, security guardrails, and environment variables.
        </p>
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
                ollama (with fallback simulator)
              </p>
            </div>
            <div>
              <span className="text-gray-400 font-mono">OLLAMA_BASE_URL</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                http://localhost:11434
              </p>
            </div>
            <div>
              <span className="text-gray-400 font-mono">OLLAMA_MODEL</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                llama3.2 (Configurable in .env)
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
              <span className="text-emerald-400 font-mono font-bold">ENABLED</span>
            </div>
            <div className="flex items-center justify-between p-2 bg-gray-900 rounded border border-gray-800">
              <span className="text-gray-300">Auto Execute MEDIUM Risk</span>
              <span className="text-emerald-400 font-mono font-bold">AUDITED</span>
            </div>
            <div className="flex items-center justify-between p-2 bg-gray-900 rounded border border-gray-800">
              <span className="text-gray-300">Block HIGH & CRITICAL Risk</span>
              <span className="text-rose-400 font-mono font-bold">ENFORCED</span>
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
                10 (Hard guardrail against runaway loops)
              </p>
            </div>
            <div>
              <span className="text-gray-400">Tool Execution Timeout</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                30 seconds (CancellationToken cancellation)
              </p>
            </div>
            <div>
              <span className="text-gray-400">Max Tool Result Size</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                65,536 bytes (64 KB output validation limit)
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
                SQLite (default local development)
              </p>
            </div>
            <div>
              <span className="text-gray-400 font-mono">CONNECTION_STRING</span>
              <p className="font-mono text-gray-200 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800 truncate">
                Data Source=secure_harness.db
              </p>
            </div>
            <div>
              <span className="text-gray-400">Production Compatibility</span>
              <p className="text-gray-300 mt-0.5 p-2 bg-gray-900 rounded border border-gray-800">
                Clean Architecture EF Core design ready for PostgreSQL and Redis memory cache.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
