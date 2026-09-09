import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { PatientProfileRedesignComponent } from './patient-profile-redesign.component';

/**
 * Sweep #645. Two of the findings in this file were behavioural rather than cosmetic, so they
 * are pinned here; the other nineteen are label/control pairings that the template carries.
 *
 * <p>The component had no spec at all, which is why an avatar hash and a mouse-only modal
 * both went unnoticed.</p>
 */
describe('PatientProfileRedesignComponent (sweep #645)', () => {
  interface Probe {
    confirmVisible: boolean;
    onEscapeKey(): void;
    profileDisplayName: string;
    initials: string;
    avatarColor: string;
  }

  function create() {
    TestBed.configureTestingModule({
      providers: [
        { provide: RestService, useValue: { request: () => of({ items: [], totalCount: 0 }) } },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (key: string) =>
              key === 'currentUser' ? { roles: ['Patient'], userName: 'p' } : { name: 'Office' },
            getDeep: () => null,
            getDeep$: () => of(null),
            getOne$: () => of(null),
            getAll$: () => of({}),
          },
        },
        { provide: Router, useValue: { navigateByUrl: () => undefined } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    const fixture = TestBed.createComponent(PatientProfileRedesignComponent);
    return { fixture, probe: fixture.componentInstance as unknown as Probe };
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('Escape closes the confirm modal', () => {
    it('closes it when open', () => {
      // Before the sweep the modal had a click-to-dismiss scrim and nothing else, so a
      // keyboard-only user could open it and not get out.
      const c = create();
      c.probe.confirmVisible = true;
      c.probe.onEscapeKey();
      expect(c.probe.confirmVisible).toBeFalse();
    });

    it('is inert when nothing is open', () => {
      const c = create();
      expect(() => c.probe.onEscapeKey()).not.toThrow();
      expect(c.probe.confirmVisible).toBeFalse();
    });

    it('is wired to a real document keypress, not merely callable', () => {
      const c = create();
      c.probe.confirmVisible = true;
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      expect(c.probe.confirmVisible).toBeFalse();
    });
  });

  describe('initials', () => {
    /** profileDisplayName is a getter on the component; override it per case. */
    function withName(c: ReturnType<typeof create>, name: string) {
      Object.defineProperty(c.probe, 'profileDisplayName', { get: () => name, configurable: true });
    }

    it('takes the first letter of the first and last names', () => {
      const c = create();
      withName(c, 'Ada Lovelace');
      expect(c.probe.initials).toBe('AL');
    });

    it('uses the LAST name part, not the second, for a three-part name', () => {
      // The sweep swapped `parts[parts.length - 1]` for `parts.at(-1)`; this pins that it
      // still reaches the end of the array rather than the middle.
      const c = create();
      withName(c, 'Ada Byron Lovelace');
      expect(c.probe.initials).toBe('AL');
    });

    it('gives a single initial for a one-word name', () => {
      const c = create();
      withName(c, 'Ada');
      expect(c.probe.initials).toBe('A');
    });

    it('falls back to ? for an empty name', () => {
      const c = create();
      withName(c, '');
      expect(c.probe.initials).toBe('?');
    });

    it('ignores runs of whitespace between the parts', () => {
      const c = create();
      withName(c, '  Ada    Lovelace  ');
      expect(c.probe.initials).toBe('AL');
    });
  });

  describe('avatarColor', () => {
    function withName(c: ReturnType<typeof create>, name: string) {
      Object.defineProperty(c.probe, 'profileDisplayName', { get: () => name, configurable: true });
    }

    it('is a valid hsl hue inside the 0-359 range', () => {
      const c = create();
      withName(c, 'Ada Lovelace');
      const match = /^hsl\((\d+), 42%, 42%\)$/.exec(c.probe.avatarColor);
      expect(match).withContext(c.probe.avatarColor).not.toBeNull();
      expect(Number(match![1])).toBeLessThan(360);
    });

    it('is stable for the same name and moves for a different one', () => {
      // It is a colour hash, so the contract is determinism: the same name must always come
      // back to the same hue. One instance throughout, because TestBed cannot be
      // reconfigured once a component has been created.
      const c = create();
      withName(c, 'Ada Lovelace');
      const first = c.probe.avatarColor;

      withName(c, 'Grace Hopper');
      const other = c.probe.avatarColor;

      withName(c, 'Ada Lovelace');
      expect(c.probe.avatarColor).toBe(first);
      expect(other).not.toBe(first);
    });

    it('still yields a finite hue for a name outside the basic multilingual plane', () => {
      // charCodeAt is deliberate (S7758 won't-fix -- see the getter's docstring): the loop
      // steps by code unit, so a surrogate pair contributes both halves. That is coherent and
      // deterministic, which is all a colour hash owes anyone. This pins that an astral
      // character yields neither NaN nor an out-of-range hue.
      const c = create();
      withName(c, 'Ada \u{1F600} Lovelace');
      const match = /^hsl\((\d+), 42%, 42%\)$/.exec(c.probe.avatarColor);
      expect(match).withContext(c.probe.avatarColor).not.toBeNull();
      expect(Number(match![1])).toBeLessThan(360);
    });
  });
});
