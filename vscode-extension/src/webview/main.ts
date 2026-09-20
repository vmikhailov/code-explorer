import cytoscape from 'cytoscape';
import dagre from 'cytoscape-dagre';
import {
  WebSocketMessage,
  HandshakeRequest,
  HandshakeResponse,
  GetArchitectureRequest,
  ExecuteCypherRequest,
  QueryResponse,
  GraphPatchEvent,
  ScanProgressEvent,
  GraphNode,
  GraphEdge,
} from '../../../proto/types';

cytoscape.use(dagre);

declare function acquireVsCodeApi(): {
  postMessage(message: any): void;
  getState(): any;
  setState(state: any): void;
};

const vscode = acquireVsCodeApi();

// UI Elements
const statusBadge = document.getElementById('connection-status') as HTMLSpanElement;
const cypherInput = document.getElementById('cypher-input') as HTMLInputElement;
const runBtn = document.getElementById('run-btn') as HTMLButtonElement;
const fitBtn = document.getElementById('fit-btn') as HTMLButtonElement;
const layoutBtn = document.getElementById('layout-btn') as HTMLButtonElement;
const refreshBtn = document.getElementById('refresh-btn') as HTMLButtonElement;
const progressBarContainer = document.getElementById('progress-bar-container') as HTMLDivElement;
const progressBarFill = document.getElementById('progress-bar-fill') as HTMLDivElement;
const progressBarText = document.getElementById('progress-bar-text') as HTMLSpanElement;

// Drawer Elements
const nodeDrawer = document.getElementById('node-drawer') as HTMLElement;
const drawerKindBadge = document.getElementById('drawer-kind-badge') as HTMLSpanElement;
const drawerTitle = document.getElementById('drawer-title') as HTMLHeadingElement;
const drawerFile = document.getElementById('drawer-file') as HTMLDivElement;
const drawerProperties = document.getElementById('drawer-properties') as HTMLDivElement;
const drawerCloseBtn = document.getElementById('drawer-close-btn') as HTMLButtonElement;
const jumpCodeBtn = document.getElementById('jump-code-btn') as HTMLButtonElement;

let cy: cytoscape.Core;
let ws: WebSocket | null = null;
let currentWsUrl = '';
let currentWorkspaceRoot = '';
let selectedNodeData: GraphNode | null = null;

// Initialize Cytoscape
function initCytoscape() {
  cy = cytoscape({
    container: document.getElementById('cy'),
    boxSelectionEnabled: false,
    autounselectify: false,
    style: [
      {
        selector: 'node',
        style: {
          'label': 'data(label)',
          'color': '#ffffff',
          'font-size': '11px',
          'text-valign': 'center',
          'text-halign': 'center',
          'background-color': '#334155',
          'border-width': 2,
          'border-color': '#64748b',
          'width': 'label',
          'height': 34,
          'padding': '10px',
          'shape': 'round-rectangle',
          'text-wrap': 'ellipsis',
          'text-max-width': '160px',
        },
      },
      {
        selector: 'node[kind = "Project"]',
        style: {
          'background-color': '#3b0764',
          'border-color': '#a855f7',
          'border-width': 3,
          'font-weight': 'bold',
          'height': 42,
          'font-size': '12px',
        },
      },
      {
        selector: 'node[kind = "Class"]',
        style: {
          'background-color': '#0c4a6e',
          'border-color': '#38bdf8',
        },
      },
      {
        selector: 'node[kind = "Database"], node[kind = "Table"]',
        style: {
          'background-color': '#064e3b',
          'border-color': '#34d399',
          'shape': 'barrel',
        },
      },
      {
        selector: 'node[kind = "Endpoint"]',
        style: {
          'background-color': '#78350f',
          'border-color': '#fbbf24',
          'shape': 'tag',
        },
      },
      {
        selector: 'node:selected',
        style: {
          'border-color': '#ffffff',
          'border-width': 4,
          'underlay-color': '#38bdf8',
          'underlay-padding': '4px',
          'underlay-opacity': 0.5,
        },
      },
      {
        selector: 'edge',
        style: {
          'width': 2,
          'line-color': '#475569',
          'target-arrow-color': '#475569',
          'target-arrow-shape': 'triangle',
          'curve-style': 'bezier',
          'label': 'data(kind)',
          'font-size': '9px',
          'color': '#94a3b8',
          'text-rotation': 'autorotate',
          'text-background-opacity': 0.8,
          'text-background-color': '#1e1e1e',
          'text-background-padding': '2px',
        },
      },
      {
        selector: 'edge:selected',
        style: {
          'width': 3,
          'line-color': '#38bdf8',
          'target-arrow-color': '#38bdf8',
        },
      },
    ],
  });

  // Node Selection & Drawer
  cy.on('tap', 'node', (evt) => {
    const node = evt.target;
    const data = node.data() as GraphNode;
    selectedNodeData = data;
    showDrawer(data);
  });

  // Double click node -> Jump to code directly
  cy.on('dbltap', 'node', (evt) => {
    const node = evt.target;
    const data = node.data() as GraphNode;
    jumpToSource(data);
  });

  // Background tap closes drawer
  cy.on('tap', (evt) => {
    if (evt.target === cy) {
      hideDrawer();
    }
  });
}

function showDrawer(node: GraphNode) {
  drawerKindBadge.textContent = node.kind || 'NODE';
  drawerTitle.textContent = node.displayName || node.name;

  if (node.filePath) {
    drawerFile.textContent = `${node.filePath}${node.lineStart ? `:${node.lineStart}` : ''}`;
    drawerFile.onclick = () => jumpToSource(node);
    drawerFile.parentElement?.classList.remove('hidden');
    jumpCodeBtn.disabled = false;
  } else {
    drawerFile.textContent = 'No file location';
    drawerFile.onclick = null;
    jumpCodeBtn.disabled = true;
  }

  drawerProperties.innerHTML = '';
  if (node.properties && Object.keys(node.properties).length > 0) {
    for (const [key, val] of Object.entries(node.properties)) {
      const item = document.createElement('div');
      item.className = 'prop-item';
      item.innerHTML = `<span class="prop-key">${escapeHtml(key)}</span><span class="prop-val">${escapeHtml(val)}</span>`;
      drawerProperties.appendChild(item);
    }
  } else {
    drawerProperties.innerHTML = '<span style="color:#666">No additional properties</span>';
  }

  nodeDrawer.classList.remove('hidden');
}

function hideDrawer() {
  nodeDrawer.classList.add('hidden');
  selectedNodeData = null;
}

function jumpToSource(node: GraphNode) {
  if (node.filePath) {
    vscode.postMessage({
      type: 'OPEN_FILE',
      filePath: node.filePath,
      lineStart: node.lineStart,
      lineEnd: node.lineEnd,
    });
  }
}

function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, (m) => {
    switch (m) {
      case '&': return '&amp;';
      case '<': return '&lt;';
      case '>': return '&gt;';
      case '"': return '&quot;';
      default: return '&#39;';
    }
  });
}

// Layout helper
function applyDagreLayout() {
  const layout = cy.layout({
    name: 'dagre',
    rankDir: 'TB',
    nodeSep: 50,
    rankSep: 80,
    animate: true,
    animationDuration: 400,
  } as any);
  layout.run();
}

// Convert GraphNode and GraphEdge into Cytoscape elements
function loadGraphData(nodes: GraphNode[], edges: GraphEdge[]) {
  cy.elements().remove();

  const elements: cytoscape.ElementDefinition[] = [];

  for (const n of nodes) {
    elements.push({
      group: 'nodes',
      data: {
        id: n.id,
        label: n.displayName || n.name,
        kind: n.kind,
        filePath: n.filePath,
        lineStart: n.lineStart,
        lineEnd: n.lineEnd,
        properties: n.properties,
        parent: n.parentId,
      },
    });
  }

  for (const e of edges) {
    elements.push({
      group: 'edges',
      data: {
        id: e.id || `${e.source}->${e.target}`,
        source: e.source,
        target: e.target,
        kind: e.kind,
        properties: e.properties,
      },
    });
  }

  cy.add(elements);
  applyDagreLayout();
}

// WebSocket Management
function connectWebSocket(url: string) {
  if (ws) {
    try {
      ws.close();
    } catch {}
    ws = null;
  }

  statusBadge.className = 'status-badge connecting';
  statusBadge.textContent = 'Connecting...';

  ws = new WebSocket(url);

  ws.onopen = () => {
    statusBadge.className = 'status-badge connected';
    statusBadge.textContent = 'Connected';

    // 1. Send Handshake
    const handshake: WebSocketMessage<HandshakeRequest> = {
      type: 'HANDSHAKE_REQUEST',
      requestId: 'req_handshake_1',
      payload: {
        clientVersion: '0.1.0',
        clientName: 'vscode-extension',
        workspacePath: currentWorkspaceRoot,
      },
    };
    sendWsMessage(handshake);

    // 2. Request initial architecture graph
    requestArchitecture();
  };

  ws.onmessage = (event) => {
    try {
      const msg: WebSocketMessage = JSON.parse(event.data);
      handleIncomingMessage(msg);
    } catch (err: any) {
      console.error('Failed to parse incoming WS message:', err);
    }
  };

  ws.onerror = (err) => {
    console.error('WebSocket error:', err);
    statusBadge.className = 'status-badge disconnected';
    statusBadge.textContent = 'Error';
  };

  ws.onclose = () => {
    statusBadge.className = 'status-badge disconnected';
    statusBadge.textContent = 'Disconnected';
  };
}

function sendWsMessage(msg: WebSocketMessage) {
  if (ws && ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(msg));
  }
}

function requestArchitecture() {
  const req: WebSocketMessage<GetArchitectureRequest> = {
    type: 'GET_ARCHITECTURE_REQUEST',
    requestId: `req_arch_${Date.now()}`,
    payload: {
      format: 'graph',
    },
  };
  sendWsMessage(req);
}

function executeCypher(query: string) {
  const req: WebSocketMessage<ExecuteCypherRequest> = {
    type: 'EXECUTE_CYPHER_REQUEST',
    requestId: `req_cypher_${Date.now()}`,
    payload: {
      query,
    },
  };
  sendWsMessage(req);
}

function handleIncomingMessage(msg: WebSocketMessage) {
  switch (msg.type) {
    case 'HANDSHAKE_RESPONSE': {
      const resp = msg.payload as HandshakeResponse;
      console.log(`Connected to CodeExplorer ${resp.serverVersion} (nodes: ${resp.totalNodes}, edges: ${resp.totalEdges})`);
      break;
    }

    case 'QUERY_RESPONSE': {
      const queryResp = msg.payload as QueryResponse;
      if (queryResp.success && queryResp.graph) {
        loadGraphData(queryResp.graph.nodes || [], queryResp.graph.edges || []);
      } else if (!queryResp.success) {
        vscode.postMessage({
          type: 'SHOW_ERROR',
          message: `Query error: ${queryResp.errorMessage}`,
        });
      }
      break;
    }

    case 'GRAPH_PATCH_EVENT': {
      const patch = msg.payload as GraphPatchEvent;
      applyGraphPatch(patch);
      break;
    }

    case 'SCAN_PROGRESS_EVENT': {
      const progress = msg.payload as ScanProgressEvent;
      updateScanProgress(progress);
      break;
    }
  }
}

function applyGraphPatch(patch: GraphPatchEvent) {
  // Remove removed nodes
  if (patch.removedNodeIds && patch.removedNodeIds.length > 0) {
    for (const id of patch.removedNodeIds) {
      cy.getElementById(id).remove();
    }
  }

  // Add / update nodes
  if (patch.addedNodes && patch.addedNodes.length > 0) {
    for (const n of patch.addedNodes) {
      if (cy.getElementById(n.id).length === 0) {
        cy.add({
          group: 'nodes',
          data: {
            id: n.id,
            label: n.displayName || n.name,
            kind: n.kind,
            filePath: n.filePath,
            lineStart: n.lineStart,
            lineEnd: n.lineEnd,
            properties: n.properties,
            parent: n.parentId,
          },
        });
      }
    }
  }

  // Add edges
  if (patch.addedEdges && patch.addedEdges.length > 0) {
    for (const e of patch.addedEdges) {
      const edgeId = e.id || `${e.source}->${e.target}`;
      if (cy.getElementById(edgeId).length === 0) {
        cy.add({
          group: 'edges',
          data: {
            id: edgeId,
            source: e.source,
            target: e.target,
            kind: e.kind,
            properties: e.properties,
          },
        });
      }
    }
  }

  applyDagreLayout();
}

function updateScanProgress(progress: ScanProgressEvent) {
  if (progress.percentage >= 100) {
    progressBarContainer.classList.add('hidden');
    requestArchitecture();
  } else {
    progressBarContainer.classList.remove('hidden');
    progressBarFill.style.width = `${progress.percentage}%`;
    progressBarText.textContent = `${progress.phase || 'Scanning'} (${Math.round(progress.percentage)}%)`;
  }
}

// Event Listeners
fitBtn.addEventListener('click', () => {
  cy.animate({ fit: { eles: cy.elements(), padding: 30 }, duration: 300 });
});

layoutBtn.addEventListener('click', () => {
  applyDagreLayout();
});

refreshBtn.addEventListener('click', () => {
  requestArchitecture();
});

drawerCloseBtn.addEventListener('click', () => {
  hideDrawer();
});

jumpCodeBtn.addEventListener('click', () => {
  if (selectedNodeData) {
    jumpToSource(selectedNodeData);
  }
});

runBtn.addEventListener('click', () => {
  const query = cypherInput.value.trim();
  if (query.length > 0) {
    executeCypher(query);
  }
});

cypherInput.addEventListener('keydown', (e) => {
  if (e.key === 'Enter') {
    const query = cypherInput.value.trim();
    if (query.length > 0) {
      executeCypher(query);
    }
  }
});

// Window Message Listener (from Extension Host)
window.addEventListener('message', (event) => {
  const message = event.data;
  switch (message.type) {
    case 'SERVER_CONFIG':
      currentWsUrl = message.wsUrl;
      currentWorkspaceRoot = message.workspaceRoot;
      connectWebSocket(currentWsUrl);
      break;

    case 'TRIGGER_SCAN':
      sendWsMessage({
        type: 'TRIGGER_SCAN_REQUEST',
        requestId: `req_scan_${Date.now()}`,
        payload: { targetPath: currentWorkspaceRoot },
      });
      break;
  }
});

// Bootstrapping
initCytoscape();
vscode.postMessage({ type: 'WEBVIEW_READY' });
