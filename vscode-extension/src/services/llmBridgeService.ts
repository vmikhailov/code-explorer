import * as vscode from 'vscode';
import { getModelStatus, ModelStatus } from '../modelManager';

export type AiProvider = 'builtin' | 'openai' | 'gguf';

export interface ModelDescriptor {
  id: string;
  name: string;
  vendor: string;
  family: string;
  provider: AiProvider;
  description?: string;
}

export interface AiSettings {
  provider: AiProvider;
  model: string;
  endpoint: string;
  apiKey: string;
}

export class LlmBridgeService {
  private static instance: LlmBridgeService;

  private constructor() {}

  public static getInstance(): LlmBridgeService {
    if (!LlmBridgeService.instance) {
      LlmBridgeService.instance = new LlmBridgeService();
    }
    return LlmBridgeService.instance;
  }

  public getSettings(): AiSettings {
    const config = vscode.workspace.getConfiguration('codeExplorer');
    return {
      provider: config.get<AiProvider>('ai.provider', 'builtin'),
      model: config.get<string>('ai.model', ''),
      endpoint: config.get<string>('ai.endpoint', 'http://localhost:11434/v1'),
      apiKey: config.get<string>('ai.apiKey', ''),
    };
  }

  public async saveSettings(settings: Partial<AiSettings>): Promise<void> {
    const config = vscode.workspace.getConfiguration('codeExplorer');
    if (settings.provider !== undefined) {
      await config.update('ai.provider', settings.provider, vscode.ConfigurationTarget.Global);
    }
    if (settings.model !== undefined) {
      await config.update('ai.model', settings.model, vscode.ConfigurationTarget.Global);
    }
    if (settings.endpoint !== undefined) {
      await config.update('ai.endpoint', settings.endpoint, vscode.ConfigurationTarget.Global);
    }
    if (settings.apiKey !== undefined) {
      await config.update('ai.apiKey', settings.apiKey, vscode.ConfigurationTarget.Global);
    }
  }

  /**
   * Discovers all available models across providers (VS Code/Antigravity built-in lm, OpenAI/Ollama, GGUF).
   */
  public async listAvailableModels(endpoint?: string, apiKey?: string): Promise<{
    builtinModels: ModelDescriptor[];
    externalModels: ModelDescriptor[];
    ggufStatus: ModelStatus;
  }> {
    const builtinModels: ModelDescriptor[] = [];
    const externalModels: ModelDescriptor[] = [];

    // 1. VS Code / Antigravity built-in Language Models (Copilot, Gemini, Claude)
    try {
      const lm = (vscode as any).lm;
      if (lm && typeof lm.selectChatModels === 'function') {
        const models = await lm.selectChatModels();
        if (Array.isArray(models)) {
          for (const m of models) {
            builtinModels.push({
              id: m.id || m.name,
              name: m.name || m.id,
              vendor: m.vendor || 'IDE',
              family: m.family || 'chat',
              provider: 'builtin',
              description: `Built-in IDE model (${m.vendor || 'system'}) - 0 MB download required`,
            });
          }
        }
      }
    } catch (err: any) {
      console.warn('[LlmBridge] Failed discovering built-in models:', err);
    }

    // 2. OpenAI / Ollama / LM Studio compatible endpoint
    const targetEndpoint = endpoint || this.getSettings().endpoint;
    if (targetEndpoint) {
      try {
        const cleanEp = targetEndpoint.replace(/\/$/, '');
        const res = await fetch(`${cleanEp}/models`, {
          method: 'GET',
          headers: apiKey ? { Authorization: `Bearer ${apiKey}` } : {},
          signal: AbortSignal.timeout(3000),
        });
        if (res.ok) {
          const data = (await res.json()) as any;
          const list = Array.isArray(data?.data) ? data.data : Array.isArray(data?.models) ? data.models : [];
          for (const item of list) {
            const mId = item.id || item.name;
            if (mId) {
              externalModels.push({
                id: mId,
                name: mId,
                vendor: 'OpenAI-Compatible',
                family: 'custom',
                provider: 'openai',
                description: `External endpoint (${cleanEp})`,
              });
            }
          }
        }
      } catch {
        // Non-blocking if local server is offline
      }
    }

    // 3. Local GGUF Model status
    const folders = vscode.workspace.workspaceFolders;
    const wsRoot = folders && folders.length > 0 ? folders[0].uri.fsPath : undefined;
    const ggufStatus = getModelStatus(wsRoot);

    return { builtinModels, externalModels, ggufStatus };
  }

  /**
   * Executes inference via the configured or specified provider.
   */
  public async sendPrompt(params: {
    prompt: string;
    systemPrompt?: string;
    overrideSettings?: Partial<AiSettings>;
    token?: vscode.CancellationToken;
  }): Promise<string> {
    const settings = { ...this.getSettings(), ...params.overrideSettings };

    switch (settings.provider) {
      case 'builtin':
        return this.sendViaBuiltin(params.prompt, params.systemPrompt, settings.model, params.token);

      case 'openai':
        return this.sendViaOpenAi(params.prompt, params.systemPrompt, settings.endpoint, settings.model, settings.apiKey);

      case 'gguf':
        throw new Error('GGUF inference runs natively in the ce.exe CLI engine.');

      default:
        throw new Error(`Unsupported AI provider: ${settings.provider}`);
    }
  }

  private async sendViaBuiltin(
    prompt: string,
    systemPrompt?: string,
    preferredModelId?: string,
    token?: vscode.CancellationToken
  ): Promise<string> {
    const lm = (vscode as any).lm;
    if (!lm || typeof lm.selectChatModels !== 'function') {
      throw new Error('VS Code Language Model API (vscode.lm) is not available in this IDE environment.');
    }

    let models: any[] = [];
    if (preferredModelId) {
      models = await lm.selectChatModels({ id: preferredModelId });
    }
    if (!models || models.length === 0) {
      models = await lm.selectChatModels();
    }
    if (!models || models.length === 0) {
      throw new Error('No built-in chat models found in IDE. Ensure GitHub Copilot or Antigravity AI is enabled.');
    }

    const selectedModel = models[0];
    const messages: any[] = [];
    const chatMsg = (vscode as any).LanguageModelChatMessage;

    if (systemPrompt && chatMsg?.User) {
      messages.push(chatMsg.User(`[System Instructions]\n${systemPrompt}\n\n[End System Instructions]`));
    }
    if (chatMsg?.User) {
      messages.push(chatMsg.User(prompt));
    } else {
      messages.push({ role: 1, content: prompt });
    }

    const response = await selectedModel.sendRequest(messages, {}, token);
    let output = '';
    for await (const chunk of response.text) {
      output += chunk;
    }
    return output;
  }

  private async sendViaOpenAi(
    prompt: string,
    systemPrompt: string | undefined,
    endpoint: string,
    modelName: string,
    apiKey?: string
  ): Promise<string> {
    const cleanEp = (endpoint || 'http://localhost:11434/v1').replace(/\/$/, '');
    const url = `${cleanEp}/chat/completions`;

    const messages = [];
    if (systemPrompt) {
      messages.push({ role: 'system', content: systemPrompt });
    }
    messages.push({ role: 'user', content: prompt });

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (apiKey) {
      headers.Authorization = `Bearer ${apiKey}`;
    }

    const res = await fetch(url, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        model: modelName || 'default',
        messages,
        temperature: 0.1,
      }),
    });

    if (!res.ok) {
      const errText = await res.text();
      throw new Error(`OpenAI request failed (${res.status}): ${errText}`);
    }

    const data = (await res.json()) as any;
    return data.choices?.[0]?.message?.content ?? '';
  }

  /**
   * Tests the connection with the given settings.
   */
  public async testConnection(settings: AiSettings): Promise<{ success: boolean; message: string }> {
    try {
      if (settings.provider === 'builtin') {
        const res = await this.sendPrompt({
          prompt: 'Respond with valid JSON: {"status": "ok"}',
          overrideSettings: settings,
        });
        return { success: true, message: `Connected to built-in model! Response: ${res.slice(0, 80).trim()}...` };
      }

      if (settings.provider === 'openai') {
        const res = await this.sendPrompt({
          prompt: 'Respond with valid JSON: {"status": "ok"}',
          overrideSettings: settings,
        });
        return { success: true, message: `Connected to ${settings.endpoint}! Response: ${res.slice(0, 80).trim()}...` };
      }

      if (settings.provider === 'gguf') {
        const folders = vscode.workspace.workspaceFolders;
        const wsRoot = folders && folders.length > 0 ? folders[0].uri.fsPath : undefined;
        const status = getModelStatus(wsRoot);
        if (status.exists) {
          return { success: true, message: `Local GGUF model found at ${status.modelPath} (${status.sizeMb} MB).` };
        }
        return { success: false, message: `GGUF model not found. Click 'Download Model' to download it (~940 MB).` };
      }

      return { success: false, message: `Unknown provider: ${settings.provider}` };
    } catch (err: any) {
      return { success: false, message: err.message || String(err) };
    }
  }
}
