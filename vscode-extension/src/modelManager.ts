import * as path from 'path';
import * as os from 'os';
import * as fs from 'fs';

export const MODEL_FILE_NAME = 'ce-intent-v2-q4_k_m.gguf';

export interface ModelStatus {
  exists: boolean;
  modelPath?: string;
  sizeBytes?: number;
  sizeMb?: string;
}

/**
 * Checks whether the CodeExplorer intent distillation SLM model is present on disk.
 */
export function getModelStatus(workspaceRoot?: string): ModelStatus {
  // 1. Explicit environment variable override
  const envPath = process.env.CODE_INTENT_MODEL_PATH;
  if (envPath && fs.existsSync(envPath)) {
    try {
      const stat = fs.statSync(envPath);
      return {
        exists: true,
        modelPath: envPath,
        sizeBytes: stat.size,
        sizeMb: (stat.size / (1024 * 1024)).toFixed(1),
      };
    } catch {
      // Ignore stat failures and proceed to default paths
    }
  }

  // 2. Default user profile cache: ~/.codeexplorer/models/ce-intent-v2-q4_k_m.gguf
  const defaultDir = path.join(os.homedir(), '.codeexplorer', 'models');
  const defaultPath = path.join(defaultDir, MODEL_FILE_NAME);
  if (fs.existsSync(defaultPath)) {
    try {
      const stat = fs.statSync(defaultPath);
      return {
        exists: true,
        modelPath: defaultPath,
        sizeBytes: stat.size,
        sizeMb: (stat.size / (1024 * 1024)).toFixed(1),
      };
    } catch {
      // Ignore
    }
  }

  // 3. Local workspace or dev environment fallback paths
  const candidates: string[] = [];
  if (workspaceRoot) {
    candidates.push(path.join(workspaceRoot, '..', 'code-intent-distill', 'models', MODEL_FILE_NAME));
    candidates.push(path.join(workspaceRoot, '..', '..', 'code-intent-distill', 'models', MODEL_FILE_NAME));
  }

  for (const cand of candidates) {
    try {
      const full = path.resolve(cand);
      if (fs.existsSync(full)) {
        const stat = fs.statSync(full);
        return {
          exists: true,
          modelPath: full,
          sizeBytes: stat.size,
          sizeMb: (stat.size / (1024 * 1024)).toFixed(1),
        };
      }
    } catch {
      // Ignore invalid candidates
    }
  }

  return {
    exists: false,
    modelPath: defaultPath,
  };
}
