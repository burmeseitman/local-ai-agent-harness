import React, { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { AgentStep } from '../types';
import { StatusBadge } from '../components/StatusBadge';
import {
  ArrowLeft,
  Clock,
  Repeat,
  Layers,
  CheckCircle2,
  AlertOctagon,
  Cpu,
  Shield,
  FileCheck,
  ChevronRight,
  Database
} from 'lucide-react';

export const RunDetailPage: React.FC = () => {
  const { runId } = useParams<{ runId: string }>();
  const [selectedStep, setSelectedStep] = useState<AgentStep | null>(null);

  const { data: run, isLoading, error } = useQuery({
    queryKey: ['run', runId],
    queryFn: () => (runId ? api.getRunById(runId) : Promise.reject('No ID')),
    enabled: !!runId,
  });

  const { data: traces } = useQuery({
    queryKey: ['traces', runId],
    queryFn: () => (runId ? api.getTracesForRun(runId) : Promise.resolve([])),
    enabled: !!runId,
  });

  if (isLoading) {
    return <div className="p-8 text-gray-400 text-sm">Loading execution trace for {runId}...</div>;
  }

  if (error || !run) {
    return (
      <div className="p-8 text-rose-400 text-sm space-y-2">
        <p>Failed to find or load run details.</p>
        <Link to="/runs" className="text-blue-400 underline">Back to runs list</Link>
      </div>
    );
  }

  // Pre-select first step if none selected
  const activeStep = selectedStep || run.steps[0] || null;

  return (
    <div className="p-6 max-w-7xl mx-auto w-full space-y-6">
      {/* Navigation & Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center space-x-3">
          <Link
            to="/runs"
            className="p-2 bg-gray-900 hover:bg-gray-800 text-gray-300 rounded-lg border border-gray-800 transition"
          >
            <ArrowLeft className="w-4 h-4" />
          </Link>
          <div>
            <div className="flex items-center space-x-3">
              <h1 className="text-lg font-bold text-white font-mono">{run.id}</h1>
              <StatusBadge status={run.status} size="sm" />
            </div>
            <p className="text-xs text-gray-400 mt-0.5">
              Executed in conversation <span className="font-mono text-gray-300">{run.conversationId}</span>
            </p>
          </div>
        </div>

        <div className="flex items-center space-x-4 text-xs font-mono bg-[#111827] px-3.5 py-2 rounded-lg border border-gray-800">
          <div className="flex items-center text-gray-300">
            <Clock className="w-3.5 h-3.5 mr-1.5 text-blue-400" />
            <span>{Math.round(run.latencyMs)} ms</span>
          </div>
          <div className="flex items-center text-gray-300">
            <Repeat className="w-3.5 h-3.5 mr-1.5 text-purple-400" />
            <span>{run.loopCount} loops</span>
          </div>
        </div>
      </div>

      {/* User Task Card */}
      <div className="bg-[#111827] border border-gray-800 rounded-xl p-4">
        <span className="text-[10px] text-gray-400 uppercase tracking-wider font-semibold">User Goal / Instruction</span>
        <p className="text-sm font-medium text-gray-100 mt-1">{run.userPrompt}</p>
        {run.finalAnswer && (
          <div className="mt-3 pt-3 border-t border-gray-800/80">
            <span className="text-[10px] text-emerald-400 uppercase tracking-wider font-semibold">Verified Final Output</span>
            <p className="text-xs text-gray-300 mt-1 whitespace-pre-wrap">{run.finalAnswer}</p>
          </div>
        )}
      </div>

      {/* VISUAL EXECUTION GRAPH / PIPELINE */}
      <div className="bg-[#111827] border border-gray-800 rounded-xl p-5 space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-xs font-semibold text-gray-300 uppercase tracking-wider flex items-center">
            <Layers className="w-3.5 h-3.5 mr-1.5 text-blue-400" />
            Visual Execution Flow (Click node to inspect)
          </h2>
          <span className="text-xs text-gray-500 font-mono">{run.steps.length} Nodes</span>
        </div>

        {/* Nodes Flow Bar */}
        <div className="flex items-center overflow-x-auto py-3 space-x-2">
          {run.steps.map((step, idx) => {
            const isSelected = activeStep?.id === step.id;
            return (
              <React.Fragment key={step.id || idx}>
                <button
                  onClick={() => setSelectedStep(step)}
                  className={`flex-shrink-0 px-3.5 py-2.5 rounded-lg border text-left transition-all ${
                    isSelected
                      ? 'bg-blue-950/40 border-blue-500 ring-2 ring-blue-500/30'
                      : 'bg-gray-900/90 border-gray-800 hover:border-gray-700'
                  }`}
                >
                  <div className="flex items-center justify-between space-x-2 text-[10px] font-mono mb-1">
                    <span className="text-gray-400 font-bold">#{step.stepIndex}</span>
                    <span className="text-gray-500">{Math.round(step.durationMs)}ms</span>
                  </div>
                  <div className="text-xs font-semibold text-gray-200 whitespace-nowrap">{step.type}</div>
                  <div className="text-[11px] text-gray-400 truncate max-w-[120px] mt-0.5">{step.title}</div>
                </button>
                {idx < run.steps.length - 1 && (
                  <ChevronRight className="w-4 h-4 text-gray-600 flex-shrink-0" />
                )}
              </React.Fragment>
            );
          })}
        </div>
      </div>

      {/* 2-Column: Step Node Inspector & Traces Log */}
      <div className="grid grid-cols-12 gap-6">
        {/* Node Inspector (7 cols) */}
        <div className="col-span-12 lg:col-span-7 bg-[#111827] border border-gray-800 rounded-xl p-5 space-y-4">
          {activeStep ? (
            <>
              <div className="flex items-center justify-between pb-3 border-b border-gray-800">
                <div className="flex items-center space-x-2">
                  <span className="text-xs font-mono font-bold bg-blue-900/60 text-blue-300 px-2 py-0.5 rounded border border-blue-700/60">
                    {activeStep.type}
                  </span>
                  <h3 className="text-sm font-semibold text-white">{activeStep.title}</h3>
                </div>
                <StatusBadge status={activeStep.status} size="sm" />
              </div>

              <div>
                <span className="text-[10px] text-gray-400 uppercase tracking-wider font-mono">Description</span>
                <p className="text-xs text-gray-200 mt-1">{activeStep.description}</p>
              </div>

              {activeStep.inputPayload && (
                <div>
                  <span className="text-[10px] text-gray-400 uppercase tracking-wider font-mono">Input / Arguments</span>
                  <pre className="mt-1 p-3 bg-black/60 rounded-lg text-xs font-mono text-gray-300 overflow-x-auto whitespace-pre-wrap border border-gray-800">
                    {activeStep.inputPayload}
                  </pre>
                </div>
              )}

              {activeStep.outputPayload && (
                <div>
                  <span className="text-[10px] text-gray-400 uppercase tracking-wider font-mono">Validated Output</span>
                  <pre className="mt-1 p-3 bg-black/60 rounded-lg text-xs font-mono text-emerald-300/90 overflow-x-auto whitespace-pre-wrap border border-gray-800">
                    {activeStep.outputPayload}
                  </pre>
                </div>
              )}

              {activeStep.errorDetails && (
                <div className="p-3 bg-rose-950/40 border border-rose-800/60 rounded-lg text-xs text-rose-300">
                  <span className="font-bold">Error:</span> {activeStep.errorDetails}
                </div>
              )}
            </>
          ) : (
            <p className="text-xs text-gray-500">Select a step node above to inspect details.</p>
          )}
        </div>

        {/* Detailed OpenTelemetry Traces for Run (5 cols) */}
        <div className="col-span-12 lg:col-span-5 bg-[#111827] border border-gray-800 rounded-xl p-5 space-y-3">
          <h3 className="text-xs font-semibold text-gray-300 uppercase tracking-wider flex items-center">
            <Cpu className="w-3.5 h-3.5 mr-1.5 text-emerald-400" />
            Audit & Telemetry Spans ({traces?.length || 0})
          </h3>
          <div className="space-y-2 max-h-[420px] overflow-y-auto pr-1">
            {traces?.length === 0 ? (
              <p className="text-xs text-gray-500">No traces recorded.</p>
            ) : (
              traces?.map((trace) => (
                <div key={trace.traceId + trace.operation + trace.timestamp} className="p-2.5 bg-gray-900/60 border border-gray-800/80 rounded text-xs font-mono">
                  <div className="flex items-center justify-between text-[11px] mb-1">
                    <span className="text-blue-400 font-bold">{trace.operation}</span>
                    <span className="text-gray-500">{Math.round(trace.latencyMs)}ms</span>
                  </div>
                  {trace.details && (
                    <p className="text-[11px] text-gray-400 font-sans truncate">{trace.details}</p>
                  )}
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
