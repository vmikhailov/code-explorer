import test from 'node:test';
import assert from 'node:assert/strict';
import {
  deriveHttpUrl,
  WebviewServerTransport,
  TransportUrls,
  WebviewConnectionState,
} from './webviewTransport';

test('deriveHttpUrl: normalizes WebSocket URLs to base HTTP URLs', () => {
  assert.equal(deriveHttpUrl('ws://127.0.0.1:54321/ws'), 'http://127.0.0.1:54321');
  assert.equal(deriveHttpUrl('wss://localhost:8080/ws'), 'https://localhost:8080');
  assert.equal(deriveHttpUrl('ws://example.com:9000'), 'http://example.com:9000');
  assert.equal(deriveHttpUrl(''), '');
  assert.equal(deriveHttpUrl(undefined), '');
});

test('WebviewServerTransport: holds current URLs and updates listeners', () => {
  const transport = new WebviewServerTransport();
  try {
    transport.updateUrls('ws://127.0.0.1:61234/ws');

    assert.equal(transport.getWsUrl(), 'ws://127.0.0.1:61234/ws');
    assert.equal(transport.getHttpUrl(), 'http://127.0.0.1:61234');

    let notifiedUrls: TransportUrls | null = null;
    const unsubscribe = transport.onUrlChange((urls) => {
      notifiedUrls = urls;
    });

    transport.updateUrls('ws://127.0.0.1:62000/ws');
    assert.ok(notifiedUrls !== null);
    const received = notifiedUrls as TransportUrls;
    assert.equal(received.wsUrl, 'ws://127.0.0.1:62000/ws');
    assert.equal(received.httpUrl, 'http://127.0.0.1:62000');

    unsubscribe();
    notifiedUrls = null;
    transport.updateUrls('ws://127.0.0.1:63000/ws');
    assert.equal(notifiedUrls, null);
  } finally {
    transport.disconnect();
  }
});

test('WebviewServerTransport: connection state notifications and reconnectNow', () => {
  const transport = new WebviewServerTransport();
  try {
    const states: WebviewConnectionState[] = [];

    const unsubState = transport.onConnectionStateChange((state) => {
      states.push(state);
    });

    transport.updateUrls('ws://127.0.0.1:55555/ws');
    transport.reconnectNow();

    assert.ok(states.includes('reconnecting'));
    assert.equal(transport.getWsUrl(), 'ws://127.0.0.1:55555/ws');
    unsubState();
  } finally {
    transport.disconnect();
  }
});

test('WebviewServerTransport: fetchJson sends correct headers and parses JSON', async () => {
  const transport = new WebviewServerTransport();
  const originalFetch = global.fetch;
  try {
    transport.updateUrls('http://127.0.0.1:54321'); // Or ws://127.0.0.1:54321/ws

    let requestedUrl = '';
    let requestedInit: RequestInit | undefined;

    global.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
      requestedUrl = String(input);
      requestedInit = init;
      return {
        ok: true,
        status: 200,
        statusText: 'OK',
        json: async () => ({ success: true, count: 42 }),
      } as unknown as Response;
    }) as any;

    const data = await transport.fetchJson<{ success: boolean; count: number }>('/api/gates');
    assert.equal(data.success, true);
    assert.equal(data.count, 42);
    assert.equal(requestedUrl, 'http://127.0.0.1:54321/api/gates');
    assert.equal((requestedInit?.headers as any)?.Accept, 'application/json');
  } finally {
    global.fetch = originalFetch;
    transport.disconnect();
  }
});

test('WebviewServerTransport: fetchJson throws descriptive error on non-ok status', async () => {
  const transport = new WebviewServerTransport();
  const originalFetch = global.fetch;
  try {
    transport.updateUrls('ws://127.0.0.1:54321/ws');

    global.fetch = (async () => {
      return {
        ok: false,
        status: 404,
        statusText: 'Not Found',
        text: async () => 'Resource does not exist',
      } as unknown as Response;
    }) as any;

    await assert.rejects(
      async () => {
        await transport.fetchJson('/api/nonexistent');
      },
      /HTTP 404: Not Found - Resource does not exist/
    );
  } finally {
    global.fetch = originalFetch;
    transport.disconnect();
  }
});

test('WebviewServerTransport: wsFactory and max reconnect attempts cap', () => {
  let createdSockets = 0;
  const transport = new WebviewServerTransport({
    wsFactory: () => {
      createdSockets++;
      const mockWs: any = {
        readyState: 0,
        close: () => {},
        send: () => {},
      };
      return mockWs as unknown as WebSocket;
    },
  });

  try {
    transport.connect('ws://127.0.0.1:55555/ws');
    assert.equal(createdSockets, 1);
  } finally {
    transport.disconnect();
  }
});
