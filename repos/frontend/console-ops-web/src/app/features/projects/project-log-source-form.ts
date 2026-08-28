import { AzureLogPlatform } from '../../core/contracts/azure-discovery';
import { ProjectLogSourceInput } from '../../core/contracts/project-registration';

/**
 * Form rules for an environment's optional log source, shared by registration and editing.
 *
 * They mirror what the API enforces, so the operator is told at the field rather than by a rejected save:
 * both parts are required together, the workspace is a GUID, and the resource name has to be a name Azure
 * would accept. Console Ops queries the provider with these values, so a value that cannot be real is
 * refused before it is stored.
 *
 * The name rule depends on the platform. A container app is lower-case and short; an App Service site may be
 * mixed case and longer, so applying the container app rule to one would refuse a real site.
 */

const WORKSPACE_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const CONTAINER_APP_NAME_PATTERN = /^[a-z](?:[a-z0-9]|-(?!-))*[a-z0-9]$/;
const CONTAINER_APP_NAME_MAX_LENGTH = 32;
const SITE_NAME_PATTERN = /^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?$/;
const SITE_NAME_MAX_LENGTH = 60;

export function validateOptionalLogSource(
  workspaceId: string,
  resourceName: string,
  platform: AzureLogPlatform = 'containerApp',
): string | null {
  const workspace = workspaceId.trim();
  const resource = resourceName.trim();

  if (workspace === '' && resource === '') {
    return null;
  }

  if (workspace === '') {
    return 'Enter the Log Analytics workspace ID as well, or clear the application name.';
  }

  if (resource === '') {
    return 'Enter the application name as well, or clear the workspace ID.';
  }

  if (!WORKSPACE_ID_PATTERN.test(workspace)) {
    return 'Workspace ID must be a GUID, as Azure reports it for the workspace.';
  }

  if (platform === 'appService') {
    if (resource.length > SITE_NAME_MAX_LENGTH) {
      return `App Service name must be ${SITE_NAME_MAX_LENGTH} characters or fewer.`;
    }

    return SITE_NAME_PATTERN.test(resource)
      ? null
      : 'App Service name must be letters, digits, or hyphens, and may not start or end with a hyphen.';
  }

  if (resource.length > CONTAINER_APP_NAME_MAX_LENGTH) {
    return `Container app name must be ${CONTAINER_APP_NAME_MAX_LENGTH} characters or fewer.`;
  }

  return CONTAINER_APP_NAME_PATTERN.test(resource)
    ? null
    : 'Container app name must be lower-case letters, digits, or single hyphens, starting with a letter.';
}

/** `null` when the operator left the source blank, which the API stores as no source. */
export function toLogSource(
  workspaceId: string,
  resourceName: string,
  platform: AzureLogPlatform = 'containerApp',
): ProjectLogSourceInput | null {
  const workspace = workspaceId.trim();
  const resource = resourceName.trim();

  return workspace === '' || resource === ''
    ? null
    : { workspaceId: workspace, containerAppName: resource, platform };
}
