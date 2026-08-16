import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { LogMarker } from '../../../core/contracts/log-stream';
import { Icon, IconName } from '../../../core/ui/icon';

/**
 * A deployment or runtime event, shown inline as context rather than as a card.
 *
 * This is the only cross-screen material the stream carries, and it earns its place by explaining a
 * change in what follows: errors that begin immediately after a release are the reason this screen
 * exists. It therefore states the release and links to it, rather than repeating the Deployments screen.
 */
@Component({
  selector: 'co-log-marker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon, RouterLink],
  templateUrl: './log-marker.html',
  styleUrl: './log-marker.scss',
})
export class LogMarkerRow {
  readonly marker = input.required<LogMarker>();

  protected readonly icon = computed<IconName>(() =>
    this.marker().markerKind === 'deployment' ? 'cube' : 'refresh',
  );
}
