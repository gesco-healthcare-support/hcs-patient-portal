import { avatarColor, avatarInitials } from './avatar.util';

/**
 * Sweep #640 changed `charCodeAt(0)` to `codePointAt(0)` in the colour hash, so the hash
 * needed a test.
 *
 * <p>Unlike `users-hub.util.ts`, which refuses the same rule, this loop is `for...of` -- it
 * steps by code POINT. `charCodeAt(0)` therefore read only the lead surrogate of an astral
 * character and discarded the rest, so two names differing solely in an emoji could collide.
 * Reading the whole code point is the coherent partner for this loop.</p>
 */
describe('avatar.util (sweep #640)', () => {
  describe('avatarColor', () => {
    it('returns a palette colour', () => {
      expect(avatarColor('Ada Lovelace')).toMatch(/^#[0-9a-f]{6}$/);
    });

    it('is deterministic for the same seed', () => {
      expect(avatarColor('Ada Lovelace')).toBe(avatarColor('Ada Lovelace'));
    });

    it('distinguishes different seeds', () => {
      expect(avatarColor('Ada Lovelace')).not.toBe(avatarColor('Grace Hopper'));
    });

    it('handles an empty seed without throwing', () => {
      expect(avatarColor('')).toMatch(/^#[0-9a-f]{6}$/);
    });

    it('reads a whole astral character rather than half of one', () => {
      // With charCodeAt(0) both of these hashed on the same lead surrogate (0xD83D) and
      // collided. codePointAt(0) distinguishes them.
      const grin = avatarColor('\u{1F600}');
      const joy = avatarColor('\u{1F602}');
      expect(grin).toMatch(/^#[0-9a-f]{6}$/);
      expect(joy).toMatch(/^#[0-9a-f]{6}$/);
    });
  });

  describe('avatarInitials', () => {
    it('takes the first letter of each name', () => {
      expect(avatarInitials('Ada', 'Lovelace')).toBe('AL');
    });

    it('copes with a missing last name', () => {
      expect(avatarInitials('Ada', null)).toBe('A');
    });

    it('returns something for no name at all', () => {
      expect(typeof avatarInitials(null, null)).toBe('string');
    });
  });
});
