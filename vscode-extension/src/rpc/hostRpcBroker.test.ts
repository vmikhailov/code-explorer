import test from 'node:test';
import assert from 'node:assert/strict';

const createMockEmitter = <T>() => {
  const listeners = new Set<(e: T) => void>();
  return {
    event: (listener: (e: T) => void) => {
      listeners.add(listener);
      return { dispose: () => listeners.delete(listener) };
    },
    fire: (data: T) => {
      for (const l of Array.from(listeners)) {
        l(data);
      }
    },
    dispose: () => listeners.clear(),
  };
};

// Mock vscode module for standalone Node test runner
const Module = require('module');
const origLoad = Module._load;
Module._load = function (request: string, parent: unknown, isMain: boolean) {
  if (request === 'vscode') {
    return {
      Disposable: class {
        constructor(private readonly fn?: () => void) {}
        dispose() {
          this.fn?.();
        }
      },
      EventEmitter: class<T> {
        private impl = createMockEmitter<T>();
        public event = this.impl.event;
        public fire = this.impl.fire;
        public dispose = this.impl.dispose;
      },
      workspace: {
        getConfiguration: () => ({
          get: (_k: string, def: unknown) => def,
        }),
      },
    };
  }
  return origLoad.apply(this, [request, parent, isMain]);
};

// eslint-disable-next-line @typescript-eslint/no-var-requires
const { EngineSupervisor } = require('../engine/engineSupervisor') as typeof import('../engine/engineSupervisor');
// eslint-disable-next-line @typescript-eslint/no-var-requires
const { HostRpcBroker } = require('./hostRpcBroker') as typeof import('./hostRpcBroker');
// eslint-disable-next-line @typescript-eslint/no-var-requires
const { WebviewRpcClient } = require('../webview/rpc/webviewRpcClient') as typeof import('../webview/rpc/webviewRpcClient');

function createPairedWebviewAndClient(broker: import('./hostRpcBroker').HostRpcBroker) {
  const hostListeners = new Set<(msg: any) => void>();
  const webviewListeners = new Set<(msg: any) => void>();

  const webviewPort: import('./hostRpcBroker').WebviewMessagePort = {
    postMessage: (msg: any) => {
      for (const l of Array.from(webviewListeners)) {
        l(msg);
      }
      return true;
    },
    onDidReceiveMessage: (listener: (e: any) => any) => {
      hostListeners.add(listener);
      return { dispose: () => hostListeners.delete(listener) };
    },
  };

  const attachment = broker.attachWebview(webviewPort);

  const client = new WebviewRpcClient({
    postMessage: (msg: any) => {
      for (const l of Array.from(hostListeners)) {
        l(msg);
      }
    },
    onMessage: (listener: (msg: any) => void) => {
      webviewListeners.add(listener);
      return () => webviewListeners.delete(listener);
    },
  });

  return {
    client,
    dispose: () => {
      client.dispose();
      attachment.dispose();
    },
  };
}

test('HostRpcBroker + EngineSupervisor: 5 parallel RPC requests during cold start queue behind single-flight ensureReady and all resolve without loss', async () => {
  const processLogs: string[] = [];
  const protocolLogs: string[] = [];
  let discoverCalls = 0;
  let startServerCalls = 0;

  const supervisor = new EngineSupervisor(
    {} as any,
    {} as any,
    () => 'C:/workspace/repo',
    { appendLine: (m: string) => processLogs.push(m), show: () => {} },
    { appendLine: (m: string) => protocolLogs.push(m), show: () => {} },
    {
      retryDelayMs: 10,
      adapter: {
        discoverBinary: async () => {
          discoverCalls++;
          await new Promise((r) => setTimeout(r, 25));
          return { command: 'ce', args: [] };
        },
        startServer: async (wsRoot) => {
          startServerCalls++;
          await new Promise((r) => setTimeout(r, 40));
          return {
            status: 'ready',
            port: 52111,
            wsUrl: 'ws://127.0.0.1:52111/ws',
            httpUrl: 'http://127.0.0.1:52111',
            workspace: wsRoot,
            version: '1.86.0',
          };
        },
        stopServer: async () => {},
        httpRequest: async <T>(_method: 'GET' | 'POST', apiPath: string): Promise<T> => {
          return { path: apiPath, nodes: [1, 2, 3] } as unknown as T;
        },
      },
    }
  );

  const broker = new HostRpcBroker(supervisor, {
    appendLine: (m: string) => protocolLogs.push(m),
  });

  const tabs = [
    createPairedWebviewAndClient(broker),
    createPairedWebviewAndClient(broker),
    createPairedWebviewAndClient(broker),
    createPairedWebviewAndClient(broker),
    createPairedWebviewAndClient(broker),
  ];

  try {
    assert.equal(supervisor.getState(), 'Idle');

    // Fire 5 concurrent diagram.fetch RPC calls while engine is Idle
    const results = await Promise.all([
      tabs[0].client.fetchDiagram<{ path: string; nodes: number[] }>({ viewType: 'system-map' }),
      tabs[1].client.fetchDiagram<{ path: string; nodes: number[] }>({ viewType: 'service-flow' }),
      tabs[2].client.fetchDiagram<{ path: string; nodes: number[] }>({ viewType: 'project-dependencies' }),
      tabs[3].client.fetchDiagram<{ path: string; nodes: number[] }>({ viewType: 'module-map', project: 'Core' }),
      tabs[4].client.fetchDiagram<{ path: string; nodes: number[] }>({ viewType: 'database-map' }),
    ]);

    assert.equal(supervisor.getState(), 'Ready');
    assert.equal(discoverCalls, 1, 'Binary discovery must run exactly once (single-flight)');
    assert.equal(startServerCalls, 1, 'ce serve must spawn exactly once (single-flight)');
    assert.equal(results.length, 5);
    assert.equal(results[0].path, '/api/view?type=system-map');
    assert.equal(results[3].path, '/api/view?type=module-map&project=Core');

    // Verify strict log channel separation
    assert.ok(
      processLogs.some((l) => l.includes('Discovering CodeExplorer binary')),
      'Process channel must contain binary discovery logs'
    );
    assert.ok(
      !processLogs.some((l) => l.includes('[Protocol]')),
      'Process channel must NOT contain protocol RPC logs'
    );
    assert.ok(
      protocolLogs.some((l) => l.includes('diagram.fetch OK') && l.includes('ms,')),
      'Protocol channel must log RPC duration in ms and payload size in bytes'
    );
  } finally {
    for (const t of tabs) t.dispose();
    broker.dispose();
    supervisor.dispose();
  }
});

test('EngineSupervisor + HostRpcBroker: transient port busy retries once, then enters Failed with 0 background timers until explicit retry', async () => {
  let startAttempts = 0;
  let shouldSucceed = false;

  const supervisor = new EngineSupervisor(
    {} as any,
    {} as any,
    () => 'C:/workspace/repo',
    { appendLine: () => {}, show: () => {} },
    { appendLine: () => {}, show: () => {} },
    {
      retryDelayMs: 15,
      adapter: {
        discoverBinary: async () => ({ command: 'ce', args: [] }),
        startServer: async (wsRoot) => {
          startAttempts++;
          if (!shouldSucceed) {
            const err: any = new Error('Port 52100 already in use (EADDRINUSE)');
            err.exitCode = 1;
            err.stderr = 'EADDRINUSE';
            throw err;
          }
          return {
            status: 'ready',
            port: 52100,
            wsUrl: 'ws://127.0.0.1:52100/ws',
            httpUrl: 'http://127.0.0.1:52100',
            workspace: wsRoot,
          };
        },
        stopServer: async () => {},
        httpRequest: async <T>(): Promise<T> => ({ ok: true } as unknown as T),
      },
    }
  );

  const broker = new HostRpcBroker(supervisor, { appendLine: () => {} });
  const pair = createPairedWebviewAndClient(broker);

  try {
    // 1. Initial call fails transiently -> retries once -> enters Failed (2 start attempts total: initial + 1 retry)
    await assert.rejects(() => pair.client.get('/api/status'), /EADDRINUSE/);
    assert.equal(supervisor.getState(), 'Failed');
    assert.equal(startAttempts, 2, 'Must attempt initial start + exactly 1 transient retry');

    // 2. While in Failed state, subsequent RPC calls reject immediately without spawning again
    await assert.rejects(() => pair.client.get('/api/status'), /EADDRINUSE/);
    assert.equal(startAttempts, 2, 'Zero additional spawn attempts allowed while in Failed state');

    // 3. Explicit user retry (`engine.retry`) recovers to Ready
    shouldSucceed = true;
    const recovered = await pair.client.request<{ port: number }>('engine.retry');
    assert.equal(recovered.port, 52100);
    assert.equal(supervisor.getState(), 'Ready');
    assert.equal(startAttempts, 3);
  } finally {
    pair.dispose();
    broker.dispose();
    supervisor.dispose();
  }
});
