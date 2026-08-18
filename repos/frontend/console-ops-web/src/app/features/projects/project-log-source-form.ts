import { ProjectLogSourceInput } from '../../core/contracts/project-registration';

/**
 * Form rules for an environment's optional log source, shared by registration and editing.
 *
 * They mirror what the API enforces, so the operator is told at the field rather than by a rejected save:
 * both parts are required together, the workspace is a GUID, and the container app name has to be a name
 * Azure would accept. Console Ops queries the provider with these values, so a value that cannot be real
 * is refused before it is stored.
 */

const WORKSPACE_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const CONTAINER_APP_NAME_PATTERN = /^[a-z](?:[a-z0-9]|-(?!-))*[a-z0-9]$/;
const CONTAINER_APP_NAME_MAX_LENGTH = 32;

export function validateOptionalLogSource(
  workspaceId: string,
  containerAppName: string,
): string | null {
  const workspace = workspaceId.trim();
  const containerApp = containerAppName.trim();

  if (workspace === '' && containerApp === '') {
    return null;
  }

  if (workspace === '') {
    return 'Enter the Log Analytics workspace ID as well, or clear the container app name.';
  }

  if (containerApp === '') {
    return 'Enter the container app name as well, or clear the workspace ID.';
  }

  if (!WORKSPACE_ID_PATTERN.test(workspace)) {
    return 'Workspace ID must be a GUID, as Azure reports it for the workspace.';
  }

  if (containerApp.length > CONTAINER_APP_NAME_MAX_LENGTH) {
    return `Container app name must be ${CONTAINER_APP_NAME_MAX_LENGTH} characters or fewer.`;
  }

  return CONTAINER_APP_NAME_PATTERN.test(containerApp)
    ? null
    : 'Container app name must be lower-case letters, digits, or single hyphens, starting with a letter.';
}

/** `null` when the operator left the source blank, which the API stores as no source. */
export function toLogSource(
  workspaceId: string,
  containerAppName: string,
): ProjectLogSourceInput | null {
  const workspace = workspaceId.trim();
  const containerApp = containerAppName.trim();

  return workspace === '' || containerApp === ''
    ? null
    : { workspaceId: workspace, containerAppName: containerApp };
}
