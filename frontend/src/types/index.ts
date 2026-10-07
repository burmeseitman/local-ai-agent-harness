export type ToolRiskLevel = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';
export type ToolExecutionStatus = 'Pending' | 'Executing' | 'Success' | 'Failed' | 'Blocked' | 'AwaitingApproval';
export type PolicyDecisionType = 'ALLOWED' | 'BLOCKED' | 'REQUIRES_APPROVAL';
export type AgentStepType = 'PLAN' | 'ACT' | 'OBSERVE' | 'EVALUATE' | 'RETRY' | 'COMPLETE' | 'FAIL';
export type RunStatus = 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';
export type MemoryType = 'ShortTerm' | 'LongTerm' | 'Episodic' | 'Semantic';
export type MessageRole = 'SYSTEM' | 'USER' | 'TOOL' | 'MEMORY' | 'MODEL_OUTPUT';

export interface AgentStep {
  id: string;
  runId: string;
  stepIndex: number;
  type: AgentStepType;
  title: string;
  description: string;
  timestamp: string;
  durationMs: number;
  status: string;
  inputPayload?: string | null;
  outputPayload?: string | null;
  errorDetails?: string | null;
}

export interface AgentRun {
  id: string;
  conversationId: string;
  userPrompt: string;
  status: RunStatus;
  startTime: string;
  endTime?: string | null;
  latencyMs: number;
  loopCount: number;
  finalAnswer?: string | null;
  errorMessage?: string | null;
  steps: AgentStep[];
}

export interface AgentRequest {
  prompt: string;
  conversationId?: string | null;
  sessionId?: string | null;
  streamEvents?: boolean;
}

export interface AgentResponse {
  runId: string;
  conversationId: string;
  finalAnswer: string;
  status: RunStatus;
  latencyMs: number;
  loopCount: number;
  steps: AgentStep[];
  errorMessage?: string | null;
}

export interface ToolItem {
  name: string;
  description: string;
  riskLevel: ToolRiskLevel;
  inputSchema: any;
  outputSchema: any;
  executionCount: number;
  averageLatencyMs: number;
  isAllowed: boolean;
  recentExecutions?: any[];
}

export interface MemoryItem {
  id: string;
  type: MemoryType;
  content: string;
  createdAt: string;
  updatedAt: string;
  importance: number;
  tags: string[];
  source: string;
}

export interface ExecutionTrace {
  traceId: string;
  runId: string;
  conversationId: string;
  timestamp: string;
  operation: string;
  latencyMs: number;
  details?: string | null;
  status: string;
}

export interface HarnessMetrics {
  totalRuns: number;
  successfulRuns: number;
  failedRuns: number;
  averageLatencyMs: number;
  toolCalls: number;
  toolFailures: number;
  retries: number;
  averageLoopCount: number;
}

export interface Conversation {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messages: AgentMessage[];
}

export interface AgentMessage {
  id: string;
  conversationId: string;
  role: MessageRole;
  content: string;
  createdAt: string;
  metadata?: Record<string, string>;
}
