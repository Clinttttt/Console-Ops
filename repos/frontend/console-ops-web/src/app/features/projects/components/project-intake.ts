import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { ProjectListItem } from '../../../core/contracts/project-registry';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';

interface IntakeStep {
  readonly title: string;
  readonly description: string;
}

/** Product copy describing what registration asks for. Not API data. */
const INTAKE_STEPS: readonly IntakeStep[] = [
  {
    title: 'Repository',
    description: 'Connect your source repository and select a branch.',
  },
  {
    title: 'Workflow',
    description: 'Optionally select the GitHub Actions workflow to observe.',
  },
  {
    title: 'Endpoints',
    description: 'Define the application, health, and version endpoints.',
  },
  {
    title: 'Environment',
    description: 'Name the environment and choose its kind.',
  },
];

/** Registration guidance and the newest registrations. */
@Component({
  selector: 'co-project-intake',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon, ProjectMark],
  templateUrl: './project-intake.html',
  styleUrl: './project-intake.scss',
})
export class ProjectIntake {
  readonly recentlyAdded = input.required<readonly ProjectListItem[]>();

  readonly showAll = output<void>();

  protected readonly steps = INTAKE_STEPS;

  protected toneFor(projectId: string): ProjectMarkTone {
    return toneForProject(projectId);
  }
}
