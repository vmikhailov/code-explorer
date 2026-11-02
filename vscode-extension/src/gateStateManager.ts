import * as vscode from 'vscode';
import { ServerTransport } from './serverTransport';
import {
  GateSuiteSummaryDto,
  RatchetGatesResponseDto,
  GateItemDto,
  SaveGateResponseDto,
  TestGateResponseDto,
} from '../../proto/types';

export interface GateSuiteChangeEvent {
  suite: GateSuiteSummaryDto | null;
  isEvaluating: boolean;
  evaluatingGateId?: string | null;
  actionStatus?: string | null;
  error?: string | null;
}

/**
 * GateStateManager - Centralized single source of truth for Semantic Invariant Gates (SIG).
 * All UI surfaces (Left TreeView, Right Webview, Command Palette) access gates via this service,
 * communicating with the backend exclusively through ServerTransport.
 */
export class GateStateManager implements vscode.Disposable {
  private static _instance: GateStateManager | null = null;

  private _suite: GateSuiteSummaryDto | null = null;
  private _isEvaluating = false;
  private _evaluatingGateId: string | null = null;
  private _inFlightEvaluation: Promise<GateSuiteSummaryDto | null> | null = null;
  private _inFlightLoad: Promise<GateSuiteSummaryDto | null> | null = null;
  private _inFlightScope: string | null = null; // 'all' or gateId
  private readonly _disposables: vscode.Disposable[] = [];

  private readonly _onDidChangeSuite = new vscode.EventEmitter<GateSuiteChangeEvent>();
  public readonly onDidChangeSuite: vscode.Event<GateSuiteChangeEvent> = this._onDidChangeSuite.event;

  public static initialize(
    transport: ServerTransport,
    getWorkspaceRoot: () => string | undefined,
    outputChannel: vscode.OutputChannel
  ): GateStateManager {
    if (!GateStateManager._instance) {
      GateStateManager._instance = new GateStateManager(transport, getWorkspaceRoot, outputChannel);
    }
    return GateStateManager._instance;
  }

  public static getInstance(): GateStateManager {
    if (!GateStateManager._instance) {
      throw new Error('GateStateManager has not been initialized yet.');
    }
    return GateStateManager._instance;
  }

  private constructor(
    private readonly transport: ServerTransport,
    private readonly getWorkspaceRoot: () => string | undefined,
    private readonly outputChannel: vscode.OutputChannel
  ) {
    this._disposables.push(
      this.transport.onDidServerStart(() => {
        this.invalidate();
      }),
      this.transport.onDidServerStop(() => {
        this.invalidate();
      }),
      this._onDidChangeSuite.event((ev) => {
        this.transport.publish('gates:updated', ev);
      })
    );
  }

  public getSuite(): GateSuiteSummaryDto | null {
    return this._suite;
  }

  public isEvaluating(): boolean {
    return this._isEvaluating;
  }

  public evaluatingGateId(): string | null {
    return this._evaluatingGateId;
  }

  public invalidate(): void {
    this._suite = null;
    this._isEvaluating = false;
    this._evaluatingGateId = null;
    this._inFlightEvaluation = null;
    this._inFlightLoad = null;
    this._inFlightScope = null;
    this._onDidChangeSuite.fire({
      suite: null,
      isEvaluating: false,
      evaluatingGateId: null,
    });
  }

  public setSuite(suite: GateSuiteSummaryDto): void {
    this._suite = suite;
    this._isEvaluating = false;
    this._evaluatingGateId = null;
    this._inFlightEvaluation = null;
    this._inFlightLoad = null;
    this._inFlightScope = null;
    this._onDidChangeSuite.fire({
      suite,
      isEvaluating: false,
      evaluatingGateId: null,
    });
  }

  /**
   * Loads the gate suite definitions or cached suite without executing heavy graph queries.
   * Gates are ONLY evaluated upon explicit user action (e.g. evaluateSuite / POST /api/gates/check).
   */
  public async loadSuite(force = false): Promise<GateSuiteSummaryDto | null> {
    if (!force && this._suite) {
      return this._suite;
    }

    if (this._inFlightLoad) {
      return this._inFlightLoad;
    }

    if (this._inFlightEvaluation) {
      return this._inFlightEvaluation;
    }

    this._inFlightLoad = (async (): Promise<GateSuiteSummaryDto | null> => {
      try {
        const root = this.getWorkspaceRoot();
        if (!root) {
          return null;
        }

        const res = await this.transport.get<GateSuiteSummaryDto>('/api/gates', {
          workspaceRoot: root,
        });

        if (res && Array.isArray(res.gates)) {
          this._suite = res;
          this._onDidChangeSuite.fire({
            suite: this._suite,
            isEvaluating: false,
            evaluatingGateId: null,
            actionStatus: undefined,
          });
        }

        return this._suite;
      } catch (err: any) {
        this.outputChannel.appendLine(`[GateStateManager] loadSuite error: ${err?.message}`);
        return null;
      } finally {
        this._inFlightLoad = null;
      }
    })();

    return this._inFlightLoad;
  }

  /**
   * Evaluates all gates or a specific gate by ID.
   * Deduplicates concurrent calls: if an evaluation is already in flight, attaches to the in-flight operation.
   */
  public async evaluateSuite(gateId?: string): Promise<GateSuiteSummaryDto | null> {
    const scope = gateId ? gateId.trim() : 'all';

    // Deduplication check:
    // If a full evaluation ('all') is already in flight, any gate request or all-gates request can await it.
    if (this._inFlightEvaluation && (this._inFlightScope === 'all' || this._inFlightScope === scope)) {
      return this._inFlightEvaluation;
    }

    this._isEvaluating = true;
    this._evaluatingGateId = gateId || null;
    this._inFlightScope = scope;

    const actionText = gateId ? `Evaluating gate '${gateId}'...` : 'Evaluating all gates...';
    this.outputChannel.appendLine(`[GateStateManager] ${actionText}`);

    this._onDidChangeSuite.fire({
      suite: this._suite,
      isEvaluating: true,
      evaluatingGateId: this._evaluatingGateId,
      actionStatus: actionText,
    });

    const runEvaluation = async (): Promise<GateSuiteSummaryDto | null> => {
      try {
        const root = this.getWorkspaceRoot();
        if (!root) {
          throw new Error('No active workspace folder.');
        }

        const res = await this.transport.post<GateSuiteSummaryDto>(
          '/api/gates/check',
          gateId ? { gateId } : {},
          { workspaceRoot: root, timeoutMs: 120000 }
        );

        if (res && Array.isArray(res.gates)) {
          if (res.gates.length > 1) {
            // Backend returned full updated suite containing all gates
            this._suite = res;
          } else if (gateId && this._suite && Array.isArray(this._suite.gates)) {
            // Backend returned single updated gate: match by ID to avoid overwriting with the first gate
            const targetId = gateId.trim().toLowerCase();
            const updatedGate = res.gates.find((g) => g.id.trim().toLowerCase() === targetId) || res.gates[0];
            const nextGates = [...this._suite.gates];
            const idx = nextGates.findIndex((g) => g.id.trim().toLowerCase() === targetId);
            if (idx >= 0 && updatedGate) {
              nextGates[idx] = updatedGate;
            } else if (updatedGate) {
              nextGates.push(updatedGate);
            }

            let passed = 0;
            let failed = 0;
            let warnings = 0;
            let newViolations = 0;
            let toleratedViolations = 0;
            let ratchetOps = 0;
            for (const g of nextGates) {
              if (g.status === 'Passed') passed++;
              else if (g.status === 'PassedTolerated') {
                passed++;
                toleratedViolations += g.toleratedViolationsCount;
              } else if (g.status === 'Warning') {
                warnings++;
                newViolations += g.newViolationsCount;
                toleratedViolations += g.toleratedViolationsCount;
              } else if (g.status === 'FailedNew' || g.status === 'Error') {
                failed++;
                newViolations += g.newViolationsCount;
              }
              if (g.canRatchetDown) ratchetOps++;
            }

            this._suite = {
              ...this._suite,
              totalGates: nextGates.length,
              passedCount: passed,
              failedCount: failed,
              warningCount: warnings,
              newViolationsCount: newViolations,
              toleratedViolationsCount: toleratedViolations,
              ratchetOpportunities: ratchetOps,
              isSuccess: failed === 0,
              gates: nextGates,
            };
          } else {
            this._suite = res;
          }
        }

        const finalStatus = this._suite
          ? this._suite.isSuccess
            ? `Passed (${this._suite.passedCount}/${this._suite.totalGates})`
            : `${this._suite.failedCount} gate(s) failed (${this._suite.newViolationsCount} new)`
          : null;

        this.outputChannel.appendLine(`[GateStateManager] Evaluation completed: ${finalStatus || 'Done'}`);

        return this._suite;
      } catch (err: any) {
        this.outputChannel.appendLine(`[GateStateManager] Evaluation failed: ${err.message}`);
        this._onDidChangeSuite.fire({
          suite: this._suite,
          isEvaluating: false,
          evaluatingGateId: null,
          error: err?.message || 'Failed to evaluate gates',
        });
        throw err;
      } finally {
        this._isEvaluating = false;
        this._evaluatingGateId = null;
        this._inFlightEvaluation = null;
        this._inFlightScope = null;

        const completionStatus = this._suite
          ? this._suite.isSuccess
            ? `Passed (${this._suite.passedCount}/${this._suite.totalGates})`
            : `${this._suite.failedCount} gate(s) failed (${this._suite.newViolationsCount} new)`
          : null;

        this._onDidChangeSuite.fire({
          suite: this._suite,
          isEvaluating: false,
          evaluatingGateId: null,
          actionStatus: completionStatus,
        });
      }
    };

    this._inFlightEvaluation = runEvaluation();
    return this._inFlightEvaluation;
  }

  /**
   * Ratchets baseline(s) for a gate or all gates, updating tolerance and reloading state.
   */
  public async ratchetSuite(gateId?: string, updateAll = false): Promise<RatchetGatesResponseDto> {
    this._isEvaluating = true;
    this._evaluatingGateId = gateId || null;
    const actionText = gateId ? `Ratcheting ${gateId}...` : 'Ratcheting baselines...';

    this._onDidChangeSuite.fire({
      suite: this._suite,
      isEvaluating: true,
      evaluatingGateId: this._evaluatingGateId,
      actionStatus: actionText,
    });

    try {
      const root = this.getWorkspaceRoot();
      if (!root) {
        throw new Error('No active workspace folder.');
      }

      const res = await this.transport.post<RatchetGatesResponseDto>(
        '/api/gates/ratchet',
        gateId ? { gateId } : { updateAll: updateAll || !gateId },
        { workspaceRoot: root }
      );

      if (res && res.suite) {
        this._suite = res.suite;
      } else {
        await this.evaluateSuite(undefined);
      }

      return res;
    } catch (err: any) {
      this.outputChannel.appendLine(`[GateStateManager] Ratchet failed: ${err.message}`);
      this._onDidChangeSuite.fire({
        suite: this._suite,
        isEvaluating: false,
        evaluatingGateId: null,
        error: err?.message || 'Failed to ratchet baseline',
      });
      throw err;
    } finally {
      this._isEvaluating = false;
      this._evaluatingGateId = null;
      this._onDidChangeSuite.fire({
        suite: this._suite,
        isEvaluating: false,
        evaluatingGateId: null,
      });
    }
  }

  /**
   * Initializes starter gate template and updates suite.
   */
  public async initStarterGates(overwrite = false): Promise<RatchetGatesResponseDto> {
    this._isEvaluating = true;
    this._onDidChangeSuite.fire({
      suite: this._suite,
      isEvaluating: true,
      actionStatus: 'Initializing starter gates...',
    });

    try {
      const root = this.getWorkspaceRoot();
      if (!root) {
        throw new Error('No active workspace folder.');
      }

      const res = await this.transport.post<RatchetGatesResponseDto>(
        '/api/gates/init',
        { overwrite },
        { workspaceRoot: root }
      );

      if (res && res.suite) {
        this._suite = res.suite;
      }

      return res;
    } catch (err: any) {
      this.outputChannel.appendLine(`[GateStateManager] Init failed: ${err.message}`);
      this._onDidChangeSuite.fire({
        suite: this._suite,
        isEvaluating: false,
        evaluatingGateId: null,
        error: err?.message || 'Failed to init starter gate',
      });
      throw err;
    } finally {
      this._isEvaluating = false;
      this._onDidChangeSuite.fire({
        suite: this._suite,
        isEvaluating: false,
        evaluatingGateId: null,
      });
    }
  }

  /**
   * Saves or updates a gate YAML definition, re-evaluates the suite, and notifies all UI surfaces.
   */
  public async saveGate(gate: Partial<GateItemDto>, targetPath?: string): Promise<SaveGateResponseDto> {
    this.outputChannel.appendLine(`[GateStateManager] Saving gate '${gate.id}'...`);
    try {
      const root = this.getWorkspaceRoot();
      if (!root) {
        throw new Error('No active workspace folder.');
      }

      const res = await this.transport.post<SaveGateResponseDto>(
        '/api/gates/save',
        { gate, targetPath },
        { workspaceRoot: root }
      );

      if (res && res.suite) {
        this.setSuite(res.suite);
      } else {
        await this.loadSuite(true);
      }

      return res;
    } catch (err: any) {
      this.outputChannel.appendLine(`[GateStateManager] Save gate failed: ${err.message}`);
      throw err;
    }
  }

  /**
   * Validates and dry-runs a gate Cypher query against the graph, returning detected violations.
   */
  public async testGateQuery(
    query: string,
    gateId?: string,
    parameters?: Record<string, string>,
    message?: string,
    target?: string
  ): Promise<TestGateResponseDto> {
    const root = this.getWorkspaceRoot();
    if (!root) {
      throw new Error('No active workspace folder.');
    }

    return await this.transport.post<TestGateResponseDto>(
      '/api/gates/test',
      { query, gateId, parameters, message, target },
      { workspaceRoot: root }
    );
  }

  public dispose(): void {
    for (const d of this._disposables) {
      d.dispose();
    }
    this._disposables.length = 0;
    this._onDidChangeSuite.dispose();
    GateStateManager._instance = null;
  }
}
