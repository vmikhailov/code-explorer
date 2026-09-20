# CodeExplorer Shared Protocol

This directory contains the canonical protocol definition for communication between the **CodeExplorer CLI engine** (`cli/`) and the **VS Code Extension** (`vscode-extension/`).

## Files
- `code_explorer.proto`: Canonical Protocol Buffers schema specifying message envelopes, requests, responses, and events.
- `types.ts`: Strongly-typed TypeScript interfaces generated from `code_explorer.proto` for frontend consumption.

## Wire Format: JSON over WebSocket
While `code_explorer.proto` is the formal schema specification, the WebSocket communication uses standard **Protobuf JSON** mapping:
```json
{
  "type": "GET_ARCHITECTURE_REQUEST",
  "requestId": "req-101",
  "payload": {
    "projectFilter": "",
    "format": "graph"
  }
}
```

This guarantees 100% human debuggability in browser / Webview DevTools, zero third-party binary decoders in the extension, and sub-millisecond serialization latency with `System.Text.Json` and native browser `JSON.parse`.
