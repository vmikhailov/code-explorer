import React, { useState, useEffect, useRef, useCallback } from 'react';
import {
  WebSocketMessage,
  HandshakeRequest,
  HandshakeResponse,
  GetArchitectureRequest,
  GetDependenciesRequest,
  ExecuteCypherRequest,
  QueryResponse,
  GraphData,
  GraphNode,
} from '../../../proto/types';
import { Toolbar } from './components/Toolbar';
import { ProjectFlowView } from './components/ProjectFlowView';
import { CytoscapeView } from './components/CytoscapeView';

declare function acquireVsCodeApi(): {
  postMessage(message: any): void;
  getState(): any;
  setState(state: any): void;
};

let vscodeApi: any = null;
try {
  vscodeApi = acquireVsCodeApi();
} catch {}

export const App: React.FC = () => {
  const [viewMode, setViewMode] = useState<'flow' | 'full'>('flow');
  const [connectionStatus, setConnectionStatus] = useState<'connecting' | 'connected' | 'disconnected'>('connecting');
  const [allProjects, setAllProjects] = useState<string[]>([]);
  const [selectedProject, setSelectedProject] = useState<string>('');
  const [flowGraph, setFlowGraph] = useState<GraphData | null>(null);
  const [fullGraph, setFullGraph] = useState<GraphData | null>(null);
  const [cypherQuery, setCypherQuery] = useState<string>('');
  const [selectedDrawerNode, setSelectedDrawerNode] = useState<GraphNode | null>(null);

  const wsRef = useRef<WebSocket | null>(null);
  const workspaceRootRef = useRef<string>('');

  const sendWsMessage = useCallback((msg: WebSocketMessage) => {
    if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
      wsRef.current.send(JSON.stringify(msg));
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

  const handleSelectProject = useCallback((project: string) => {
    if (!project) return;
    setSelectedProject(project);
    requestDependencies(project);
  }, [requestDependencies]);

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
    if (wsRef.current) {
      try {
        wsRef.current.close();
      } catch {}
      wsRef.current = null;
    }

    setConnectionStatus('connecting');
    const ws = new WebSocket(url);
    wsRef.current = ws;

    ws.onopen = () => {
      setConnectionStatus('connected');
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

      // 2. Fetch dependencies for default/first project
      requestDependencies();

      // 3. Pre-fetch full architecture for the Cytoscape view
      requestArchitecture();
    };

    ws.onmessage = (event) => {
      try {
        const msg: WebSocketMessage = JSON.parse(event.data);
        switch (msg.type) {
          case 'HANDSHAKE_RESPONSE': {
            const resp = msg.payload as HandshakeResponse;
            console.log(`Connected to CodeExplorer ${resp.serverVersion}`);
            break;
          }

          case 'QUERY_RESPONSE': {
            const resp = msg.payload as QueryResponse;
            if (resp.success && resp.graph) {
              // Check if this is a 3-column flow response or a full architecture response
              const hasColumns = resp.graph.nodes?.some((n) => n.properties?.column);
              if (hasColumns) {
                setFlowGraph(resp.graph);
                const metadata = resp.graph.metadata;
                if (metadata?.selectedProject) {
                  setSelectedProject(metadata.selectedProject);
                }
                if (metadata?.allProjects) {
                  try {
                    const parsed = JSON.parse(metadata.allProjects) as string[];
                    setAllProjects(parsed);
                  } catch {}
                }
              } else {
                setFullGraph(resp.graph);
              }
            } else if (!resp.success && vscodeApi) {
              vscodeApi.postMessage({
                type: 'SHOW_ERROR',
                message: resp.errorMessage || 'Query failed',
              });
            }
            break;
          }
        }
      } catch (err) {
        console.error('Failed to parse WS message:', err);
      }
    };

    ws.onerror = () => {
      setConnectionStatus('disconnected');
    };

    ws.onclose = () => {
      setConnectionStatus('disconnected');
    };
  }, [requestDependencies, requestArchitecture]);

  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      const msg = event.data;
      switch (msg.type) {
        case 'SERVER_CONFIG':
          workspaceRootRef.current = msg.workspaceRoot;
          connectWebSocket(msg.wsUrl);
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
      if (wsRef.current) {
        wsRef.current.close();
      }
    };
  }, [connectWebSocket]);

  return (
    <div id="app">
      <Toolbar
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        allProjects={allProjects}
        selectedProject={selectedProject}
        onSelectProject={handleSelectProject}
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
      />

      <main className="main-viewport">
        {viewMode === 'flow' ? (
          <ProjectFlowView
            graph={flowGraph}
            onSelectProject={handleSelectProject}
            onOpenFile={handleOpenFile}
          />
        ) : (
          <CytoscapeView
            graph={fullGraph}
            onOpenFile={handleOpenFile}
            onSelectNode={setSelectedDrawerNode}
          />
        )}
      </main>

      {/* Slide-out drawer for Cytoscape node inspection */}
      {selectedDrawerNode && (
        <aside className="drawer">
          <div className="drawer-header">
            <span className="badge">{selectedDrawerNode.kind}</span>
            <h3>{selectedDrawerNode.displayName || selectedDrawerNode.name}</h3>
            <button className="close-btn" onClick={() => setSelectedDrawerNode(null)}>
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
  );
};
