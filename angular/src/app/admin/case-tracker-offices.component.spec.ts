import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { of, throwError } from 'rxjs';
import { CaseTrackerOfficesComponent } from './case-tracker-offices.component';

/**
 * The operator's only view of the Case Tracker push, and its only control over it.
 *
 * THE ASSERTION THAT MATTERS MOST HERE is that a successful toggle REPLACES the row with the
 * server's response rather than flipping the local copy. The component says why in a comment at
 * :251 -- the response carries a refreshed pending count, which is the number the operator needs
 * immediately after enabling. Nothing enforced that until now, and "just set pushEnabled = !old"
 * is the obvious simplification someone would reach for.
 *
 * Its near-twin integration-failures.component.ts does the OPPOSITE on success -- removes the row
 * locally -- for its own documented reason. The two specs are deliberately not copies: if both
 * asserted the same thing the pair would look covered and prove one behaviour.
 */
describe('CaseTrackerOfficesComponent', () => {
  interface Signal<T> {
    (): T;
    set(value: T): void;
  }
  interface Probe {
    ngOnInit(): void;
    offices: Signal<unknown[]>;
    loading: Signal<boolean>;
    error: Signal<string | null>;
    saving: Signal<string | null>;
    confirming: Signal<{ officeId: string; action: 'start' | 'return' } | null>;
    load(): Promise<void>;
    toggle(office: unknown): Promise<void>;
    askFeed(office: unknown, action: 'start' | 'return'): void;
    isConfirming(office: unknown, action: 'start' | 'return'): boolean;
    confirmFeed(office: unknown): Promise<void>;
  }

  let request: jasmine.Spy;

  const downtown = {
    officeId: 'office-1',
    officeName: 'Example Downtown Clinic',
    pushEnabled: false,
    pendingCount: 7,
  };

  /** Lets the component's `await firstValueFrom(...)` settle before assertions. */
  function settle(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
  }

  beforeEach(() => {
    request = jasmine.createSpy('request').and.returnValue(of([downtown]));
    TestBed.configureTestingModule({
      imports: [CaseTrackerOfficesComponent],
      providers: [{ provide: RestService, useValue: { request } }],
    });
  });

  function probe(): Probe {
    // Never change-detected: the template is irrelevant to every assertion here.
    return TestBed.createComponent(CaseTrackerOfficesComponent)
      .componentInstance as unknown as Probe;
  }

  describe('load', () => {
    it('asks the case-tracker office endpoint', async () => {
      probe().ngOnInit();
      await settle();
      expect(request).toHaveBeenCalled();
      const [config] = request.calls.mostRecent().args as [{ method: string; url: string }];
      expect(config.method).toBe('GET');
      expect(config.url).toBe('/api/app/case-tracker/offices');
    });

    it('publishes the returned offices and clears the loading flag', async () => {
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      expect(cmp.offices()).toEqual([downtown]);
      expect(cmp.loading()).toBeFalse();
    });

    it('treats a null response as an empty list', async () => {
      request.and.returnValue(of(null));
      const cmp = probe();
      await cmp.load();
      expect(cmp.offices()).toEqual([]);
    });

    it('explains a failed load and still clears the loading flag', async () => {
      request.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      await cmp.load();
      expect(cmp.error()).toBe('The clinic list could not be loaded. Please try again.');
      expect(cmp.loading()).toBeFalse();
    });

    it('clears a previous error when a new load starts', async () => {
      const cmp = probe();
      cmp.error.set('stale message');
      await cmp.load();
      expect(cmp.error()).toBeNull();
    });
  });

  describe('toggle', () => {
    it('sends the INVERSE of the current push setting', async () => {
      const cmp = probe();
      await cmp.toggle(downtown);
      const [config] = request.calls.mostRecent().args as [
        { method: string; url: string; body: { enabled: boolean } },
      ];
      expect(config.method).toBe('PUT');
      expect(config.url).toBe('/api/app/case-tracker/offices/office-1/push');
      // downtown.pushEnabled is false, so the request must ask for true.
      expect(config.body).toEqual({ enabled: true });
    });

    it('sends false when the office is currently enabled', async () => {
      // The positive control for the inversion: a literal `true` would pass the
      // test above and fail this one.
      const cmp = probe();
      await cmp.toggle({ ...downtown, pushEnabled: true });
      const [config] = request.calls.mostRecent().args as [{ body: { enabled: boolean } }];
      expect(config.body).toEqual({ enabled: false });
    });

    // THE LOAD-BEARING ONE. The row must come from the server, not from local
    // arithmetic, because the response carries a refreshed pendingCount.
    it('replaces the row with the server response, including the refreshed count', async () => {
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      const updated = { ...downtown, pushEnabled: true, pendingCount: 2 };
      request.and.returnValue(of(updated));
      await cmp.toggle(downtown);
      expect(cmp.offices()).toEqual([updated]);
    });

    it('does not reload the whole list after a toggle', async () => {
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      request.calls.reset();
      request.and.returnValue(of({ ...downtown, pushEnabled: true }));
      await cmp.toggle(downtown);
      const calls = request.calls.allArgs() as Array<[{ method: string }]>;
      expect(calls.filter(([config]) => config.method === 'GET')).toHaveSize(0);
    });

    it('leaves other offices untouched', async () => {
      const other = { ...downtown, officeId: 'office-2', officeName: 'Example Uptown Clinic' };
      request.and.returnValue(of([downtown, other]));
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      request.and.returnValue(of({ ...downtown, pushEnabled: true }));
      await cmp.toggle(downtown);
      expect(cmp.offices()[1]).toEqual(other);
    });

    it('keeps the existing row when the server returns nothing', async () => {
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      request.and.returnValue(of(null));
      await cmp.toggle(downtown);
      expect(cmp.offices()).toEqual([downtown]);
    });

    it('marks only the office being saved, and clears it afterwards', async () => {
      const cmp = probe();
      const inFlight = cmp.toggle(downtown);
      expect(cmp.saving()).toBe('office-1');
      await inFlight;
      expect(cmp.saving()).toBeNull();
    });

    it('names the office when the toggle fails', async () => {
      request.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      await cmp.toggle(downtown);
      expect(cmp.error()).toBe('Could not change the push setting for Example Downtown Clinic.');
    });

    it('falls back to the id when the office has no name', async () => {
      request.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      await cmp.toggle({ ...downtown, officeName: '' });
      expect(cmp.error()).toBe('Could not change the push setting for office-1.');
    });

    it('releases the saving flag when the toggle fails', async () => {
      request.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      await cmp.toggle(downtown);
      expect(cmp.saving()).toBeNull();
    });
  });

  // #927: the cutover between push and the changes feed. Both actions need an inline confirm, POST to a
  // literal path, and -- like the toggle -- replace the row from the server's response.
  describe('feed actions', () => {
    const enabled = { ...downtown, pushEnabled: true, pendingCount: 0 };
    const fed = { ...enabled, feedActive: true, outstandingCount: 4, lastRequestAt: null };

    async function loaded(rows: unknown[]): Promise<Probe> {
      request.and.returnValue(of(rows));
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      request.calls.reset();
      return cmp;
    }

    it('asks for a confirm first, and only for the office and action chosen', async () => {
      const cmp = await loaded([enabled]);
      cmp.askFeed(enabled, 'start');
      expect(cmp.isConfirming(enabled, 'start')).toBeTrue();
      expect(cmp.isConfirming(enabled, 'return')).toBeFalse();
      expect(cmp.isConfirming({ ...enabled, officeId: 'office-2' }, 'start')).toBeFalse();
      expect(request).not.toHaveBeenCalled();
    });

    it('does nothing when confirmed without having been asked', async () => {
      const cmp = await loaded([enabled]);
      await cmp.confirmFeed(enabled);
      expect(request).not.toHaveBeenCalled();
    });

    it('starts the feed with a POST to the literal path and shows the returned row', async () => {
      const cmp = await loaded([enabled]);
      request.and.returnValue(of(fed));
      cmp.askFeed(enabled, 'start');

      await cmp.confirmFeed(enabled);

      const [config] = request.calls.mostRecent().args as [{ method: string; url: string }];
      expect(config.method).toBe('POST');
      expect(config.url).toBe('/api/app/case-tracker/offices/office-1/feed/start');
      expect(cmp.offices()).toEqual([fed]);
      expect(cmp.confirming()).toBeNull();
      expect(cmp.saving()).toBeNull();
    });

    it('returns an office to push with a POST to the literal path', async () => {
      const cmp = await loaded([fed]);
      request.and.returnValue(of({ ...fed, feedActive: false, outstandingCount: null }));
      cmp.askFeed(fed, 'return');

      await cmp.confirmFeed(fed);

      const [config] = request.calls.mostRecent().args as [{ method: string; url: string }];
      expect(config.url).toBe('/api/app/case-tracker/offices/office-1/feed/return-to-push');
      expect((cmp.offices()[0] as { feedActive: boolean }).feedActive).toBeFalse();
    });

    it('names the office when starting fails, and keeps the row as it was', async () => {
      const cmp = await loaded([enabled]);
      request.and.returnValue(throwError(() => new Error('boom')));
      cmp.askFeed(enabled, 'start');

      await cmp.confirmFeed(enabled);

      expect(cmp.error()).toBe('Could not start the feed for Example Downtown Clinic.');
      expect(cmp.offices()).toEqual([enabled]);
      expect(cmp.confirming()).toBeNull();
      expect(cmp.saving()).toBeNull();
    });

    it('names the office when returning to push fails', async () => {
      const cmp = await loaded([fed]);
      request.and.returnValue(throwError(() => new Error('boom')));
      cmp.askFeed(fed, 'return');

      await cmp.confirmFeed(fed);

      expect(cmp.error()).toBe('Could not return Example Downtown Clinic to push.');
    });
  });
});
