import * as assert from 'assert';
import * as http from 'http';
import * as net from 'net';
import type { TransportStateSnapshot } from './serverTransport';
import type { ServerInfo } from './processManager';

// Mock minimal vscode module for unit tests running in Node
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

// Inject mock vscode onto require cache if running outside VS Code host
const Module = require('module');
const origLoad = Module._load;
Module._load = function (request: string, parent: unknown, isMain: boolean) {
  if (request === 'vscode') {
    return {
      Disposable: class {
        constructor(private readonly callOnDispose: () => void) {}
        public dispose() {
          this.callOnDispose();
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
          get: (_key: string, defaultVal: unknown) => defaultVal,
        }),
      },
    };
  }
  return origLoad.apply(this, [request, parent, isMain]);
};

// eslint-disable-next-line @typescript-eslint/no-var-requires
const {
  allocateFreePort,
  isPortAvailable,
  executeHttpRequest,
  ServerTransport,
} = require('./serverTransport') as typeof import('./serverTransport');

async function runTests() {
  console.log('Running ServerTransport tests...');

  // 1. Test allocateFreePort & isPortAvailable
  {
    const port = await allocateFreePort('127.0.0.1');
    assert.ok(port > 0 && port < 65536, `Allocated port should be valid: ${port}`);
    const freeBefore = await isPortAvailable(port, '127.0.0.1');
    assert.strictEqual(freeBefore, true, 'Allocated port should be free after release');

    // Occupy port and check availability
    const blocker = net.createServer();
    await new Promise<void>((resolve) => blocker.listen(port, '127.0.0.1', () => resolve()));
    try {
      const freeWhileOccupied = await isPortAvailable(port, '127.0.0.1');
      assert.strictEqual(freeWhileOccupied, false, 'Port should report unavailable while occupied');
    } finally {
      await new Promise<void>((resolve) => blocker.close(() => resolve()));
    }
  }

  // 2. Test executeHttpRequest against a local HTTP server
  {
    const mockServer = http.createServer((req, res) => {
      if (req.url === '/api/status' && req.method === 'GET') {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ status: 'ready', nodeCount: 42 }));
        return;
      }
      if (req.url === '/api/echo' && req.method === 'POST') {
        let body = '';
        req.on('data', (chunk) => (body += chunk));
        req.on('end', () => {
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(body);
        });
        return;
      }
      res.writeHead(500, { 'Content-Type': 'text/plain' });
      res.end('Internal Error');
    });

    await new Promise<void>((resolve) => mockServer.listen(0, '127.0.0.1', () => resolve()));
    const addr = mockServer.address() as net.AddressInfo;
    const baseUrl = `http://127.0.0.1:${addr.port}`;

    try {
      const getRes = await executeHttpRequest<{ status: string; nodeCount: number }>(
        `${baseUrl}/api/status`,
        'GET',
        undefined,
        undefined,
        2000
      );
      assert.strictEqual(getRes.status, 'ready');
      assert.strictEqual(getRes.nodeCount, 42);

      const postRes = await executeHttpRequest<{ hello: string }>(
        `${baseUrl}/api/echo`,
        'POST',
        { hello: 'world' },
        undefined,
        2000
      );
      assert.strictEqual(postRes.hello, 'world');

      await assert.rejects(
        () => executeHttpRequest(`${baseUrl}/api/fail`, 'GET', undefined, undefined, 2000),
        /HTTP 500/
      );
    } finally {
      await new Promise<void>((resolve) => mockServer.close(() => resolve()));
    }
  }

  // 3. Test ServerTransport Sticky Port, Pub/Sub, and Liveness Auto-Restart
  {
    const startEmitter = createMockEmitter<ServerInfo>();
    const stopEmitter = createMockEmitter<void>();
    const unexpectedExitEmitter = createMockEmitter<{ code: number | null; workspaceRoot: string }>();

    let currentInfo: ServerInfo | null = null;
    const startedPorts: number[] = [];

    // Spin up a real local HTTP responder so checkServerHealth succeeds on the sticky port
    let healthServer: http.Server | null = null;

    const mockProcessManager: any = {
      onDidServerStart: startEmitter.event,
      onDidServerStop: stopEmitter.event,
      onDidUnexpectedExit: unexpectedExitEmitter.event,
      hasWorkspace: () => true,
      getServerInfo: () => currentInfo,
      isServerProcessAlive: () => currentInfo !== null,
      stopServer: () => {
        currentInfo = null;
        if (healthServer) {
          healthServer.close();
          healthServer = null;
        }
        stopEmitter.fire();
      },
      ensureServerStarted: async (workspaceRoot: string, opts?: { port?: number; idleTimeout?: number }) => {
        assert.ok(opts?.port && opts.port > 0, 'ServerTransport must pass sticky port to ProcessManager');
        assert.strictEqual(opts?.idleTimeout, 0, 'ServerTransport must pass idleTimeout=0');
        startedPorts.push(opts.port);

        if (!healthServer) {
          healthServer = http.createServer((req, res) => {
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ status: 'ready', nodeCount: 10 }));
          });
          await new Promise<void>((resolve) => healthServer!.listen(opts.port, '127.0.0.1', () => resolve()));
        }

        const info: ServerInfo = {
          status: 'ready',
          port: opts.port,
          wsUrl: `ws://127.0.0.1:${opts.port}/ws`,
          httpUrl: `http://127.0.0.1:${opts.port}`,
          workspace: workspaceRoot,
          version: '1.41.6',
        };
        currentInfo = info;
        startEmitter.fire(info);
        return info;
      },
    };

    const logs: string[] = [];
    const mockOutputChannel: any = {
      appendLine: (msg: string) => logs.push(msg),
    };

    const transport = ServerTransport.initialize(
      mockProcessManager,
      () => 'C:/mock/workspace',
      mockOutputChannel
    );

    try {
      const states: string[] = [];
      transport.onDidChangeState((e: TransportStateSnapshot) => states.push(e.state));

      // Test Pub/Sub mechanism
      const receivedPayloads: Array<{ count: number }> = [];
      const sub = transport.subscribe<{ count: number }>('test:topic', (payload) => {
        receivedPayloads.push(payload);
      });
      transport.publish('test:topic', { count: 1 });
      transport.publish('test:topic', { count: 2 });
      sub.dispose();
      transport.publish('test:topic', { count: 3 });
      assert.strictEqual(receivedPayloads.length, 2, 'Unsubscribed listener should not receive third event');
      assert.strictEqual(receivedPayloads[0].count, 1);
      assert.strictEqual(receivedPayloads[1].count, 2);

      // Test Sticky Port across initial start & manual restart
      const info1 = await transport.ensureConnected('C:/mock/workspace');
      const stickyPort = transport.getAllocatedPort();
      assert.ok(stickyPort && stickyPort > 0, 'Sticky port must be allocated');
      assert.strictEqual(info1.httpUrl, `http://127.0.0.1:${stickyPort}`);

      // Perform HTTP GET through transport
      const statusData = await transport.get<{ status: string; nodeCount: number }>('/api/status');
      assert.strictEqual(statusData.status, 'ready');
      assert.strictEqual(statusData.nodeCount, 10);

      // Restart and verify the exact same port is reused
      const info2 = await transport.restart('C:/mock/workspace');
      assert.strictEqual(transport.getAllocatedPort(), stickyPort, 'Allocated port must remain sticky across restarts');
      assert.strictEqual(info2.httpUrl, `http://127.0.0.1:${stickyPort}`);
      assert.strictEqual(startedPorts.length, 2);
      assert.strictEqual(startedPorts[0], startedPorts[1], 'Both starts must bind to the exact same sticky port');
    } finally {
      mockProcessManager.stopServer();
      transport.dispose();
    }
  }

  console.log('All ServerTransport tests passed!');
}

runTests().catch((err) => {
  console.error('ServerTransport test failed:', err);
  process.exit(1);
});
