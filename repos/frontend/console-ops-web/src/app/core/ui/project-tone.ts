import { ProjectMarkTone } from './project-mark';

const TONES: readonly ProjectMarkTone[] = ['navy', 'slate', 'amber'];

/**
 * Stable presentation-only tone for a project glyph.
 *
 * Derived from the project id so the same project keeps the same mark on every screen. Operational
 * status never affects it.
 */
export function toneForProject(projectId: string): ProjectMarkTone {
  const hash = Array.from(projectId).reduce(
    (value, character) => value + character.charCodeAt(0),
    0,
  );
  return TONES[hash % TONES.length];
}
