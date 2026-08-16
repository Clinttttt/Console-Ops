import { toLogSource, validateOptionalLogSource } from './project-log-source-form';

/**
 * These rules mirror what the API enforces, so the operator is told at the field rather than by a
 * rejected save. Console Ops queries the provider with these values, so a value that cannot be real is
 * refused here too.
 */
describe('project log source form', () => {
  const workspace = '6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8';

  it('accepts a blank source, because it is optional', () => {
    expect(validateOptionalLogSource('', '')).toBeNull();
    expect(validateOptionalLogSource('   ', '  ')).toBeNull();
    expect(toLogSource('', '')).toBeNull();
  });

  it('accepts a complete source and trims it', () => {
    expect(validateOptionalLogSource(` ${workspace} `, ' spinner-api ')).toBeNull();
    expect(toLogSource(` ${workspace} `, ' spinner-api ')).toEqual({
      workspaceId: workspace,
      containerAppName: 'spinner-api',
    });
  });

  it('asks for the missing half rather than sending a source that cannot be queried', () => {
    expect(validateOptionalLogSource(workspace, '')).toContain('container app name');
    expect(validateOptionalLogSource('', 'spinner-api')).toContain('workspace ID');
    // Nothing is sent while a half is missing, so the API is never asked to reject it.
    expect(toLogSource(workspace, '')).toBeNull();
    expect(toLogSource('', 'spinner-api')).toBeNull();
  });

  it('requires the workspace to look like a workspace id', () => {
    expect(validateOptionalLogSource('not-a-guid', 'spinner-api')).toContain('GUID');
  });

  it('requires a container app name Azure would accept', () => {
    expect(validateOptionalLogSource(workspace, 'Spinner-API')).not.toBeNull();
    expect(validateOptionalLogSource(workspace, 'spinner--api')).not.toBeNull();
    expect(validateOptionalLogSource(workspace, '1spinner')).not.toBeNull();
    expect(validateOptionalLogSource(workspace, 'spinner_api')).not.toBeNull();
    expect(validateOptionalLogSource(workspace, 'a'.repeat(33))).toContain('32 characters');
    expect(validateOptionalLogSource(workspace, 'spinner-api-2')).toBeNull();
  });
});
