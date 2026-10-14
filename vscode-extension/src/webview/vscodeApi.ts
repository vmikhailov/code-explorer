export interface VsCodeApi {
  postMessage(message: any): void;
  getState(): any;
  setState(state: any): void;
}

declare function acquireVsCodeApi(): VsCodeApi;

let cachedVsCodeApi: VsCodeApi | null = null;

export function getVsCodeApi(): VsCodeApi | null {
  if (cachedVsCodeApi) {
    return cachedVsCodeApi;
  }
  try {
    if (typeof acquireVsCodeApi === 'function') {
      cachedVsCodeApi = acquireVsCodeApi();
    }
  } catch {
    // Not running inside a VS Code webview or already acquired in this execution context
  }
  return cachedVsCodeApi;
}
