/**
 * How a .NET logger category is shortened for display.
 *
 * Categories are namespaces, and the full one is often longer than the space it has:
 * `Microsoft.AspNetCore.Hosting.Diagnostics` either truncates to something that identifies nothing, or wraps
 * in the middle of a word. The last two segments are what tell one emitter from another.
 *
 * Shared by the stream line and the detail rail so both shorten identically. Wherever this is used the full
 * value stays available - on a tooltip, and in full in the detail rail's Source row - so nothing is hidden.
 */
export function shortenCategory(category: string): string {
  const segments = category.split('.');
  return segments.length <= 2 ? category : segments.slice(-2).join('.');
}
