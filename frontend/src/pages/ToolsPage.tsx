import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { ToolItem } from '../types';
import { StatusBadge } from '../components/StatusBadge';
import { Wrench, Shield, Play, Clock, CheckCircle2, AlertTriangle, Layers } from 'lucide-react';

export const ToolsPage: React.FC = () => {
  const [selectedTool, setSelectedTool] = useState<ToolItem | null>(null);
  const [testArgs, setTestArgs] = useState('{}');
  const [testResult, setTestResult] = useState<any>(null);
  const [isExecuting, setIsExecuting] = useState(false);

  const { data: tools, isLoading, refetch } = useQuery({
    queryKey: ['tools'],
    queryFn: api.getTools,
  });

  const handleSelectTool = (t: ToolItem) => {
    setSelectedTool(t);
    setTestArgs('{}');
    setTestResult(null);
  };

  const handleRunManualTest = async () => {
    if (!selectedTool) return;
    setIsExecuting(true);
    setTestResult(null);

    try {
      const parsedArgs = JSON.parse(testArgs);
      const res = await api.executeToolManually(selectedTool.name, parsedArgs);
      setTestResult(res);
      refetch();
    } catch (err: any) {
      setTestResult({
        isSuccess: false,
        errorMessage: err.message,
        status: 'Error'
      });
    } finally {
      setIsExecuting(false);
    }
  };

  return (
    <div className="p-6 max-w-7xl mx-auto w-full space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-xl font-bold text-white flex items-center space-x-2">
          <Wrench className="w-5 h-5 text-blue-400" />
          <span>Harness Tool Registry & Security Policies</span>
        </h1>
        <p className="text-sm text-gray-400 mt-1">
          Catalog of registered tools, input/output contracts, and policy engine permissions.
        </p>
      </div>

      <div className="grid grid-cols-12 gap-6">
        {/* Tools List (7 cols) */}
        <div className="col-span-12 lg:col-span-7 space-y-3">
          {isLoading ? (
            <div className="text-gray-400 text-sm p-4">Loading tools registry...</div>
          ) : (
            tools?.map((tool) => {
              const isSelected = selectedTool?.name === tool.name;
              return (
                <div
                  key={tool.name}
                  onClick={() => handleSelectTool(tool)}
                  className={`p-4 rounded-xl border cursor-pointer transition ${
                    isSelected
                      ? 'bg-blue-950/20 border-blue-500 shadow-md shadow-blue-500/10'
                      : 'bg-[#111827] border-gray-800 hover:border-gray-700'
                  }`}
                >
                  <div className="flex items-center justify-between mb-2">
                    <div className="flex items-center space-x-2">
                      <span className="font-mono font-semibold text-blue-400 text-sm">{tool.name}</span>
                      <StatusBadge status={tool.riskLevel} size="sm" />
                    </div>
                    <div className="flex items-center space-x-3 text-xs text-gray-400 font-mono">
                      <span>{tool.executionCount} calls</span>
                      <span>~{tool.averageLatencyMs}ms</span>
                    </div>
                  </div>
                  <p className="text-xs text-gray-300 leading-relaxed">{tool.description}</p>
                </div>
              );
            })
          )}
        </div>

        {/* Tool Detail & Manual Harness Test Panel (5 cols) */}
        <div className="col-span-12 lg:col-span-5 bg-[#111827] border border-gray-800 rounded-xl p-5 flex flex-col space-y-4">
          {selectedTool ? (
            <>
              <div className="flex items-center justify-between pb-3 border-b border-gray-800">
                <div>
                  <h2 className="text-sm font-bold text-white font-mono">{selectedTool.name}</h2>
                  <span className="text-xs text-gray-400">Risk: {selectedTool.riskLevel}</span>
                </div>
                <StatusBadge status={selectedTool.isAllowed ? 'ALLOWED' : 'BLOCKED'} size="sm" />
              </div>

              {/* Input Schema */}
              <div>
                <span className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Input Contract Schema</span>
                <pre className="mt-1 p-2.5 bg-black/60 rounded-lg text-xs font-mono text-gray-300 overflow-x-auto max-h-36 border border-gray-800">
                  {JSON.stringify(selectedTool.inputSchema, null, 2)}
                </pre>
              </div>

              {/* Output Schema */}
              <div>
                <span className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Output Contract Schema</span>
                <pre className="mt-1 p-2.5 bg-black/60 rounded-lg text-xs font-mono text-gray-300 overflow-x-auto max-h-36 border border-gray-800">
                  {JSON.stringify(selectedTool.outputSchema, null, 2)}
                </pre>
              </div>

              {/* Manual Sandbox Tester */}
              <div className="pt-2 border-t border-gray-800 space-y-2">
                <span className="text-xs font-semibold text-gray-300 uppercase tracking-wider flex items-center">
                  <Play className="w-3.5 h-3.5 mr-1 text-emerald-400" /> Test Execution via Harness
                </span>
                <textarea
                  value={testArgs}
                  onChange={(e) => setTestArgs(e.target.value)}
                  placeholder="{}"
                  rows={2}
                  className="w-full bg-black/60 border border-gray-700 rounded-lg p-2 text-xs font-mono text-gray-200 focus:outline-none focus:border-blue-500"
                />
                <button
                  onClick={handleRunManualTest}
                  disabled={isExecuting}
                  className="w-full py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-xs font-medium transition"
                >
                  {isExecuting ? 'Running through Harness Policy & Validator...' : 'Execute Tool Call'}
                </button>
              </div>

              {/* Result Preview */}
              {testResult && (
                <div className="p-3 bg-black/80 rounded-lg border border-gray-800 text-xs font-mono space-y-1">
                  <div className="flex items-center justify-between">
                    <span className="text-gray-400">Result Status:</span>
                    <StatusBadge status={testResult.status || (testResult.isSuccess ? 'Success' : 'Failed')} size="sm" />
                  </div>
                  {testResult.outputJson && (
                    <div>
                      <span className="text-gray-400">Output:</span>
                      <pre className="text-emerald-400 mt-1 overflow-x-auto whitespace-pre-wrap">{testResult.outputJson}</pre>
                    </div>
                  )}
                  {testResult.errorMessage && (
                    <div>
                      <span className="text-rose-400">Error:</span>
                      <p className="text-rose-300 mt-1">{testResult.errorMessage}</p>
                    </div>
                  )}
                </div>
              )}
            </>
          ) : (
            <div className="h-full flex flex-col items-center justify-center text-center text-gray-500 p-8">
              <Layers className="w-8 h-8 mb-2 opacity-40 text-blue-400" />
              <p className="text-xs">Select any tool to view schemas and run manual test execution.</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
