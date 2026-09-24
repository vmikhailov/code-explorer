import test from 'node:test';
import assert from 'node:assert/strict';
import { CommandManager } from './CommandManager';
import { ICommand } from './types';

class MockCommand implements ICommand {
  public executeCount = 0;
  public undoCount = 0;
  public readonly id: string;
  public readonly description: string;
  private readonly onExecute?: () => void;
  private readonly onUndo?: () => void;

  constructor(
    id: string,
    description: string,
    onExecute?: () => void,
    onUndo?: () => void
  ) {
    this.id = id;
    this.description = description;
    this.onExecute = onExecute;
    this.onUndo = onUndo;
  }

  execute(): void {
    this.executeCount++;
    this.onExecute?.();
  }

  undo(): void {
    this.undoCount++;
    this.onUndo?.();
  }
}

test('CommandManager: execute, undo, and redo flow', () => {
  const manager = new CommandManager(10);
  assert.equal(manager.canUndo, false);
  assert.equal(manager.canRedo, false);
  assert.equal(manager.undoDescription, null);
  assert.equal(manager.redoDescription, null);

  let state = 0;
  const cmd1 = new MockCommand('CMD_1', 'Increment to 1', () => { state = 1; }, () => { state = 0; });
  const cmd2 = new MockCommand('CMD_2', 'Increment to 2', () => { state = 2; }, () => { state = 1; });

  // 1. Execute first command
  manager.executeCommand(cmd1);
  assert.equal(state, 1);
  assert.equal(cmd1.executeCount, 1);
  assert.equal(manager.canUndo, true);
  assert.equal(manager.canRedo, false);
  assert.equal(manager.undoDescription, 'Increment to 1');

  // 2. Execute second command
  manager.executeCommand(cmd2);
  assert.equal(state, 2);
  assert.equal(cmd2.executeCount, 1);
  assert.equal(manager.canUndo, true);
  assert.equal(manager.canRedo, false);
  assert.equal(manager.undoDescription, 'Increment to 2');

  // 3. Undo second command
  const undoResult1 = manager.undo();
  assert.equal(undoResult1, true);
  assert.equal(state, 1);
  assert.equal(cmd2.undoCount, 1);
  assert.equal(manager.canUndo, true);
  assert.equal(manager.canRedo, true);
  assert.equal(manager.undoDescription, 'Increment to 1');
  assert.equal(manager.redoDescription, 'Increment to 2');

  // 4. Undo first command
  const undoResult2 = manager.undo();
  assert.equal(undoResult2, true);
  assert.equal(state, 0);
  assert.equal(cmd1.undoCount, 1);
  assert.equal(manager.canUndo, false);
  assert.equal(manager.canRedo, true);
  assert.equal(manager.undoDescription, null);
  assert.equal(manager.redoDescription, 'Increment to 1');

  // 5. Redo first command
  const redoResult1 = manager.redo();
  assert.equal(redoResult1, true);
  assert.equal(state, 1);
  assert.equal(cmd1.executeCount, 2);
  assert.equal(manager.canUndo, true);
  assert.equal(manager.canRedo, true);
  assert.equal(manager.undoDescription, 'Increment to 1');
  assert.equal(manager.redoDescription, 'Increment to 2');

  // 6. Redo second command
  const redoResult2 = manager.redo();
  assert.equal(redoResult2, true);
  assert.equal(state, 2);
  assert.equal(cmd2.executeCount, 2);
  assert.equal(manager.canUndo, true);
  assert.equal(manager.canRedo, false);
  assert.equal(manager.undoDescription, 'Increment to 2');
  assert.equal(manager.redoDescription, null);
});

test('CommandManager: branching history clears redo stack', () => {
  const manager = new CommandManager(10);
  const cmdA = new MockCommand('A', 'Action A');
  const cmdB = new MockCommand('B', 'Action B');
  const cmdC = new MockCommand('C', 'Action C');

  manager.executeCommand(cmdA);
  manager.executeCommand(cmdB);
  assert.equal(manager.canUndo, true);
  assert.equal(manager.canRedo, false);

  manager.undo();
  assert.equal(manager.canRedo, true);
  assert.equal(manager.redoDescription, 'Action B');

  // Executing cmdC must clear redo stack
  manager.executeCommand(cmdC);
  assert.equal(manager.canUndo, true);
  assert.equal(manager.canRedo, false);
  assert.equal(manager.undoDescription, 'Action C');
  assert.equal(manager.redoDescription, null);
});

test('CommandManager: limits stack to maxHistorySize', () => {
  const manager = new CommandManager(3);

  for (let i = 1; i <= 5; i++) {
    manager.executeCommand(new MockCommand(`CMD_${i}`, `Action ${i}`));
  }

  const snapshot = manager.getSnapshot();
  assert.equal(snapshot.undoCount, 3);
  assert.equal(snapshot.undoDescription, 'Action 5');

  // Can undo 3 times
  assert.equal(manager.undo(), true); // undid 5
  assert.equal(manager.undo(), true); // undid 4
  assert.equal(manager.undo(), true); // undid 3
  assert.equal(manager.undo(), false); // 2 and 1 were pruned
});

test('CommandManager: notify change and snapshots', () => {
  const manager = new CommandManager(10);
  let notifications = 0;

  const unsubscribe = manager.subscribe(() => {
    notifications++;
  });

  const cmd = new MockCommand('TEST', 'Test Notification');
  manager.executeCommand(cmd);
  assert.equal(notifications, 1);

  manager.undo();
  assert.equal(notifications, 2);

  manager.redo();
  assert.equal(notifications, 3);

  unsubscribe();
  manager.undo();
  assert.equal(notifications, 3); // no new notifications after unsubscribe
});

test('CommandManager: scoped history isolation per view', () => {
  const manager = new CommandManager(10);

  // 1. In 'flow' view, execute a command
  manager.setScope('flow');
  assert.equal(manager.getScope(), 'flow');
  assert.equal(manager.canUndo, false);

  manager.executeCommand(new MockCommand('FLOW_1', 'Select Project A'));
  assert.equal(manager.canUndo, true);
  assert.equal(manager.undoDescription, 'Select Project A');

  // 2. Switch to 'layers' view
  manager.setScope('layers');
  assert.equal(manager.getScope(), 'layers');
  // 'layers' should have an empty stack initially
  assert.equal(manager.canUndo, false);
  assert.equal(manager.undoDescription, null);

  manager.executeCommand(new MockCommand('LAYERS_1', 'Collapse Presentation'));
  assert.equal(manager.canUndo, true);
  assert.equal(manager.undoDescription, 'Collapse Presentation');

  // 3. Switch back to 'flow' view
  manager.setScope('flow');
  // 'flow' stack is preserved intact!
  assert.equal(manager.canUndo, true);
  assert.equal(manager.undoDescription, 'Select Project A');

  // Undo in 'flow'
  assert.equal(manager.undo(), true);
  assert.equal(manager.canUndo, false);
  assert.equal(manager.canRedo, true);
  assert.equal(manager.redoDescription, 'Select Project A');

  // 4. Switch to 'layers' view again
  manager.setScope('layers');
  // 'layers' stack is unaffected by 'flow' undo!
  assert.equal(manager.canUndo, true);
  assert.equal(manager.undoDescription, 'Collapse Presentation');
  assert.equal(manager.canRedo, false);
});
