/**
 * Typed contract for `GET /api/azure/log-sources`.
 *
 * Discovery only, and read-only in Azure: Console Ops lists container apps so an operator can pick a log
 * source instead of typing a workspace GUID. Nothing here changes a resource, and choosing remains an
 * explicit act - discovery may prefill but never decide.
 */

export interface AzureLogSource {
  readonly provider: 'azureContainerApps';
  readonly containerAppName: string;
  readonly resourceGroup: string;
  readonly subscriptionId: string;
  readonly location: string | null;
  /** Container Apps environment the app belongs to, or `null` when Azure did not report one. */
  readonly environmentName: string | null;
  /**
   * Workspace the app's environment sends console logs to. `null` when that environment has no log
   * configuration, in which case Console Ops cannot read this app's logs and must say so rather than
   * offering it.
   */
  readonly workspaceId: string | null;
}

export interface AzureLogSources {
  readonly containerApps: readonly AzureLogSource[];
  /** `true` when Azure had more resources than the bounded page returned. */
  readonly hasMore: boolean;
}
