import React, { useState, useEffect, useRef, useCallback, useMemo, useSyncExternalStore } from 'react';
import {
  WebSocketMessage,
  HandshakeRequest,
  HandshakeResponse,
  GetArchitectureRequest,
  GetDependenciesRequest,
  ExecuteCypherRequest,
  TriggerScanRequest,
  ScanProgressEvent,
  QueryResponse,
  ErrorResponse,
  GraphData,
  GraphNode,
} from '../../../proto/types';
import { Toolbar } from './components/Toolbar';
import { ProjectFlowView, EdgeCategory } from './components/ProjectFlowView';
import { CytoscapeView } from './components/CytoscapeView';
import { LayeredArchitectureView } from './components/LayeredArchitectureView';
import { DomainArchitectureView } from './components/DomainArchitectureView';
import { C1SystemContextView } from './components/C1SystemContextView';
import { NodeGridView, NodeCategorySelection } from './components/NodeGridView';
import { MermaidDiagramView } from './components/MermaidDiagramView';
import {
  CommandManager,
  CommandProvider,
  ViewMode,
  ChangeViewModeCommand,
  SelectProjectCommand,
  ToggleShowTestsCommand,
  ToggleGroupLayersCommand,
  ToggleFlowEdgeTypeCommand,
  ToggleFlowCategoryCommand,
  ToggleFlowCardExpandCommand,
  ResetFlowLevelsCommand,
  ToggleLayerCollapseCommand,
  SelectDrawerNodeCommand,
} from './commands';

declare function acquireVsCodeApi(): {
  postMessage(message: any): void;
  getState(): any;
  setState(state: any): void;
};

let vscodeApi: any = null;
try {
  vscodeApi = acquireVsCodeApi();
} catch {}

const logToExtension = (level: 'INFO' | 'WARN' | 'ERROR', message: string) => {
  if (level === 'ERROR') {
    console.error(`[CodeExplorer Webview] ${message}`);
  } else if (level === 'WARN') {
    console.warn(`[CodeExplorer Webview] ${message}`);
  } else {
    console.log(`[CodeExplorer Webview] ${message}`);
  }
  if (vscodeApi) {
    try {
      vscodeApi.postMessage({
        type: 'LOG',
        level,
        message,
      });
    } catch {}
  }
};

export interface HistoryItem {
  viewMode: ViewMode;
  selectedProject: string;
}

export interface ErrorInfo {
  code?: string;
  message: string;
  details?: string;
  timestamp: number;
}

interface ErrorBoundaryProps {
  children: React.ReactNode;
  onLogError?: (error: Error, info: React.ErrorInfo) => void;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): Partial<ErrorBoundaryState> {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    this.props.onLogError?.(error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="view-error-fallback">
          <div className="error-fallback-card">
            <div className="error-fallback-icon">⚠️</div>
            <h2>View Render Error</h2>
            <p className="error-fallback-msg">
              {this.state.error?.message || 'An unexpected error occurred while rendering this view.'}
            </p>
            {this.state.error?.stack && (
              <pre className="error-fallback-stack">{this.state.error.stack}</pre>
            )}
            <button
              className="error-action-btn primary"
              onClick={() => this.setState({ hasError: false, error: null })}
            >
              Retry View
            </button>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}

export const App: React.FC = () => {
  const [viewMode, setViewMode] = useState<ViewMode>('layers');
  const [connectionStatus, setConnectionStatus] = useState<
    'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error'
  >('connecting');
  const [allProjects, setAllProjects] = useState<string[]>([]);
  const [projectPaths, setProjectPaths] = useState<Record<string, string>>({});
  const [selectedProject, setSelectedProject] = useState<string>('');
  const [flowGraph, setFlowGraph] = useState<GraphData | null>(null);
  const [fullGraph, setFullGraph] = useState<GraphData | null>(null);
  const [cypherQuery, setCypherQuery] = useState<string>('');
  const [selectedDrawerNode, setSelectedDrawerNode] = useState<GraphNode | null>(null);
  const [showTests, setShowTests] = useState<boolean>(true);
  const [groupLayers, setGroupLayers] = useState<boolean>(true);

  // Graph Management & Scan State
  const [isScanning, setIsScanning] = useState<boolean>(false);
  const [scanProgress, setScanProgress] = useState<ScanProgressEvent | null>(null);
  const [scanNotification, setScanNotification] = useState<{ type: 'success' | 'error' | 'info'; text: string } | null>(null);
  const [graphStats, setGraphStats] = useState<{ totalNodes: number; totalEdges: number; serverVersion?: string } | null>(null);
  const [gridCategory, setGridCategory] = useState<NodeCategorySelection | null>(null);
  const [serverHttpUrl, setServerHttpUrl] = useState<string>('');

  // Active error and diagnostics state
  const [activeError, setActiveError] = useState<ErrorInfo | null>(null);
  const [showErrorDetails, setShowErrorDetails] = useState<boolean>(false);
  const [copiedError, setCopiedError] = useState<boolean>(false);
  const lastWsUrlRef = useRef<string>('');
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const reconnectAttemptRef = useRef<number>(0);
  const isManuallyClosedRef = useRef<boolean>(false);

  // Global dependency counts derived from full architecture graph
  const { projectInCounts, projectOutCounts } = useMemo(() => {
    const inMap: Record<string, number> = {};
    const outMap: Record<string, number> = {};

    const idToName: Record<string, string> = {};
    for (const n of fullGraph?.nodes || []) {
      idToName[n.id] = n.name;
    }

    for (const edge of fullGraph?.edges || []) {
      const srcName = idToName[edge.source] || edge.source;
      const tgtName = idToName[edge.target] || edge.target;

      outMap[edge.source] = (outMap[edge.source] || 0) + 1;
      outMap[srcName] = (outMap[srcName] || 0) + 1;

      inMap[edge.target] = (inMap[edge.target] || 0) + 1;
      inMap[tgtName] = (inMap[tgtName] || 0) + 1;
    }

    return { projectInCounts: inMap, projectOutCounts: outMap };
  }, [fullGraph]);

  // Command Manager for Universal Undo / Redo
  const commandManager = useMemo(() => new CommandManager(100), []);
  const commandSnapshot = useSyncExternalStore(
    (onStoreChange) => commandManager.subscribe(onStoreChange),
    () => commandManager.getSnapshot()
  );

  // Lifted UI interaction states for cross-view persistence & undoability
  const [flowExpandedCategories, setFlowExpandedCategories] = useState<Map<string, Set<string>>>(new Map());
  const [flowExpandedCards, setFlowExpandedCards] = useState<Set<string>>(new Set());
  const [flowVisibleEdgeTypes, setFlowVisibleEdgeTypes] = useState<Record<EdgeCategory, boolean>>({
    library: true,
    service_call: true,
    database: true,
    messaging: true,
  });
  const [collapsedLayers, setCollapsedLayers] = useState<Set<string>>(new Set());

  const wsRef = useRef<WebSocket | null>(null);
  const workspaceRootRef = useRef<string>('');

  const sendWsMessage = useCallback((msg: WebSocketMessage) => {
    if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
      logToExtension('INFO', `Sending WS message '${msg.type}' (${msg.requestId || 'no-id'})`);
      wsRef.current.send(JSON.stringify(msg));
    } else {
      logToExtension('WARN', `Cannot send '${msg.type}': WebSocket is not open (state: ${wsRef.current?.readyState})`);
    }
  }, []);

  const requestDependencies = useCallback((projectName?: string) => {
    const req: WebSocketMessage<GetDependenciesRequest> = {
      type: 'GET_DEPENDENCIES_REQUEST',
      requestId: `req_dep_${Date.now()}`,
      payload: {
        projectName,
        includeIncoming: true,
        includeOutgoing: true,
      },
    };
    sendWsMessage(req);
  }, [sendWsMessage]);

  const requestArchitecture = useCallback(() => {
    const req: WebSocketMessage<GetArchitectureRequest> = {
      type: 'GET_ARCHITECTURE_REQUEST',
      requestId: `req_arch_${Date.now()}`,
      payload: { format: 'graph' },
    };
    sendWsMessage(req);
  }, [sendWsMessage]);

  // Command Action Handlers
  const handleViewModeChange = useCallback(
    (targetMode: ViewMode) => {
      if (viewMode === targetMode) return;
      const cmd = new ChangeViewModeCommand(
        viewMode,
        targetMode,
        selectedProject,
        selectedProject,
        setViewMode,
        setSelectedProject,
        (m, p) => {
          if (m === 'flow' && p) {
            requestDependencies(p);
          } else if (m !== 'flow') {
            requestArchitecture();
          }
        }
      );
      commandManager.executeCommand(cmd);
    },
    [viewMode, selectedProject, commandManager, requestDependencies, requestArchitecture]
  );

  const handleSelectProject = useCallback(
    (targetProject: string, targetMode: ViewMode = 'flow') => {
      if (selectedProject === targetProject && viewMode === targetMode) return;
      const cmd = new SelectProjectCommand(
        selectedProject,
        targetProject,
        viewMode,
        targetMode,
        setViewMode,
        setSelectedProject,
        (p) => requestDependencies(p)
      );
      commandManager.executeCommand(cmd);
    },
    [selectedProject, viewMode, commandManager, requestDependencies]
  );

  const handleToggleShowTests = useCallback(() => {
    const cmd = new ToggleShowTestsCommand(showTests, !showTests, setShowTests);
    commandManager.executeCommand(cmd);
  }, [showTests, commandManager]);

  const handleToggleGroupLayers = useCallback(() => {
    const cmd = new ToggleGroupLayersCommand(groupLayers, !groupLayers, setGroupLayers);
    commandManager.executeCommand(cmd);
  }, [groupLayers, commandManager]);

  const handleToggleFlowEdgeType = useCallback(
    (cat: EdgeCategory) => {
      const nextTypes = {
        ...flowVisibleEdgeTypes,
        [cat]: !flowVisibleEdgeTypes[cat],
      };
      const cmd = new ToggleFlowEdgeTypeCommand(
        cat,
        flowVisibleEdgeTypes,
        nextTypes,
        setFlowVisibleEdgeTypes
      );
      commandManager.executeCommand(cmd);
    },
    [flowVisibleEdgeTypes, commandManager]
  );

  const handleToggleFlowCategory = useCallback(
    (projectName: string, category: string, projectId?: string, currentlyActive?: boolean) => {
      const prevCategories = new Map(flowExpandedCategories);
      const nextCategories = new Map(flowExpandedCategories);

      const isRoot =
        selectedProject &&
        (projectName.toLowerCase() === selectedProject.toLowerCase() ||
          (projectId && projectId.toLowerCase().includes(selectedProject.toLowerCase())));

      const existingActive =
        nextCategories.get(projectName) ||
        nextCategories.get(projectName.toLowerCase()) ||
        (projectId ? nextCategories.get(projectId) || nextCategories.get(projectId.toLowerCase()) : undefined);

      const currentActive =
        existingActive !== undefined
          ? existingActive
          : isRoot
          ? new Set<string>(['callsOut', 'acceptsIn', 'libsOut', 'libsIn', 'dbOut', 'messagesOut', 'messagesIn'])
          : new Set<string>();

      const updated = new Set(currentActive);
      const isExpanding = currentlyActive !== undefined ? !currentlyActive : !updated.has(category);
      if (isExpanding) {
        updated.add(category);
      } else {
        updated.delete(category);
      }

      nextCategories.set(projectName, updated);
      nextCategories.set(projectName.toLowerCase(), updated);
      if (projectId) {
        nextCategories.set(projectId, updated);
        nextCategories.set(projectId.toLowerCase(), updated);
      }

      const prevCards = new Set(flowExpandedCards);
      const nextCards = new Set(flowExpandedCards);
      nextCards.add(projectName);
      if (projectId) nextCards.add(projectId);

      const cmd = new ToggleFlowCategoryCommand(
        projectName,
        category,
        isExpanding,
        prevCategories,
        nextCategories,
        prevCards,
        nextCards,
        setFlowExpandedCategories,
        setFlowExpandedCards
      );
      commandManager.executeCommand(cmd);
    },
    [flowExpandedCategories, flowExpandedCards, selectedProject, commandManager]
  );

  const handleToggleFlowCardExpand = useCallback(
    (projectName: string, projectId?: string) => {
      const prevCards = new Set(flowExpandedCards);
      const nextCards = new Set(flowExpandedCards);
      const isExp = nextCards.has(projectName) || (projectId ? nextCards.has(projectId) : false);

      if (isExp) {
        nextCards.delete(projectName);
        if (projectId) nextCards.delete(projectId);
      } else {
        nextCards.add(projectName);
        if (projectId) nextCards.add(projectId);
      }

      const cmd = new ToggleFlowCardExpandCommand(
        projectName,
        !isExp,
        prevCards,
        nextCards,
        setFlowExpandedCards
      );
      commandManager.executeCommand(cmd);
    },
    [flowExpandedCards, commandManager]
  );

  const handleResetFlowLevels = useCallback(() => {
    if (!selectedProject) return;
    const allCats = new Set<string>([
      'callsOut',
      'acceptsIn',
      'libsOut',
      'libsIn',
      'dbOut',
      'messagesOut',
      'messagesIn',
    ]);
    const prevCats = new Map(flowExpandedCategories);
    const nextCats = new Map<string, Set<string>>();
    nextCats.set(selectedProject, allCats);
    nextCats.set(selectedProject.toLowerCase(), allCats);

    const prevCards = new Set(flowExpandedCards);
    const nextCards = new Set<string>([selectedProject]);

    const cmd = new ResetFlowLevelsCommand(
      prevCats,
      nextCats,
      prevCards,
      nextCards,
      setFlowExpandedCategories,
      setFlowExpandedCards
    );
    commandManager.executeCommand(cmd);
  }, [selectedProject, flowExpandedCategories, flowExpandedCards, commandManager]);

  const handleToggleLayerCollapse = useCallback(
    (layerId: string) => {
      const prevCollapsed = new Set(collapsedLayers);
      const nextCollapsed = new Set(collapsedLayers);
      const isCollapsing = !nextCollapsed.has(layerId);
      if (isCollapsing) {
        nextCollapsed.add(layerId);
      } else {
        nextCollapsed.delete(layerId);
      }

      const cmd = new ToggleLayerCollapseCommand(
        layerId,
        layerId,
        isCollapsing,
        prevCollapsed,
        nextCollapsed,
        setCollapsedLayers
      );
      commandManager.executeCommand(cmd);
    },
    [collapsedLayers, commandManager]
  );

  const handleSelectDrawerNode = useCallback(
    (node: GraphNode | null) => {
      if (selectedDrawerNode === node) return;
      const cmd = new SelectDrawerNodeCommand(
        selectedDrawerNode,
        node,
        setSelectedDrawerNode
      );
      commandManager.executeCommand(cmd);

      if (vscodeApi && node) {
        vscodeApi.postMessage({
          type: 'NODE_SELECTED',
          nodeId: node.id,
          name: node.name,
          kind: node.kind,
        });
      }
    },
    [selectedDrawerNode, commandManager]
  );

  // Keyboard shortcut listener for Undo / Redo (Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, Alt+Left, Alt+Right)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      const isInput =
        target &&
        (target.tagName === 'INPUT' ||
          target.tagName === 'TEXTAREA' ||
          target.isContentEditable);

      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
        if (isInput) return; // Allow native undo inside input/textarea
        e.preventDefault();
        if (e.shiftKey) {
          commandManager.redo();
        } else {
          commandManager.undo();
        }
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y') {
        if (isInput) return;
        e.preventDefault();
        commandManager.redo();
      } else if (e.altKey && e.key === 'ArrowLeft') {
        e.preventDefault();
        commandManager.undo();
      } else if (e.altKey && e.key === 'ArrowRight') {
        e.preventDefault();
        commandManager.redo();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [commandManager]);

  const handleOpenFile = useCallback((filePath: string, lineStart?: number) => {
    if (vscodeApi) {
      vscodeApi.postMessage({
        type: 'OPEN_FILE',
        filePath,
        lineStart,
      });
    }
  }, []);

  const handleRunCypher = useCallback(() => {
    if (!cypherQuery.trim()) return;
    const req: WebSocketMessage<ExecuteCypherRequest> = {
      type: 'EXECUTE_CYPHER_REQUEST',
      requestId: `req_cypher_${Date.now()}`,
      payload: { query: cypherQuery.trim() },
    };
    sendWsMessage(req);
  }, [cypherQuery, sendWsMessage]);

  const connectWebSocket = useCallback((url: string) => {
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }

    if (wsRef.current) {
      try {
        isManuallyClosedRef.current = true;
        wsRef.current.close();
      } catch {}
      wsRef.current = null;
    }
    isManuallyClosedRef.current = false;

    lastWsUrlRef.current = url;
    const httpUrl = url.replace(/^ws:\/\//, 'http://').replace(/^wss:\/\//, 'https://').replace(/\/ws$/, '');
    setServerHttpUrl(httpUrl);
    logToExtension('INFO', `Connecting to backend WebSocket at ${url} (HTTP: ${httpUrl})...`);
    setConnectionStatus(reconnectAttemptRef.current > 0 ? 'reconnecting' : 'connecting');
    const ws = new WebSocket(url);
    wsRef.current = ws;

    ws.onopen = () => {
      logToExtension('INFO', `Connected to backend WebSocket at ${url}`);
      setConnectionStatus('connected');
      reconnectAttemptRef.current = 0;
      setActiveError(null);
      // 1. Handshake
      const handshake: WebSocketMessage<HandshakeRequest> = {
        type: 'HANDSHAKE_REQUEST',
        requestId: 'req_handshake_1',
        payload: {
          clientVersion: '0.1.0',
          clientName: 'vscode-extension',
          workspacePath: workspaceRootRef.current,
        },
      };
      ws.send(JSON.stringify(handshake));

    };

    ws.onmessage = (event) => {
      try {
        const msg: WebSocketMessage = JSON.parse(event.data);
        switch (msg.type) {
          case 'HANDSHAKE_RESPONSE': {
            const resp = msg.payload as HandshakeResponse;
            logToExtension(
              'INFO',
              `Handshake successful: server v${resp.serverVersion}, nodes=${resp.totalNodes}, edges=${resp.totalEdges}, schemaOutdated=${resp.isSchemaOutdated}, isScanning=${resp.isScanning}`
            );
            setGraphStats({ totalNodes: resp.totalNodes, totalEdges: resp.totalEdges, serverVersion: resp.serverVersion });
            console.log(`Connected to CodeExplorer ${resp.serverVersion}`);

            if (resp.isScanning) {
              setIsScanning(true);
              if (resp.scanProgress) {
                setScanProgress(resp.scanProgress);
              }
              setScanNotification({ type: 'info', text: 'Workspace indexing is currently in progress...' });
            } else if (resp.isSchemaOutdated) {
              logToExtension(
                'WARN',
                `Database schema is outdated (v${resp.schemaVersion ?? 1} < v${resp.currentSchemaVersion ?? 2}). Triggering background re-index...`
              );
              setIsScanning(true);
              setScanProgress({ phase: 'Starting', percentage: 5, currentFile: 'Upgrading database schema...' });
              setScanNotification({ type: 'info', text: 'Database schema is outdated. Rebuilding graph in background...' });
              sendWsMessage({
                type: 'TRIGGER_SCAN_REQUEST',
                requestId: `req_scan_schema_${Date.now()}`,
                payload: { clear: true },
              });
            } else {
              // Fetch full architecture (with layer classifications)
              requestArchitecture();

              // Fetch dependencies for default/first project
              requestDependencies();
            }
            break;
          }

          case 'SCAN_PROGRESS_EVENT': {
            const scanEv = msg.payload as ScanProgressEvent;
            logToExtension('INFO', `Scan progress: phase=${scanEv.phase}, percent=${scanEv.percentage}%`);
            setScanProgress(scanEv);
            if (scanEv.phase === 'Starting' || scanEv.phase === 'Indexing') {
              setIsScanning(true);
            } else if (scanEv.phase === 'Completed') {
              setIsScanning(false);
              const countInfo = scanEv.currentFile || `Completed (${scanEv.totalFiles || 0} nodes)`;
              setScanNotification({ type: 'success', text: countInfo });
              if (scanEv.totalFiles) {
                setGraphStats((prev) => ({ totalNodes: scanEv.totalFiles || prev?.totalNodes || 0, totalEdges: prev?.totalEdges || 0, serverVersion: prev?.serverVersion }));
              }
              setTimeout(() => setScanNotification(null), 5000);
              // Auto-refresh active views
              requestArchitecture();
              if (selectedProject) {
                requestDependencies(selectedProject);
              } else {
                requestDependencies();
              }
            } else if (scanEv.phase === 'Failed') {
              setIsScanning(false);
              setScanNotification({ type: 'error', text: scanEv.currentFile || 'Scan failed' });
              setTimeout(() => setScanNotification(null), 7000);
            }
            break;
          }

          case 'ERROR_RESPONSE': {
            const err = msg.payload as ErrorResponse;
            logToExtension('ERROR', `Server error [${err.code}]: ${err.message}`);
            setActiveError({
              code: err.code || 'ERROR',
              message: err.message || 'An unexpected error occurred',
              details: err.details,
              timestamp: Date.now(),
            });
            setShowErrorDetails(false);
            if (vscodeApi) {
              vscodeApi.postMessage({
                type: 'SHOW_ERROR',
                message: err.message,
                details: err.details,
              });
            }
            break;
          }

          case 'QUERY_RESPONSE': {
            const resp = msg.payload as QueryResponse;
            if (resp.success && resp.graph) {
              const nodeCount = resp.graph.nodes?.length || 0;
              const edgeCount = resp.graph.edges?.length || 0;
              const isArch = msg.requestId?.startsWith('req_arch') || resp.graph.metadata?.graphType === 'architecture';
              const isFlow = msg.requestId?.startsWith('req_dep') || resp.graph.metadata?.graphType === 'flow';
              const hasColumns = resp.graph.nodes?.some((n) => n.properties?.column);
              const isFlowGraph = isFlow || (!isArch && hasColumns);
              logToExtension('INFO', `Query response received: ${nodeCount} nodes, ${edgeCount} edges (type=${isFlowGraph ? 'flow' : 'architecture'})`);
              if (isFlowGraph) {
                setFlowGraph(resp.graph);
                const metadata = resp.graph.metadata;
                if (metadata?.selectedProject) {
                  const sp = metadata.selectedProject;
                  setSelectedProject(sp);
                }
                if (metadata?.allProjects) {
                  try {
                    const parsed = JSON.parse(metadata.allProjects) as string[];
                    const seen = new Set<string>();
                    const unique: string[] = [];
                    for (const p of parsed.filter(Boolean)) {
                      const lower = p.toLowerCase();
                      if (!seen.has(lower)) {
                        seen.add(lower);
                        unique.push(p);
                      }
                    }
                    setAllProjects(unique);
                  } catch {}
                }
                if (metadata?.projectPaths) {
                  try {
                    const parsedPaths = JSON.parse(metadata.projectPaths) as Record<string, string>;
                    setProjectPaths((prev) => ({ ...prev, ...parsedPaths }));
                  } catch {}
                }
              } else {
                setFullGraph(resp.graph);
                const metadata = resp.graph.metadata;
                if (metadata?.projectPaths) {
                  try {
                    const parsedPaths = JSON.parse(metadata.projectPaths) as Record<string, string>;
                    setProjectPaths((prev) => ({ ...prev, ...parsedPaths }));
                  } catch {}
                }
                // Also extract all project names from full architecture if not set
                if (resp.graph.nodes) {
                  const seen = new Set<string>();
                  const projs: string[] = [];
                  const nodePaths: Record<string, string> = {};
                  for (const n of resp.graph.nodes) {
                    if (n.kind === 'Project' && Boolean(n.name)) {
                      const lower = n.name.toLowerCase();
                      if (!seen.has(lower)) {
                        seen.add(lower);
                        projs.push(n.name);
                      }
                      const p =
                        n.filePath ||
                        n.properties?.path ||
                        (n.id?.startsWith('workspace:project:')
                          ? n.id.substring('workspace:project:'.length)
                          : '');
                      if (p) {
                        nodePaths[n.name] = p;
                      }
                    }
                  }
                  if (Object.keys(nodePaths).length > 0) {
                    setProjectPaths((prev) => ({ ...nodePaths, ...prev }));
                  }
                  projs.sort((a, b) => a.localeCompare(b));
                  if (projs.length > 0) {
                    setAllProjects((prev) => (prev.length === 0 ? projs : prev));
                    setSelectedProject((prev) => (prev ? prev : projs[0]));
                  }
                }
              }
            } else if (!resp.success) {
              const errorMsg = resp.errorMessage || 'Query execution failed';
              const errorDetails = (resp as any).errorDetails;
              logToExtension('ERROR', `Query failed: ${errorMsg}`);
              setActiveError({
                code: 'QUERY_FAILED',
                message: errorMsg,
                details: errorDetails,
                timestamp: Date.now(),
              });
              setShowErrorDetails(false);
              if (vscodeApi) {
                vscodeApi.postMessage({
                  type: 'SHOW_ERROR',
                  message: errorMsg,
                  details: errorDetails,
                });
              }
            }
            break;
          }
        }
      } catch (err: any) {
        logToExtension('ERROR', `Failed to parse WS message: ${err?.message || err}`);
        console.error('Failed to parse WS message:', err);
      }
    };

    ws.onerror = () => {
      logToExtension('ERROR', `WebSocket connection error at ${url}`);
      setConnectionStatus('error');
      setActiveError((prev) => prev ?? {
        code: 'CONNECTION_FAILED',
        message: `Failed to connect to CodeExplorer server at ${url}`,
        details: 'Verify that the backend process is running and that port conflicts or firewall rules are not blocking WebSocket connections.',
        timestamp: Date.now(),
      });
    };

    ws.onclose = (ev) => {
      logToExtension('WARN', `WebSocket closed (code: ${ev.code}, reason: ${ev.reason || 'none'})`);
      if (isManuallyClosedRef.current || ev.code === 1000) {
        setConnectionStatus('disconnected');
        return;
      }

      const attempt = reconnectAttemptRef.current + 1;
      reconnectAttemptRef.current = attempt;
      const delay = Math.min(1000 * Math.pow(1.5, Math.min(attempt, 8)), 10000);

      setConnectionStatus('reconnecting');
      logToExtension('WARN', `Scheduling reconnect attempt #${attempt} in ${Math.round(delay)}ms...`);

      reconnectTimeoutRef.current = setTimeout(() => {
        if (lastWsUrlRef.current) {
          connectWebSocket(lastWsUrlRef.current);
        }
      }, delay);
    };
  }, [requestDependencies, requestArchitecture]);

  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      const msg = event.data;
      switch (msg.type) {
        case 'SERVER_CONFIG':
          logToExtension('INFO', `Received SERVER_CONFIG: wsUrl=${msg.wsUrl}, workspace=${msg.workspaceRoot}`);
          workspaceRootRef.current = msg.workspaceRoot;
          connectWebSocket(msg.wsUrl);
          break;

        case 'TRIGGER_SCAN':
          logToExtension('INFO', `Received TRIGGER_SCAN from extension (clear=${Boolean(msg.clear)})`);
          handleTriggerScan(Boolean(msg.clear));
          break;

        case 'SET_VIEW_MODE':
          logToExtension('INFO', `Received SET_VIEW_MODE from extension: ${msg.viewMode}`);
          if (msg.viewMode) {
            handleViewModeChange(msg.viewMode as ViewMode);
          }
          break;

        case 'OPEN_NODE_GRID':
          logToExtension('INFO', `Received OPEN_NODE_GRID from extension: kind=${msg.kind}, layer=${msg.layerName}`);
          setGridCategory({ kind: msg.kind, layerTitle: msg.layerName });
          handleViewModeChange('grid');
          break;

        case 'FOCUS_NODE':
          logToExtension('INFO', `Received FOCUS_NODE from extension: ${msg.nodeId} (${msg.kind})`);
          if (msg.nodeId) {
            const currentGraph = viewMode === 'flow' ? flowGraph : fullGraph;
            const targetNode = currentGraph?.nodes?.find(
              (n) => n.id === msg.nodeId || n.name === msg.nodeId || (msg.nodeId && n.id.includes(msg.nodeId))
            );
            if (targetNode) {
              setSelectedDrawerNode(targetNode);
            }
            const p = allProjects.find((name) => msg.nodeId.toLowerCase().includes(name.toLowerCase()));
            if (p) {
              handleSelectProject(p, viewMode === 'flow' ? 'flow' : 'c1');
            }
          }
          break;
      }
    };

    window.addEventListener('message', handleMessage);

    // Notify extension host that webview is ready
    if (vscodeApi) {
      vscodeApi.postMessage({ type: 'WEBVIEW_READY' });
    }

    return () => {
      window.removeEventListener('message', handleMessage);
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
      }
      if (wsRef.current) {
        isManuallyClosedRef.current = true;
        try {
          wsRef.current.close();
        } catch {}
      }
    };
  }, [connectWebSocket]);

  const handleCopyError = useCallback(() => {
    if (!activeError) return;
    const text = `[CodeExplorer Error ${activeError.code || 'ERROR'}]\nMessage: ${activeError.message}\n${activeError.details ? 'Details:\n' + activeError.details : ''}`;
    if (vscodeApi) {
      vscodeApi.postMessage({ type: 'COPY_TO_CLIPBOARD', text });
    } else if (navigator.clipboard) {
      navigator.clipboard.writeText(text);
    }
    setCopiedError(true);
    setTimeout(() => setCopiedError(false), 2000);
  }, [activeError]);

  const handleShowLogs = useCallback(() => {
    if (vscodeApi) {
      vscodeApi.postMessage({ type: 'SHOW_LOGS' });
    }
  }, []);

  const handleReconnect = useCallback(() => {
    setActiveError(null);
    reconnectAttemptRef.current = 0;
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }
    if (wsRef.current) {
      try {
        wsRef.current.close();
      } catch {}
    }
    if (lastWsUrlRef.current) {
      connectWebSocket(lastWsUrlRef.current);
    }
  }, [connectWebSocket]);

  const handleTriggerScan = useCallback((clear: boolean = false) => {
    if (connectionStatus !== 'connected') {
      logToExtension('WARN', 'Cannot trigger scan: not connected to server');
      return;
    }
    if (isScanning) {
      logToExtension('WARN', 'Scan already in progress');
      return;
    }
    setIsScanning(true);
    setScanProgress({ phase: 'Starting', percentage: 5, currentFile: clear ? 'Full re-index...' : 'Scanning workspace...' });
    setScanNotification(null);
    sendWsMessage({
      type: 'TRIGGER_SCAN_REQUEST',
      requestId: `req_scan_${Date.now()}`,
      payload: {
        targetPath: workspaceRootRef.current,
        clear,
      },
    });
  }, [connectionStatus, isScanning, sendWsMessage]);

  return (
    <CommandProvider manager={commandManager}>
      <div id="app">
        <Toolbar
          viewMode={viewMode}
          onViewModeChange={handleViewModeChange}
          allProjects={allProjects}
          projectPaths={projectPaths}
          selectedProject={selectedProject}
          onSelectProject={(p) => handleSelectProject(p, 'flow')}
          connectionStatus={connectionStatus}
          onFitView={() => {
            if (viewMode === 'flow') {
              requestDependencies(selectedProject);
            } else {
              requestArchitecture();
            }
          }}
          onRefresh={() => {
            if (viewMode === 'flow') {
              requestDependencies(selectedProject);
            } else {
              requestArchitecture();
            }
          }}
          cypherQuery={cypherQuery}
          onCypherQueryChange={setCypherQuery}
          onRunCypher={handleRunCypher}
          showTests={showTests}
          onToggleShowTests={handleToggleShowTests}
          canGoBack={commandSnapshot.canUndo}
          canGoForward={commandSnapshot.canRedo}
          onGoBack={() => commandManager.undo()}
          onGoForward={() => commandManager.redo()}
          undoDescription={commandSnapshot.undoDescription}
          redoDescription={commandSnapshot.redoDescription}
          groupLayers={groupLayers}
          onToggleGroupLayers={handleToggleGroupLayers}
          isScanning={isScanning}
          scanProgress={scanProgress}
          onTriggerScan={handleTriggerScan}
          graphStats={graphStats}
        />

        {scanNotification && (
          <div className={`scan-toast ${scanNotification.type}`}>
            <span className="toast-icon">
              {scanNotification.type === 'success' ? '✅' : scanNotification.type === 'info' ? 'ℹ️' : '❌'}
            </span>
            <span className="toast-text">{scanNotification.text}</span>
          </div>
        )}

        {activeError && (
          <div className="error-banner" role="alert">
            <div className="error-banner-main">
              <div className="error-badge-icon">⚠️</div>
              <div className="error-content">
                <div className="error-header">
                  {activeError.code && <span className="error-code-badge">{activeError.code}</span>}
                  <span className="error-title">{activeError.message}</span>
                </div>
                {showErrorDetails && activeError.details && (
                  <pre className="error-details-view">{activeError.details}</pre>
                )}
              </div>
              <div className="error-actions">
                {activeError.details && (
                  <button
                    className="error-action-btn secondary"
                    onClick={() => setShowErrorDetails((prev) => !prev)}
                  >
                    {showErrorDetails ? 'Hide Details' : 'View Details'}
                  </button>
                )}
                <button
                  className="error-action-btn secondary"
                  onClick={handleCopyError}
                  title="Copy error details to clipboard"
                >
                  {copiedError ? '✓ Copied' : 'Copy'}
                </button>
                <button
                  className="error-action-btn secondary"
                  onClick={handleShowLogs}
                  title="Open VS Code Output Channel"
                >
                  Show Logs
                </button>
                {(connectionStatus === 'disconnected' || connectionStatus === 'error' || connectionStatus === 'reconnecting') && (
                  <button
                    className="error-action-btn primary"
                    onClick={handleReconnect}
                    title="Reconnect to server"
                  >
                    {connectionStatus === 'reconnecting' ? 'Reconnecting now...' : 'Reconnect'}
                  </button>
                )}
                <button
                  className="error-action-btn close"
                  onClick={() => setActiveError(null)}
                  title="Dismiss error"
                >
                  ✕
                </button>
              </div>
            </div>
          </div>
        )}

        <main className="main-viewport">
          {isScanning && (
            <div className="floating-scan-progress">
              <div className="scan-progress-content">
                <span className="scan-spinner">⚡</span>
                <span className="scan-phase-label">
                  {scanProgress?.phase === 'Indexing' ? 'Indexing Workspace...' : 'Scanning...'}
                </span>
                {scanProgress?.currentFile && (
                  <span className="scan-file-label" title={scanProgress.currentFile}>
                    {scanProgress.currentFile}
                  </span>
                )}
                <span className="scan-percent-badge">
                  {Math.round(scanProgress?.percentage || 0)}%
                </span>
              </div>
              <div className="scan-progress-track">
                <div
                  className="scan-progress-bar"
                  style={{ width: `${Math.min(100, Math.max(0, scanProgress?.percentage || 0))}%` }}
                />
              </div>
            </div>
          )}

          <ErrorBoundary onLogError={(err) => logToExtension('ERROR', `View crash: ${err.message}\n${err.stack}`)}>
            {viewMode === 'c1' && (
              <C1SystemContextView
                graph={fullGraph}
                onOpenFile={handleOpenFile}
                onDrillDownToC2={(p) => handleSelectProject(p, 'flow')}
              />
            )}

            {viewMode === 'semantic' && (
              <DomainArchitectureView
                graph={fullGraph}
                onOpenFile={handleOpenFile}
                onFocusInFlow={(p) => handleSelectProject(p, 'flow')}
              />
            )}

            {viewMode === 'layers' && (
              <LayeredArchitectureView
                graph={fullGraph}
                onOpenFile={handleOpenFile}
                onFocusInFlow={(p) => handleSelectProject(p, 'flow')}
                showTests={showTests}
                collapsedLayers={collapsedLayers}
                onToggleLayer={handleToggleLayerCollapse}
              />
            )}

            {viewMode === 'flow' && (
              <ProjectFlowView
                graph={flowGraph}
                fullGraph={fullGraph}
                onSelectProject={(p) => handleSelectProject(p, 'flow')}
                onOpenFile={handleOpenFile}
                expandedCategories={flowExpandedCategories}
                onToggleCategory={handleToggleFlowCategory}
                expandedCards={flowExpandedCards}
                onToggleCardExpand={handleToggleFlowCardExpand}
                visibleEdgeTypes={flowVisibleEdgeTypes}
                onToggleEdgeType={handleToggleFlowEdgeType}
                onResetLevels={handleResetFlowLevels}
              />
            )}

            {viewMode === 'full' && (
              <CytoscapeView
                graph={fullGraph}
                onOpenFile={handleOpenFile}
                onSelectNode={handleSelectDrawerNode}
                groupLayers={groupLayers}
                onToggleGroupLayers={handleToggleGroupLayers}
                showTests={showTests}
                collapsedLayers={collapsedLayers}
                onToggleLayerCollapse={handleToggleLayerCollapse}
              />
            )}

            {viewMode === 'grid' && (
              <NodeGridView
                category={gridCategory}
                graph={fullGraph}
                serverHttpUrl={serverHttpUrl}
                onOpenFile={handleOpenFile}
                onFocusInDiagram={(nodeId, kind) => {
                  const p = allProjects.find((name) => nodeId.toLowerCase().includes(name.toLowerCase()));
                  if (p) {
                    handleSelectProject(p, 'flow');
                  } else {
                    handleViewModeChange('c1');
                  }
                }}
                onSelectNode={handleSelectDrawerNode}
                onSwitchView={(m) => handleViewModeChange(m)}
              />
            )}

            {viewMode === 'mermaid' && (
              <MermaidDiagramView
                graph={fullGraph}
                serverHttpUrl={serverHttpUrl}
                onOpenFile={handleOpenFile}
                onFocusNode={(nodeId, kind) => {
                  const p = allProjects.find((name) => nodeId.toLowerCase().includes(name.toLowerCase()));
                  if (p) {
                    handleSelectProject(p, 'flow');
                  } else {
                    handleViewModeChange('c1');
                  }
                }}
              />
            )}
          </ErrorBoundary>
        </main>

        {/* Slide-out drawer for Cytoscape node inspection */}
        {selectedDrawerNode && (
          <aside className="drawer">
            <div className="drawer-header">
              <span className="badge">{selectedDrawerNode.kind}</span>
              <h3>{selectedDrawerNode.displayName || selectedDrawerNode.name}</h3>
              <button className="close-btn" onClick={() => handleSelectDrawerNode(null)}>
                &times;
              </button>
            </div>
            <div className="drawer-content">
              {selectedDrawerNode.filePath && (
                <div className="drawer-field">
                  <label>Location</label>
                  <div
                    className="clickable-code-link"
                    onClick={() => handleOpenFile(selectedDrawerNode.filePath!, selectedDrawerNode.lineStart)}
                  >
                    {selectedDrawerNode.filePath}
                  </div>
                </div>
              )}
              <div className="drawer-field">
                <label>Properties</label>
                <div className="property-list">
                  {selectedDrawerNode.properties &&
                    Object.entries(selectedDrawerNode.properties).map(([k, v]) => (
                      <div key={k} className="prop-item">
                        <span className="prop-key">{k}</span>
                        <span className="prop-val">{v}</span>
                      </div>
                    ))}
                </div>
              </div>
            </div>
          </aside>
        )}
      </div>
    </CommandProvider>
  );
};
