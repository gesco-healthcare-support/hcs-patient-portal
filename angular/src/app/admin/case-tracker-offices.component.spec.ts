import { TestBed } from '@angular/core/testing';
import { PermissionService, RestService } from '@abp/ng.core';
import { of, throwError } from 'rxjs';
import {
  CaseTrackerOfficesComponent,
  MANAGE_CONNECTION_POLICY,
} from './case-tracker-offices.component';

/**
 * The operator's only view of each office's Case Tracker connection, and its only control over it.
 *
 * THE ASSERTION THAT MATTERS MOST HERE is that a successful Connect or Disconnect REPLACES the row with
 * the server's response rather than flipping the local copy: the response carries the refreshed mode
 * and counts the operator checks next, and "just set pushEnabled = !old" is the obvious simplification
 * someone would reach for.
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
    confirming: Signal<{ officeId: string; action: 'connect' | 'disconnect' } | null>;
    canManage: boolean;
    load(): Promise<void>;
    isConnected(office: unknown): boolean;
    actionFor(office: unknown): 'connect' | 'disconnect';
    askConnection(office: unknown): void;
    isConfirming(office: unknown): boolean;
    confirmConnection(office: unknown): Promise<void>;
  }

  let request: jasmine.Spy;
  let granted: Set<string>;

  const off = {
    officeId: 'office-1',
    officeName: 'Example Downtown Clinic',
    pushEnabled: false,
    pendingCount: 7,
  };
  const connected = { ...off, pushEnabled: true, feedActive: true, outstandingCount: 0 };

  /** Lets the component's `await firstValueFrom(...)` settle before assertions. */
  function settle(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
  }

  beforeEach(() => {
    request = jasmine.createSpy('request').and.returnValue(of([off]));
    granted = new Set([MANAGE_CONNECTION_POLICY]);
    TestBed.configureTestingModule({
      imports: [CaseTrackerOfficesComponent],
      providers: [
        { provide: RestService, useValue: { request } },
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (p: string) => granted.has(p) },
        },
      ],
    });
  });

  function probe(): Probe {
    // Never change-detected: the template is irrelevant to these assertions (see 'the screen' below).
    return TestBed.createComponent(CaseTrackerOfficesComponent)
      .componentInstance as unknown as Probe;
  }

  async function loaded(rows: unknown[]): Promise<Probe> {
    request.and.returnValue(of(rows));
    const cmp = probe();
    cmp.ngOnInit();
    await settle();
    request.calls.reset();
    return cmp;
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
      expect(cmp.offices()).toEqual([off]);
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

  describe('the action each office offers', () => {
    it('offers Connect to an office that is off', () => {
      expect(probe().actionFor(off)).toBe('connect');
    });

    it('offers Disconnect only when the switch is on AND the feed is on', () => {
      const cmp = probe();
      expect(cmp.isConnected(connected)).toBeTrue();
      expect(cmp.actionFor(connected)).toBe('disconnect');
    });

    it('offers Connect to a switch left on without the feed, which the server then starts', () => {
      const cmp = probe();
      const onWithoutFeed = { ...off, pushEnabled: true, feedActive: false };
      expect(cmp.isConnected(onWithoutFeed)).toBeFalse();
      expect(cmp.actionFor(onWithoutFeed)).toBe('connect');
    });

    it('offers Connect to an office whose feed is on but whose switch is off (the Sandbox state)', () => {
      expect(probe().actionFor({ ...off, feedActive: true })).toBe('connect');
    });

    it('reads the manage permission from the policy the server enforces', () => {
      expect(probe().canManage).toBeTrue();
      granted.clear();
      expect(probe().canManage).toBeFalse();
    });
  });

  describe('connect and disconnect', () => {
    it('asks for a confirm first, only for the office chosen, and sends nothing yet', async () => {
      const cmp = await loaded([off]);
      cmp.askConnection(off);
      expect(cmp.isConfirming(off)).toBeTrue();
      expect(cmp.confirming()?.action).toBe('connect');
      expect(cmp.isConfirming({ ...off, officeId: 'office-2' })).toBeFalse();
      expect(request).not.toHaveBeenCalled();
    });

    it('does nothing when confirmed without having been asked', async () => {
      const cmp = await loaded([off]);
      await cmp.confirmConnection(off);
      expect(request).not.toHaveBeenCalled();
    });

    it('connects with a POST to the literal path', async () => {
      const cmp = await loaded([off]);
      request.and.returnValue(of(connected));
      cmp.askConnection(off);

      await cmp.confirmConnection(off);

      const [config] = request.calls.mostRecent().args as [{ method: string; url: string }];
      expect(config.method).toBe('POST');
      expect(config.url).toBe('/api/app/case-tracker/offices/office-1/connect');
    });

    it('disconnects a connected office with a POST to the literal path', async () => {
      const cmp = await loaded([connected]);
      request.and.returnValue(of({ ...connected, pushEnabled: false }));
      cmp.askConnection(connected);

      await cmp.confirmConnection(connected);

      const [config] = request.calls.mostRecent().args as [{ method: string; url: string }];
      expect(config.url).toBe('/api/app/case-tracker/offices/office-1/disconnect');
    });

    // THE LOAD-BEARING ONE. The row must come from the server, not from local arithmetic.
    it('replaces the row with the server response and clears the confirm and saving flags', async () => {
      const cmp = await loaded([off]);
      const updated = { ...connected, outstandingCount: 7 };
      request.and.returnValue(of(updated));
      cmp.askConnection(off);

      await cmp.confirmConnection(off);

      expect(cmp.offices()).toEqual([updated]);
      expect(cmp.confirming()).toBeNull();
      expect(cmp.saving()).toBeNull();
    });

    it('does not reload the whole list afterwards', async () => {
      const cmp = await loaded([off]);
      request.and.returnValue(of(connected));
      cmp.askConnection(off);

      await cmp.confirmConnection(off);

      const calls = request.calls.allArgs() as Array<[{ method: string }]>;
      expect(calls.filter(([config]) => config.method === 'GET')).toHaveSize(0);
    });

    it('leaves other offices untouched', async () => {
      const other = { ...off, officeId: 'office-2', officeName: 'Example Uptown Clinic' };
      const cmp = await loaded([off, other]);
      request.and.returnValue(of(connected));
      cmp.askConnection(off);

      await cmp.confirmConnection(off);

      expect(cmp.offices()[1]).toEqual(other);
    });

    it('keeps the existing row when the server returns nothing', async () => {
      const cmp = await loaded([off]);
      request.and.returnValue(of(null));
      cmp.askConnection(off);

      await cmp.confirmConnection(off);

      expect(cmp.offices()).toEqual([off]);
    });

    it('marks only the office being saved while the call is in flight', async () => {
      const cmp = await loaded([off]);
      request.and.returnValue(of(connected));
      cmp.askConnection(off);

      const inFlight = cmp.confirmConnection(off);
      expect(cmp.saving()).toBe('office-1');
      await inFlight;
      expect(cmp.saving()).toBeNull();
    });

    it('names the office when connecting fails, and keeps the row as it was', async () => {
      const cmp = await loaded([off]);
      request.and.returnValue(throwError(() => new Error('boom')));
      cmp.askConnection(off);

      await cmp.confirmConnection(off);

      expect(cmp.error()).toBe('Could not connect Example Downtown Clinic. Please try again.');
      expect(cmp.offices()).toEqual([off]);
      expect(cmp.confirming()).toBeNull();
      expect(cmp.saving()).toBeNull();
    });

    it('falls back to the id when disconnecting an unnamed office fails', async () => {
      const unnamed = { ...connected, officeName: '' };
      const cmp = await loaded([unnamed]);
      request.and.returnValue(throwError(() => new Error('boom')));
      cmp.askConnection(unnamed);

      await cmp.confirmConnection(unnamed);

      expect(cmp.error()).toBe('Could not disconnect office-1. Please try again.');
    });
  });

  describe('the screen', () => {
    async function render(rows: unknown[]): Promise<HTMLElement> {
      request.and.returnValue(of(rows));
      const fixture = TestBed.createComponent(CaseTrackerOfficesComponent);
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    function buttons(root: HTMLElement): string[] {
      return Array.from(root.querySelectorAll('tbody button')).map((b) => b.textContent!.trim());
    }

    it('shows one Connect or Disconnect per office, and none of the old actions', async () => {
      const root = await render([off, { ...connected, officeId: 'office-2' }]);

      expect(buttons(root)).toEqual(['Connect', 'Disconnect']);
      const text = root.textContent ?? '';
      expect(text).not.toContain('Start feed');
      expect(text).not.toContain('Return to push');
      expect(text).not.toContain('Turn on');
    });

    it('shows the offices but no buttons to a caller without the manage permission', async () => {
      granted.clear();

      const root = await render([off, connected]);

      expect(root.querySelectorAll('tbody tr').length).toBe(2);
      expect(buttons(root)).toEqual([]);
    });

    it('labels each office Connected, Off, or on without its feed', async () => {
      const root = await render([
        connected,
        { ...off, officeId: 'office-2' },
        { ...off, officeId: 'office-3', pushEnabled: true, feedActive: false },
      ]);

      const cells = Array.from(root.querySelectorAll('tbody tr')).map((tr) =>
        tr.querySelectorAll('td')[1].textContent!.trim(),
      );
      expect(cells).toEqual(['Connected', 'Off', 'On, feed not started']);
    });
  });
});
