import { Observable } from 'rxjs';

import { WorkflowInventory } from '../contracts/workflows';

/**
 * Port for the Workflows screen.
 *
 * Read-only: the browser asks Console Ops, and Console Ops asks the provider. No token, repository call, or
 * classification decision happens here.
 */
export abstract class WorkflowsDataSource {
  abstract load(): Observable<WorkflowInventory>;
}
