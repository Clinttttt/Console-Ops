/**
 * Typed contract for `GET /api/azure/log-sources`.
 *
 * Discovery only, and read-only in Azure: Console Ops lists the resources that host applications so an
 * operator can pick a log source instead of typing a workspace GUID. Nothing here changes a resource, and
 * choosing remains an explicit act - discovery may prefill but never decide.
 *
 * It lists resources Console Ops cannot read yet, on purpose. An operator who cannot find their App Service
 * has no way to tell "Azure does not have it" from "Console Ops does not look for it". Such a resource is
 * shown with the reason and must never be offered as though selecting it would work.
 */

/** Which Azure service hosts the resource. Logs are not read the same way for each. */
export type AzureLogPlatform = 'containerApp' | 'appService';

/**
 * Whether a discovered resource can be used as a log source.
 *
 * - `readable`: Console Ops can read it, and a workspace is known.
 * - `noWorkspace`: nothing collects its logs, so there is nothing to read.
 * - `platformNotSupported`: Console Ops has no reader for this service yet.
 */
export type AzureLogSourceStatus = 'readable' | 'noWorkspace' | 'platformNotSupported';

export interface AzureLogSource {
  readonly provider: 'azure';
  readonly platform: AzureLogPlatform;
  readonly name: string;
  readonly resourceGroup: string;
  readonly subscriptionId: string;
  readonly location: string | null;
  /** Container Apps environment the app belongs to, or `null` for platforms with no equivalent. */
  readonly environmentName: string | null;
  /**
   * Workspace its logs are sent to, or `null` when Console Ops could not establish one - in which case there
   * is nothing to read and the screen must say so rather than offering it.
   */
  readonly workspaceId: string | null;
  /**
   * The address Azure reports for this resource, or `null` when it has none Console Ops could reach - a
   * container app with internal ingress resolves only inside its own network. Registering a project offers it
   * so a generated host name does not have to be copied by hand.
   */
  readonly applicationUrl: string | null;
  readonly status: AzureLogSourceStatus;
}

export interface AzureLogSources {
  readonly sources: readonly AzureLogSource[];
  /** `true` when Azure had more resources than the bounded page returned. */
  readonly hasMore: boolean;
}
