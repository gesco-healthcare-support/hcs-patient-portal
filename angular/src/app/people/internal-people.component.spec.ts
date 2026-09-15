import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalPeopleComponent } from './internal-people.component';
import { PeopleSectionGateway } from './people-section.gateway';

/**
 * Sweep #636. This component had no spec, so the two things the sweep touched inside it were
 * unheld: the new Escape handler for the columns menu, and the date-of-birth sort key.
 *
 * <p>The component is constructed but never change-detected, so only the route subscription
 * and the methods each test calls run.</p>
 */
describe('InternalPeopleComponent (sweep #636)', () => {
  interface Probe {
    showCols: { set(v: boolean): void; (): boolean };
    onEscapeKey(): void;
    sortValue(row: unknown, key: string): unknown;
  }

  function create() {
    TestBed.configureTestingModule({
      providers: [
        { provide: ActivatedRoute, useValue: { data: of({}) } },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
        {
          provide: PeopleSectionGateway,
          // The constructor loads two lookups and the route subscription triggers list().
          useValue: {
            stateLookup: () => of([]),
            languageLookup: () => of([]),
            list: () => of([]),
            activeInvitedEmails: () => of([]),
          },
        },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    const fixture = TestBed.createComponent(InternalPeopleComponent);
    return fixture.componentInstance as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('Escape closes the columns menu', () => {
    it('closes it when open', () => {
      // Before the sweep the menu had a click-away backdrop and no keyboard exit.
      const c = create();
      c.showCols.set(true);
      c.onEscapeKey();
      expect(c.showCols()).toBeFalse();
    });

    it('is inert when the menu is already closed', () => {
      const c = create();
      expect(() => c.onEscapeKey()).not.toThrow();
      expect(c.showCols()).toBeFalse();
    });

    it('is wired to a real document Escape keypress, not just callable', () => {
      const c = create();
      c.showCols.set(true);
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      expect(c.showCols()).toBeFalse();
    });
  });

  describe('date-of-birth sort key', () => {
    it('returns a timestamp for a real date', () => {
      const c = create();
      expect(c.sortValue({ dateOfBirth: '1990-05-15' }, 'dob')).toBe(Date.parse('1990-05-15'));
    });

    it('returns null for a missing date so it sorts apart from a real one', () => {
      // The sweep replaced the bare NaN with Number.NaN; the branch it feeds must still map
      // an absent or unparseable date to null rather than letting NaN reach the comparator.
      const c = create();
      expect(c.sortValue({ dateOfBirth: null }, 'dob')).toBeNull();
      expect(c.sortValue({}, 'dob')).toBeNull();
    });

    it('returns null for a date string that cannot be parsed', () => {
      const c = create();
      expect(c.sortValue({ dateOfBirth: 'not-a-date' }, 'dob')).toBeNull();
    });
  });
});
