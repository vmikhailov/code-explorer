export type ArchiveFormat = 'zip' | 'tar.gz';

export type EngineSource =
  | 'custom'
  | 'dotnet-tool'
  | 'downloaded'
  | 'path'
  | 'bundled';

export interface TargetInfo {
  platform: string;
  arch: string;
  rid: string;
  archiveName: string;
  binName: string;
  format: ArchiveFormat;
}

export interface EngineVersionConfig {
  pattern: string;
  fallback: string;
}

export interface ParsedSemver {
  major: number;
  minor: number;
  patch: number;
  buildNumber?: number;
  prerelease?: string;
  buildMetadata?: string;
  raw: string;
}

export interface DotnetToolInfo {
  command: string;
  args: string[];
  version: string;
}

export interface InstalledEngineCandidate {
  command: string;
  args: string[];
  version: string;
  source: EngineSource;
  score?: number;
}

export interface ProbeInstalledOptions {
  workspaceRoot?: string;
  globalStorageBinDir?: string;
  extensionRoot?: string;
  customExecutablePath?: string;
  homedir?: string;
}

export interface EngineSelectionInput {
  customExecutable?: { command: string; args: string[] } | null;
  dotnetTool?: DotnetToolInfo | null;
  downloadedEngine?: { command: string; args: string[]; version: string } | null;
}

export interface EngineSelectionResult {
  command: string;
  args: string[];
  version?: string;
  source: 'custom' | 'dotnet-tool' | 'downloaded' | 'need-download';
  reason: string;
}

export interface UpdateCheckResult {
  updated: boolean;
  currentVersion: string | null;
  targetVersion: string;
  command: string;
  message: string;
}

export type DownloadProgressCallback = (receivedBytes: number, totalBytes: number) => void;

export interface LogOutput {
  appendLine(message: string): void;
}
