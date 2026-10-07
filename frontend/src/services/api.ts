import {
  AgentRequest,
  AgentResponse,
  AgentRun,
  ToolItem,
  MemoryItem,
  ExecutionTrace,
  HarnessMetrics,
  Conversation
} from '../types';

const BASE_URL = import.meta.env.VITE_API_BASE_URL || '';

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${url}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(options?.headers || {}),
    },
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(`API Error ${response.status}: ${errorText || response.statusText}`);
  }

  return response.json();
}

export const api = {
  // Agent Execution
  runAgent: (request: AgentRequest): Promise<AgentResponse> =>
    fetchJson<AgentResponse>('/api/agent/run', {
      method: 'POST',
      body: JSON.stringify(request),
    }),

  getAllRuns: (): Promise<AgentRun[]> =>
    fetchJson<AgentRun[]>('/api/agent/runs'),

  getRunById: (runId: string): Promise<AgentRun> =>
    fetchJson<AgentRun>(`/api/agent/runs/${runId}`),

  // Tools
  getTools: (): Promise<ToolItem[]> =>
    fetchJson<ToolItem[]>('/api/tools'),

  getToolByName: (name: string): Promise<ToolItem> =>
    fetchJson<ToolItem>(`/api/tools/${name}`),

  executeToolManually: (name: string, args: any): Promise<any> =>
    fetchJson<any>(`/api/tools/${name}/execute`, {
      method: 'POST',
      body: JSON.stringify(args),
    }),

  // Memory
  getMemories: (type?: string): Promise<MemoryItem[]> =>
    fetchJson<MemoryItem[]>(type ? `/api/memory?type=${type}` : '/api/memory'),

  searchMemory: (query: string): Promise<MemoryItem[]> =>
    fetchJson<MemoryItem[]>(`/api/memory/search?q=${encodeURIComponent(query)}`),

  createMemory: (item: Partial<MemoryItem>): Promise<MemoryItem> =>
    fetchJson<MemoryItem>('/api/memory', {
      method: 'POST',
      body: JSON.stringify(item),
    }),

  deleteMemory: (id: string): Promise<void> =>
    fetch(`${BASE_URL}/api/memory/${id}`, { method: 'DELETE' }).then(() => {}),

  // Observability
  getHealth: (): Promise<{ status: string; service: string; guardrailsActive: boolean }> =>
    fetchJson('/api/health'),

  getMetrics: (): Promise<HarnessMetrics> =>
    fetchJson<HarnessMetrics>('/api/metrics'),

  getTracesForRun: (runId: string): Promise<ExecutionTrace[]> =>
    fetchJson<ExecutionTrace[]>(`/api/traces/${runId}`),

  getAllTraces: (limit = 100): Promise<ExecutionTrace[]> =>
    fetchJson<ExecutionTrace[]>(`/api/traces?limit=${limit}`),

  // Conversations
  getConversations: (): Promise<Conversation[]> =>
    fetchJson<Conversation[]>('/api/conversations'),

  getConversationById: (id: string): Promise<Conversation> =>
    fetchJson<Conversation>(`/api/conversations/${id}`),

  clearConversation: (id: string): Promise<void> =>
    fetch(`${BASE_URL}/api/conversations/${id}`, { method: 'DELETE' }).then(() => {}),
};
