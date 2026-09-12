/** Deterministic avatar color + initials, ported from the design prototype's
 *  after-common.jsx (avaColor / initials). Used by list/table patient avatars. */
const AVATAR_PALETTE = [
  '#055495',
  '#075ca1',
  '#0a4778',
  '#2f7cbf',
  '#1f6e6e',
  '#5b3ea6',
  '#82a52a',
  '#a35a26',
];

export function avatarColor(seed: string): string {
  let h = 0;
  // `for...of` yields whole code points, so codePointAt(0) is the matching read.
  // charCodeAt(0) took only the lead surrogate of an astral character and dropped the
  // rest. (Contrast users-hub.util.ts, which steps by code UNIT and refuses this rule.)
  for (const ch of seed) {
    h = (h * 31 + (ch.codePointAt(0) ?? 0)) >>> 0;
  }
  return AVATAR_PALETTE[h % AVATAR_PALETTE.length];
}

export function avatarInitials(first?: string | null, last?: string | null): string {
  const f = (first ?? '').trim();
  const l = (last ?? '').trim();
  const a = f ? f[0] : '';
  const b = l ? l[0] : '';
  return (a + b).toUpperCase() || '?';
}
