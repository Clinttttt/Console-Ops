import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { PipelineFlow, PipelineStage } from '../../../core/contracts/dashboard-overview';
import { Icon, IconName } from '../../../core/ui/icon';

const STAGE_ICONS: Readonly<Record<PipelineStage['key'], IconName>> = {
  source: 'github',
  ci: 'ciCd',
  application: 'cube',
};

/** Source -> CI/CD -> Runtime relationship, with the observed outcome after the last stage. */
@Component({
  selector: 'co-pipeline-flow',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './pipeline-flow.html',
  styleUrl: './pipeline-flow.scss',
})
export class PipelineFlowSection {
  readonly flow = input.required<PipelineFlow>();

  protected iconFor(stage: PipelineStage): IconName {
    return STAGE_ICONS[stage.key];
  }
}
