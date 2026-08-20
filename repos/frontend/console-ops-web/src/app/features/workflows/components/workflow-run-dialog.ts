import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';

import { Workflow, WorkflowInput } from '../../../core/contracts/workflows';
import { Icon } from '../../../core/ui/icon';

/** What an operator has filled in, ready to be sent. */
export interface RunRequest {
  readonly reference: string;
  readonly inputs: Readonly<Record<string, string>>;
  readonly confirmation: string | null;
}

/**
 * Asks for a run: the ref, whatever the workflow declared, and stronger intent for a destructive one.
 *
 * Deliberately not a one-click button. The branch is shown rather than assumed, the inputs are the workflow's own,
 * and a workflow an operator marked destructive cannot be started until its name is typed - which is the point of
 * marking it.
 */
@Component({
  selector: 'co-workflow-run-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './workflow-run-dialog.html',
  styleUrl: './workflow-run-dialog.scss',
})
export class WorkflowRunDialog {
  readonly workflow = input.required<Workflow>();
  /** The project's registered branch, which the ref defaults to. */
  readonly defaultBranch = input.required<string>();
  readonly inputs = input<readonly WorkflowInput[]>([]);
  /** The refs the repository reports, so a branch is chosen rather than remembered. */
  readonly branches = input<readonly string[]>([]);
  readonly branchesState = input<'idle' | 'loading' | 'loaded' | 'unavailable'>('idle');
  readonly branchesBounded = input(false);
  readonly reading = input(false);
  readonly submitting = input(false);
  readonly failure = input<string | null>(null);

  readonly dismissed = output<void>();
  readonly confirmed = output<RunRequest>();

  protected readonly reference = signal('');
  protected readonly typedName = signal('');
  protected readonly values = signal<Record<string, string>>({});

  /** The ref that will be used: the project's branch until an operator changes it themselves. */
  protected readonly effectiveReference = computed(() => {
    const typed = this.reference().trim();
    return typed === '' ? this.defaultBranch() : typed;
  });

  protected readonly isDestructive = computed(() => this.workflow().risk === 'destructive');

  /**
   * Whether the run can be asked for.
   *
   * A destructive workflow needs its name typed, and every required input needs a value - either one the operator
   * gave or one the workflow declared as a default.
   */
  protected readonly canRun = computed(() => {
    if (this.submitting() || this.reading()) {
      return false;
    }

    if (this.effectiveReference().trim() === '') {
      return false;
    }

    if (this.isDestructive() && !this.nameMatches()) {
      return false;
    }

    return this.inputs()
      .filter((input) => input.required)
      .every((input) => this.valueOf(input).trim() !== '');
  });

  protected nameMatches(): boolean {
    return this.typedName().trim().toLowerCase() === this.workflow().name.trim().toLowerCase();
  }

  protected valueOf(input: WorkflowInput): string {
    return this.values()[input.name] ?? input.default ?? '';
  }

  protected setValue(input: WorkflowInput, value: string): void {
    this.values.update((current) => ({ ...current, [input.name]: value }));
  }

  protected run(): void {
    if (!this.canRun()) {
      return;
    }

    const values: Record<string, string> = {};
    for (const input of this.inputs()) {
      const value = this.valueOf(input).trim();
      if (value !== '') {
        values[input.name] = value;
      }
    }

    this.confirmed.emit({
      reference: this.effectiveReference().trim(),
      inputs: values,
      confirmation: this.isDestructive() ? this.typedName().trim() : null,
    });
  }
}
