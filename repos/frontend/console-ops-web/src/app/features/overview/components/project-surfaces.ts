import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';

import { ProjectSurface } from '../../../core/contracts/dashboard-overview';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { Sparkline } from '../../../core/ui/sparkline';
import { Status } from '../../../core/ui/status';

/** Live status of every application and service in the current environment scope. */
@Component({
  selector: 'co-project-surfaces',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, EnvironmentTag, Icon, ProjectMark, Sparkline, Status],
  templateUrl: './project-surfaces.html',
  styleUrl: './project-surfaces.scss',
})
export class ProjectSurfacesSection {
  readonly projects = input.required<readonly ProjectSurface[]>();
  readonly scopeLabel = input.required<string>();

  /** Environment id of the surface whose commit was copied, used for transient feedback. */
  protected readonly copiedSurfaceId = signal<string | null>(null);

  protected async copyCommit(project: ProjectSurface): Promise<void> {
    const sha = project.deployedVersion?.commitSha;
    if (sha === undefined || !navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(sha);
    this.copiedSurfaceId.set(project.environment.id);
  }

  /** Stable presentation-only tone derived locally; operational status never affects it. */
  protected toneFor(projectId: string): ProjectMarkTone {
    return toneForProject(projectId);
  }
}
