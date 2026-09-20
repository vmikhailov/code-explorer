/**
 * TypeScript Type Definitions for CodeExplorer WebSocket Protocol
 * Canonical source: proto/code_explorer.proto
 */

export type MessageType =
  // Client -> Server
  | 'HANDSHAKE_REQUEST'
  | 'PING_REQUEST'
  | 'GET_ARCHITECTURE_REQUEST'
  | 'GET_DEPENDENCIES_REQUEST'
  | 'GET_SYMBOL_NEIGHBORHOOD_REQUEST'
  | 'GET_CALL_CHAIN_REQUEST'
  | 'GET_IMPACT_REQUEST'
  | 'EXECUTE_CYPHER_REQUEST'
  | 'TRIGGER_SCAN_REQUEST'
  // Server -> Client responses
  | 'HANDSHAKE_RESPONSE'
  | 'PONG_RESPONSE'
  | 'QUERY_RESPONSE'
  | 'ERROR_RESPONSE'
  // Broadcast events
  | 'GRAPH_PATCH_EVENT'
  | 'SCAN_PROGRESS_EVENT';

// ============================================================================
// Common Graph Models
// ============================================================================

export interface GraphNode {
  id: string;
  kind: string; // 'Project' | 'Class' | 'Function' | 'Endpoint' | 'Table' | 'Database' etc.
  name: string;
  displayName?: string;
  filePath?: string;
  lineStart?: number;
  lineEnd?: number;
  parentId?: string;
  properties?: Record<string, string>;
}

export interface GraphEdge {
  id?: string;
  source: string;
  target: string;
  kind: string; // 'CALLS' | 'DEPENDS_ON' | 'WRITES_TO' | 'READS_FROM' | 'IMPLEMENTS' etc.
  properties?: Record<string, string>;
}

export interface GraphData {
  nodes: GraphNode[];
  edges: GraphEdge[];
  metadata?: Record<string, string>;
}

// ============================================================================
// Request Payloads
// ============================================================================

export interface HandshakeRequest {
  clientVersion: string;
  clientName?: string;
  workspacePath?: string;
}

export interface PingRequest {
  timestamp: number;
}

export interface GetArchitectureRequest {
  projectFilter?: string;
  format?: 'graph' | 'mermaid' | 'c4';
}

export interface GetDependenciesRequest {
  projectName?: string;
  includeIncoming?: boolean;
  includeOutgoing?: boolean;
}

export interface GetSymbolNeighborhoodRequest {
  symbolName: string;
  depth?: number;
}

export interface GetCallChainRequest {
  fromSymbol: string;
  toSymbol?: string;
  maxDepth?: number;
}

export interface GetImpactRequest {
  symbolName: string;
}

export interface ExecuteCypherRequest {
  query: string;
  parameters?: Record<string, unknown>;
}

export interface TriggerScanRequest {
  targetPath?: string;
  clear?: boolean;
}

// ============================================================================
// Response & Event Payloads
// ============================================================================

export interface HandshakeResponse {
  serverVersion: string;
  workspaceRoot: string;
  dbPath: string;
  totalNodes: number;
  totalEdges: number;
  capabilities: string[];
}

export interface PongResponse {
  clientTimestamp: number;
  serverTimestamp: number;
}

export interface QueryResponse {
  success: boolean;
  errorMessage?: string;
  graph?: GraphData;
  rawJson?: string;
}

export interface ErrorResponse {
  code: string;
  message: string;
  details?: string;
}

export interface GraphPatchEvent {
  timestamp: number;
  addedNodes: GraphNode[];
  updatedNodes: GraphNode[];
  removedNodeIds: string[];
  addedEdges: GraphEdge[];
  removedEdgeIds: string[];
}

export interface ScanProgressEvent {
  phase: string;
  percentage: number;
  currentFile?: string;
  totalFiles?: number;
  processedFiles?: number;
}

// ============================================================================
// Envelope Message
// ============================================================================

export interface WebSocketMessage<T = unknown> {
  type: MessageType;
  requestId: string;
  payload: T;
}
