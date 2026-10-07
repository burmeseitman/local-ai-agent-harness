import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { AgentStep, AgentResponse } from '../types';
import { StatusBadge } from '../components/StatusBadge';
import {
  Send,
  Terminal,
  Activity,
  Layers,
  Wrench,
  Database,
  ShieldAlert,
  Play,
  RotateCcw,
  CheckCircle,
  AlertTriangle,
  Info
} from 'lucide-react';

export const AgentConsolePage: React.FC = () => {
  const [prompt, setPrompt] = useState('');
  const [isRunning, setIsRunning] = useState(false);
  const [conversation, setConversation] = useState<Array<{ role: string; content: string; meta?: any }>>([
    {
      role: 'assistant',
      content: 'Welcome to the Secure Local AI Agent Harness. Select a demo scenario below or enter any request to inspect how the harness coordinates planning, security policies, tool execution, and memory.',
    },
  ]);
  const [activeSteps, setActiveSteps] = useState<AgentStep[]>([]);
  const [selectedStep, setSelectedStep] = useState<AgentStep | null>(null);

  // Queries for inspector panel
  const { data: tools } = useQuery({ queryKey: ['tools'], queryFn: api.getTools });
  const { data: memories } = useQuery({ queryKey: ['memories'], queryFn: () => api.getMemories() });

  const handleSend = async (userPromptText?: string) => {
    const textToSend = userPromptText || prompt;
    if (!textToSend.trim() || isRunning) return;

    setPrompt('');
    setIsRunning(true);
    setActiveSteps([]);
    setSelectedStep(null);

    // Append user message
    setConversation(prev => [...prev, { role: 'user', content: textToSend }]);

    try {
      // Connect to SSE stream endpoint for real-time live event streaming
      const encodedPrompt = encodeURIComponent(textToSend);
      const eventSource = new EventSource(`/api/agent/stream?prompt=${encodedPrompt}`);

      eventSource.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);

          if (data.type === 'step_update' && data.step) {
            setActiveSteps(prev => {
              const existingIdx = prev.findIndex(s => s.stepIndex === data.step.stepIndex);
              if (existingIdx >= 0) {
                const next = [...prev];
                next[existingIdx] = data.step;
                return next;
              }
              return [...prev, data.step];
            });
          } else if (data.type === 'run_completed' && data.response) {
            const resp: AgentResponse = data.response;
            eventSource.close();
            setIsRunning(false);

            setConversation(prev => [
              ...prev,
              { role: 'assistant', content: resp.finalAnswer || resp.errorMessage || 'Execution finished.' }
            ]);
            setActiveSteps(resp.steps);
          }
        } catch {
          // Handled
        }
      };

      eventSource.onerror = async () => {
        eventSource.close();
        // Fallback to standard HTTP execution
        try {
          const resp = await api.runAgent({ prompt: textToSend });
          setActiveSteps(resp.steps);
          setConversation(prev => [
            ...prev,
            { role: 'assistant', content: resp.finalAnswer || resp.errorMessage || 'Execution completed.' }
          ]);
        } catch (err: any) {
          setConversation(prev => [
            ...prev,
            { role: 'assistant', content: `Execution Error: ${err.message}` }
          ]);
        } finally {
          setIsRunning(false);
        }
      };
    } catch (err: any) {
      setConversation(prev => [
        ...prev,
        { role: 'assistant', content: `Harness Error: ${err.message}` }
      ]);
      setIsRunning(false);
    }
  };

  const handlePreset = (presetText: string) => {
    setPrompt(presetText);
    handleSend(presetText);
  };

  return (
    <div className="flex flex-col h-full bg-[#0b0f17]">
      {/* Top Banner: Demo Scenarios Selector */}
      <div className="px-6 py-3 border-b border-gray-800 bg-[#0e1422] flex items-center justify-between">
        <div className="flex items-center space-x-2">
          <Play className="w-4 h-4 text-blue-400" />
          <span className="text-xs font-semibold text-gray-300 uppercase tracking-wider">Demo Scenarios:</span>
        </div>
        <div className="flex items-center space-x-2 overflow-x-auto text-xs">
          <button
            onClick={() => handlePreset('Is the server healthy?')}
            className="px-2.5 py-1 bg-gray-800 hover:bg-gray-700 text-gray-200 rounded border border-gray-700 whitespace-nowrap transition"
          >
            1. Server Health
          </button>
          <button
            onClick={() => handlePreset('Check the server and summarize any issues.')}
            className="px-2.5 py-1 bg-gray-800 hover:bg-gray-700 text-gray-200 rounded border border-gray-700 whitespace-nowrap transition"
          >
            2. Summarize Issues
          </button>
          <button
            onClick={() => handlePreset('Search the documentation for database connection failures.')}
            className="px-2.5 py-1 bg-gray-800 hover:bg-gray-700 text-gray-200 rounded border border-gray-700 whitespace-nowrap transition"
          >
            3. Doc Search
          </button>
          <button
            onClick={() => handlePreset('Find previous incidents related to database connectivity.')}
            className="px-2.5 py-1 bg-gray-800 hover:bg-gray-700 text-gray-200 rounded border border-gray-700 whitespace-nowrap transition"
          >
            4. Incidents
          </button>
          <button
            onClick={() => handlePreset('Test flaky service with exponential retry.')}
            className="px-2.5 py-1 bg-amber-950/40 hover:bg-amber-900/40 text-amber-300 rounded border border-amber-800/60 whitespace-nowrap transition"
          >
            5. Flaky Retry
          </button>
          <button
            onClick={() => handlePreset('Delete the production server cluster right now.')}
            className="px-2.5 py-1 bg-rose-950/40 hover:bg-rose-900/40 text-rose-300 rounded border border-rose-800/60 whitespace-nowrap transition"
          >
            6. Blocked Policy
          </button>
        </div>
      </div>

      {/* 3-Column Layout */}
      <div className="flex-1 grid grid-cols-12 overflow-hidden">
        {/* COLUMN 1: Conversation (Left, 4 cols) */}
        <div className="col-span-12 md:col-span-4 border-r border-gray-800 flex flex-col h-full bg-[#0d121c]">
          <div className="p-3 border-b border-gray-800 flex items-center justify-between">
            <span className="text-xs font-semibold text-gray-300 uppercase tracking-wider flex items-center">
              <Terminal className="w-3.5 h-3.5 mr-1.5 text-blue-400" />
              Conversation Stream
            </span>
            <button
              onClick={() => {
                setConversation([]);
                setActiveSteps([]);
              }}
              className="text-gray-400 hover:text-gray-200 text-xs flex items-center"
            >
              <RotateCcw className="w-3 h-3 mr-1" /> Clear
            </button>
          </div>

          <div className="flex-1 p-4 overflow-y-auto space-y-3">
            {conversation.map((msg, i) => (
              <div
                key={i}
                className={`p-3 rounded-lg text-xs leading-relaxed ${
                  msg.role === 'user'
                    ? 'bg-blue-950/40 border border-blue-800/50 text-blue-100 ml-4'
                    : 'bg-gray-900/80 border border-gray-800 text-gray-200 mr-4'
                }`}
              >
                <div className="flex items-center justify-between text-[10px] text-gray-400 uppercase font-mono mb-1.5">
                  <span>{msg.role === 'user' ? '👤 User' : '🤖 Harness Agent'}</span>
                </div>
                <div className="whitespace-pre-wrap">{msg.content}</div>
              </div>
            ))}
            {isRunning && (
              <div className="flex items-center space-x-2 text-xs text-blue-400 p-2 animate-pulse font-mono">
                <Activity className="w-3.5 h-3.5 animate-spin" />
                <span>Harness loop active (Reasoning & Evaluating)...</span>
              </div>
            )}
          </div>

          {/* Prompt Input Form */}
          <div className="p-3 border-t border-gray-800 bg-[#0e1422]">
            <form
              onSubmit={(e) => {
                e.preventDefault();
                handleSend();
              }}
              className="flex space-x-2"
            >
              <input
                type="text"
                placeholder="Ask agent or instruct a task..."
                value={prompt}
                onChange={(e) => setPrompt(e.target.value)}
                disabled={isRunning}
                className="flex-1 bg-gray-900 border border-gray-700 rounded-lg px-3 py-2 text-xs text-gray-100 placeholder-gray-500 focus:outline-none focus:border-blue-500 font-mono"
              />
              <button
                type="submit"
                disabled={isRunning || !prompt.trim()}
                className="px-3.5 py-2 bg-blue-600 hover:bg-blue-500 disabled:bg-gray-800 disabled:text-gray-600 text-white rounded-lg text-xs font-medium transition flex items-center"
              >
                <Send className="w-3.5 h-3.5" />
              </button>
            </form>
          </div>
        </div>

        {/* COLUMN 2: Execution Timeline (Center, 5 cols) */}
        <div className="col-span-12 md:col-span-5 border-r border-gray-800 flex flex-col h-full bg-[#0b0f17]">
          <div className="p-3 border-b border-gray-800 flex items-center justify-between bg-[#0e1422]">
            <span className="text-xs font-semibold text-gray-300 uppercase tracking-wider flex items-center">
              <Activity className="w-3.5 h-3.5 mr-1.5 text-emerald-400" />
              Harness Execution Timeline
            </span>
            <span className="text-[11px] text-gray-400 font-mono">
              {activeSteps.length} step(s)
            </span>
          </div>

          <div className="flex-1 p-4 overflow-y-auto space-y-2.5">
            {activeSteps.length === 0 ? (
              <div className="h-full flex flex-col items-center justify-center text-center p-6 text-gray-500">
                <Layers className="w-10 h-10 mb-2 opacity-30 text-blue-400" />
                <p className="text-xs">No active execution cycle.</p>
                <p className="text-[11px] text-gray-600 mt-1 max-w-xs">
                  Run a task to see PLAN, ACT, OBSERVE, EVALUATE, RETRY, and COMPLETE nodes in real time.
                </p>
              </div>
            ) : (
              activeSteps.map((step) => {
                const isSelected = selectedStep?.id === step.id;
                return (
                  <div
                    key={step.id || step.stepIndex}
                    onClick={() => setSelectedStep(step)}
                    className={`p-3 rounded-lg border cursor-pointer transition-all ${
                      isSelected
                        ? 'bg-blue-950/30 border-blue-500 ring-1 ring-blue-500/30'
                        : 'bg-[#111827] border-gray-800 hover:border-gray-700'
                    }`}
                  >
                    <div className="flex items-center justify-between mb-1.5">
                      <div className="flex items-center space-x-2">
                        <span className={`text-[10px] font-mono px-1.5 py-0.5 rounded font-bold ${
                          step.type === 'PLAN' ? 'bg-purple-900/60 text-purple-300 border border-purple-700/60' :
                          step.type === 'ACT' ? 'bg-blue-900/60 text-blue-300 border border-blue-700/60' :
                          step.type === 'OBSERVE' ? 'bg-amber-900/60 text-amber-300 border border-amber-700/60' :
                          step.type === 'EVALUATE' ? 'bg-cyan-900/60 text-cyan-300 border border-cyan-700/60' :
                          step.type === 'COMPLETE' ? 'bg-emerald-900/60 text-emerald-300 border border-emerald-700/60' :
                          'bg-rose-900/60 text-rose-300 border border-rose-700/60'
                        }`}>
                          {step.type}
                        </span>
                        <span className="text-xs font-medium text-gray-200">{step.title}</span>
                      </div>
                      <div className="flex items-center space-x-2 font-mono text-[11px]">
                        <span className="text-gray-400">{Math.round(step.durationMs)}ms</span>
                        <StatusBadge status={step.status} size="sm" />
                      </div>
                    </div>
                    <p className="text-xs text-gray-400 line-clamp-2">{step.description}</p>
                    {step.outputPayload && (
                      <div className="mt-2 text-[10px] font-mono bg-black/40 p-1.5 rounded border border-gray-800/80 text-gray-300 truncate">
                        Output: {step.outputPayload}
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* COLUMN 3: Context / Tools / Step Inspector (Right, 3 cols) */}
        <div className="col-span-12 md:col-span-3 flex flex-col h-full bg-[#0d121c]">
          <div className="p-3 border-b border-gray-800 bg-[#0e1422]">
            <span className="text-xs font-semibold text-gray-300 uppercase tracking-wider flex items-center">
              <Layers className="w-3.5 h-3.5 mr-1.5 text-purple-400" />
              Inspector & Knowledge
            </span>
          </div>

          <div className="flex-1 p-4 overflow-y-auto space-y-4 text-xs">
            {/* Step Detail Inspector if clicked */}
            {selectedStep ? (
              <div className="bg-[#111827] border border-blue-800/50 rounded-lg p-3 space-y-2">
                <div className="flex items-center justify-between pb-2 border-b border-gray-800">
                  <span className="font-semibold text-blue-400 font-mono text-[11px]">
                    Step #{selectedStep.stepIndex} ({selectedStep.type})
                  </span>
                  <StatusBadge status={selectedStep.status} size="sm" />
                </div>
                <div>
                  <span className="text-gray-400 text-[10px] uppercase font-mono">Title</span>
                  <p className="text-gray-200 font-medium">{selectedStep.title}</p>
                </div>
                <div>
                  <span className="text-gray-400 text-[10px] uppercase font-mono">Duration</span>
                  <p className="text-gray-300 font-mono">{Math.round(selectedStep.durationMs)} ms</p>
                </div>
                {selectedStep.inputPayload && (
                  <div>
                    <span className="text-gray-400 text-[10px] uppercase font-mono">Input / Arguments</span>
                    <pre className="mt-1 p-2 bg-black/60 rounded text-[11px] font-mono text-gray-300 overflow-x-auto whitespace-pre-wrap border border-gray-800">
                      {selectedStep.inputPayload}
                    </pre>
                  </div>
                )}
                {selectedStep.outputPayload && (
                  <div>
                    <span className="text-gray-400 text-[10px] uppercase font-mono">Validated Output</span>
                    <pre className="mt-1 p-2 bg-black/60 rounded text-[11px] font-mono text-emerald-300/90 overflow-x-auto whitespace-pre-wrap border border-gray-800">
                      {selectedStep.outputPayload}
                    </pre>
                  </div>
                )}
              </div>
            ) : null}

            {/* Available Tools Catalog */}
            <div className="space-y-2">
              <span className="text-[11px] font-semibold text-gray-400 uppercase tracking-wider flex items-center">
                <Wrench className="w-3 h-3 mr-1 text-amber-400" /> Available Tools ({tools?.length || 0})
              </span>
              <div className="space-y-1.5">
                {tools?.map((t) => (
                  <div key={t.name} className="p-2 bg-gray-900/60 border border-gray-800 rounded">
                    <div className="flex items-center justify-between">
                      <span className="font-mono text-blue-400 font-medium text-[11px]">{t.name}</span>
                      <StatusBadge status={t.riskLevel} size="sm" />
                    </div>
                    <p className="text-gray-400 text-[11px] mt-1 line-clamp-1">{t.description}</p>
                  </div>
                ))}
              </div>
            </div>

            {/* Retained Memory */}
            <div className="space-y-2 pt-2 border-t border-gray-800">
              <span className="text-[11px] font-semibold text-gray-400 uppercase tracking-wider flex items-center">
                <Database className="w-3 h-3 mr-1 text-cyan-400" /> Active Memories ({memories?.length || 0})
              </span>
              <div className="space-y-1.5">
                {memories?.slice(0, 4).map((m) => (
                  <div key={m.id} className="p-2 bg-gray-900/60 border border-gray-800 rounded text-[11px]">
                    <div className="flex items-center justify-between text-gray-400 text-[10px] mb-1">
                      <span className="font-mono uppercase">{m.type}</span>
                      <span>★ {m.importance}</span>
                    </div>
                    <p className="text-gray-300 line-clamp-2">{m.content}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
