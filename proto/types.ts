/**
 * TypeScript Type Definitions for CodeExplorer WebSocket Protocol
 * Canonical source: proto/code_explorer.proto
 */

export type MessageType =
  // Client -> Server
  | 'HANDSHAKE_REQUEST'
  | 'PING_REQUEST'
  | 'GET_VIEW_REQUEST'
  | 'GET_ARCHITECTURE_REQUEST'
  | 'GET_DEPENDENCIES_REQUEST'
  | 'GET_SYMBOL_NEIGHBORHOOD_REQUEST'
  | 'GET_CALL_CHAIN_REQUEST'
  | 'GET_IMPACT_REQUEST'
  | 'EXECUTE_CYPHER_REQUEST'
  | 'TRIGGER_SCAN_REQUEST'
  | 'GET_ONTOLOGY_LAYERS'
  | 'SET_DOMAIN_OVERRIDE_REQUEST'
  | 'GET_GATES_REQUEST'
  | 'EVALUATE_GATES_REQUEST'
  | 'RATCHET_GATES_REQUEST'
  | 'INIT_GATES_REQUEST'
  | 'SAVE_GATE_REQUEST'
  | 'TEST_GATE_REQUEST'
  // Server -> Client responses
  | 'HANDSHAKE_RESPONSE'
  | 'PONG_RESPONSE'
  | 'GET_VIEW_RESPONSE'
  | 'QUERY_RESPONSE'
  | 'GET_ONTOLOGY_LAYERS_RESPONSE'
  | 'DOMAIN_MUTATION_RESPONSE'
  | 'GET_GATES_RESPONSE'
  | 'EVALUATE_GATES_RESPONSE'
  | 'RATCHET_GATES_RESPONSE'
  | 'INIT_GATES_RESPONSE'
  | 'SAVE_GATE_RESPONSE'
  | 'TEST_GATE_RESPONSE'
  | 'ERROR_RESPONSE'
  // Broadcast events
  | 'GRAPH_PATCH_EVENT'
  | 'SCAN_PROGRESS_EVENT';

// ============================================================================
// Common Graph Models
// ============================================================================

export const PROJECT_KINDS = new Set([
  'Project',
  'Service',
  'App',
  'FrontendApp',
  'Library',
  'SharedLibrary',
  'Worker',
  'CliTool',
]);

export function isProjectKind(kind?: string): boolean {
  return Boolean(kind && PROJECT_KINDS.has(kind));
}

export interface GraphNode {
  id: string;
  kind: string; // 'Project' | 'Service' | 'App' | 'Library' | 'Worker' | 'CliTool' | 'Endpoint' etc.
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
  category?: 'library' | 'service_call' | 'database' | 'messaging' | string;
  isTransitive?: boolean;
  isInferred?: boolean;
  properties?: Record<string, string>;
}

export interface GraphData {
  nodes: GraphNode[];
  edges: GraphEdge[];
  metadata?: Record<string, string>;
}

export interface OntologyCategory {
  kind: string;
  label: string;
  icon: string;
  count: number;
  layerId: number;
}

export interface OntologyLayer {
  layerId: number;
  name: string;
  title: string;
  description: string;
  icon: string;
  totalCount: number;
  categories: OntologyCategory[];
}

export interface OntologyLayersResponse {
  layers: OntologyLayer[];
  totalNodes: number;
  totalEdges: number;
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
  includeLibraries?: boolean;
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
  isSchemaOutdated?: boolean;
  schemaVersion?: number;
  currentSchemaVersion?: number;
  isScanning?: boolean;
  scanProgress?: ScanProgressEvent;
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
// Semantic Invariant Gates (SIG) DTOs
// ============================================================================

export interface GateViolationDto {
  gateId: string;
  file?: string;
  line: number;
  column: number;
  symbol?: string;
  target?: string;
  message?: string;
  fingerprint: string;
  isTolerated: boolean;
}

export interface GateItemDto {
  id: string;
  name: string;
  category: string;
  platform: string;
  scope: string;
  dimension: string;
  severity: string;
  description?: string;
  remediation?: string;
  query: string;
  source: string;
  ratchetEnabled: boolean;
  baselinePath?: string;
  status: 'Passed' | 'PassedTolerated' | 'FailedNew' | 'Error' | 'Pending' | 'Evaluating' | string;
  totalViolations: number;
  newViolationsCount: number;
  toleratedViolationsCount: number;
  baselineCount: number;
  canRatchetDown: boolean;
  durationMs: number;
  errorMessage?: string;
  violations: GateViolationDto[];
}

export interface GateSuiteSummaryDto {
  totalGates: number;
  passedCount: number;
  failedCount: number;
  newViolationsCount: number;
  toleratedViolationsCount: number;
  ratchetOpportunities: number;
  isSuccess: boolean;
  totalDurationMs: number;
  gates: GateItemDto[];
}

export interface EvaluateGatesRequestDto {
  gateId?: string;
}

export interface RatchetGatesRequestDto {
  gateId?: string;
  updateAll?: boolean;
}

export interface RatchetGatesResponseDto {
  success: boolean;
  updatedGatesCount: number;
  message: string;
  suite?: GateSuiteSummaryDto;
}

export interface InitGatesRequestDto {
  overwrite?: boolean;
}

export interface SaveGateRequestDto {
  gate: Partial<GateItemDto>;
  targetPath?: string;
}

export interface SaveGateResponseDto {
  success: boolean;
  message: string;
  filePath?: string;
  gate?: GateItemDto;
  suite?: GateSuiteSummaryDto;
}

export interface TestGateRequestDto {
  query: string;
  gateId?: string;
  parameters?: Record<string, string>;
}

export interface TestGateResponseDto {
  success: boolean;
  durationMs: number;
  violationsCount: number;
  violations: GateViolationDto[];
  projectedColumns: string[];
  missingColumns: string[];
  error?: string;
}

export interface ArchitectureEvidenceItemDto {
  id: string;
  /** endpoint | http_call | grpc_call | wcf_call | publish | subscribe | query | orm | table | procedure | config | migration | type_reference */
  kind: string;
  /** Badge text: POST, GET, PUBLISH, SUBSCRIBE, SELECT, INSERT, ORM, CONFIG, ... */
  operation: string;
  title: string;
  filePath: string;
  lineStart: number;
  lineEnd: number;
  codeSnippet?: string;
  callerSymbol?: string;
  targetSymbol?: string;
  serviceName?: string;
  reason: string;
}

export interface DatabaseEvidenceResponseDto {
  databaseId: string;
  databaseName: string;
  engine: string;
  schema: string;
  serviceId?: string;
  totalCount: number;
  items: ArchitectureEvidenceItemDto[];
}

export interface EdgeEvidenceResponseDto {
  sourceId: string;
  targetId: string;
  edgeKind: string;
  totalCount: number;
  items: ArchitectureEvidenceItemDto[];
}

export interface NodeEvidenceResponseDto {
  nodeId: string;
  nodeName: string;
  nodeKind: string;
  totalCount: number;
  items: ArchitectureEvidenceItemDto[];
}

// ============================================================================
// Envelope Message
// ============================================================================

export interface WebSocketMessage<T = unknown> {
  type: MessageType;
  requestId: string;
  payload: T;
}
