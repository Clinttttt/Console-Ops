import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { LogLevel, LogSourceKind, LogStreamScope } from '../../../core/contracts/log-stream';

/** `null` means every level, so the filter has one control rather than three checkboxes. */
export type LogLevelFilter = LogLevel | null;

/** `null` means every source kind. */
export type LogSourceFilter = LogSourceKind | null;

interface LevelOption {
  readonly value: LogLevelFilter;
  /** Three letters, as a log stream is normally read. */
  readonly label: string;
  readonly level: 'healthy' | 'warning' | 'down' | null;
}

const LEVELS: readonly LevelOption[] = [
  { value: null, label: 'All', level: null },
  { value: 'info', label: 'INF', level: 'healthy' },
  { value: 'warning', label: 'WRN', level: 'warning' },
  { value: 'error', label: 'ERR', level: 'down' },
];

const SOURCES: readonly { value: LogSourceFilter; label: string }[] = [
  { value: null, label: 'All sources' },
  { value: 'application', label: 'Application' },
  { value: 'runtime', label: 'Runtime' },
  { value: 'platform', label: 'Platform' },
];

/**
 * One restrained toolbar for the log stream.
 *
 * A log screen invites a control panel; this stays at the filters an operator actually reaches for while
 * reading: which project and environment, free text, severity, source, and whether the stream follows
 * new events.
 */
@Component({
  selector: 'co-log-filters',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EnvironmentTag, Icon],
  templateUrl: './log-filters.html',
  styleUrl: './log-filters.scss',
})
export class LogFilters {
  readonly scopes = input.required<readonly LogStreamScope[]>();
  readonly scopeId = input.required<string | null>();
  readonly query = input('');
  readonly level = input.required<LogLevelFilter>();
  readonly source = input.required<LogSourceFilter>();
  readonly live = input.required<boolean>();

  readonly scopeChange = output<string>();
  readonly queryChange = output<string>();
  readonly levelChange = output<LogLevelFilter>();
  readonly sourceChange = output<LogSourceFilter>();
  readonly liveChange = output<boolean>();

  protected readonly levels = LEVELS;
  protected readonly sources = SOURCES;

  protected readonly selected = computed<LogStreamScope | null>(
    () => this.scopes().find((scope) => this.keyFor(scope) === this.scopeId()) ?? null,
  );

  /** A scope is a project and one of its environments, so the key carries both. */
  protected keyFor(scope: LogStreamScope): string {
    return `${scope.projectId}:${scope.environment.id}`;
  }

  protected onSourceSelect(value: string): void {
    this.sourceChange.emit(value === '' ? null : (value as LogSourceKind));
  }
}
