import { Observable } from 'rxjs';

import { SettingsSnapshot } from '../contracts/settings';

/**
 * Port for the Settings screen.
 *
 * Two reads rather than one: loading the screen must stay cheap, and testing a credential must be something
 * the operator asks for. The provider round trip is the whole cost of this screen, so it never happens
 * without a click.
 */
export abstract class SettingsDataSource {
  /** Configuration state only. No provider is contacted. */
  abstract load(): Observable<SettingsSnapshot>;

  /** Tests every integration's credentials. Contacts each provider, so it is slow by nature. */
  abstract probe(): Observable<SettingsSnapshot>;

  /** Asks Console Ops to collect now rather than waiting for the next sweep. */
  abstract collectNow(): Observable<SettingsSnapshot>;
}
