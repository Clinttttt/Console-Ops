import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import {
  StatusLevel,
  SystemComponentKey,
  SystemStateMatrix,
} from '../../../core/contracts/dashboard-overview';
import { Icon, IconName } from '../../../core/ui/icon';
import { Status } from '../../../core/ui/status';

interface LegendEntry {
  readonly level: StatusLevel;
  readonly label: string;
}

/** One entry per colour the matrix can show, so the legend never promises an unused state. */
const LEGEND: readonly LegendEntry[] = [
  { level: 'healthy', label: 'Healthy' },
  { level: 'running', label: 'Running' },
  { level: 'warning', label: 'Warning' },
  { level: 'degraded', label: 'Degraded' },
  { level: 'down', label: 'Down' },
  { level: 'unknown', label: 'Unknown' },
  { level: 'notApplicable', label: 'N/A' },
];

const COMPONENT_ICONS: Readonly<Record<SystemComponentKey, IconName>> = {
  api: 'api',
  database: 'database',
  cache: 'redis',
  ci: 'ciCd',
  versionSync: 'refresh',
};

/** Component health and status across the projects and environments currently in scope. */
@Component({
  selector: 'co-system-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, Status],
  templateUrl: './system-state.html',
  styleUrl: './system-state.scss',
})
export class SystemStateSection {
  readonly matrix = input.required<SystemStateMatrix>();

  protected readonly legend = LEGEND;

  protected iconFor(component: SystemComponentKey): IconName {
    return COMPONENT_ICONS[component];
  }
}
