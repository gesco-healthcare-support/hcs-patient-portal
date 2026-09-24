import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { of, throwError } from 'rxjs';
import { describeRetryAll, IntegrationFailuresComponent } from './integration-failures.component';

/**
 * The dead-letter queue for the Case Tracker push: what failed to sync, and the retry.
 *
 * THE DIFFERENCE FROM ITS NEAR-TWIN IS THE POINT. case-tracker-offices REPLACES a row from the
 * server response after a write; this one REMOVES the row locally, because the server has marked
 * the letter resolved and dropping it in place keeps the operator's position in a long list
 * (:243). Two components with the same DI, the same signals and the same load(), doing deliberately
 * opposite things on success -- so these specs assert opposite things, and the mutation pass
 * carries a probe for each direction.
 */
describe('IntegrationFailuresComponent', () => {
  interface Signal<T> {
    (): T;
    set(value: T): void;
  }
  interface Probe {
    ngOnInit(): void;
    rows: Signal<unknown[]>;
    loading: Signal<boolean>;
    error: Signal<string | null>;
    retrying: Signal<string | null>;
    officeFilter: Signal<string>;
    confirmingRetryAll: Signal<boolean>;
    retryingAll: Signal<boolean>;
    notice: Signal<string | null>;
    offices: () => Array<{ id: string; name: string }>;
    visibleRows: () => unknown[];
    load(): Promise<void>;
    retry(row: unknown): Promise<void>;
    selectOffice(officeId: string): void;
    retryAll(): Promise<void>;
  }

  let request: jasmine.Spy;

  const failure = {
    id: 'dl-1',
    officeId: 'office-1',
    officeName: 'Example Downtown Clinic',
    appointmentId: 'appt-1',
    confirmationNumber: 'RCN-1001',
    messageType: 'AppointmentCreated',
    targetPath: '/api/sync/appointments',
    attemptCount: 3,
    lastError: 'timeout',
    failedAt: '2026-01-02T10:00:00',
  };

  /** Lets the component's `await firstValueFrom(...)` settle before assertions. */
  function settle(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
  }

  beforeEach(() => {
    request = jasmine.createSpy('request').and.returnValue(of([failure]));
    TestBed.configureTestingModule({
      imports: [IntegrationFailuresComponent],
      providers: [{ provide: RestService, useValue: { request } }],
    });
  });

  function probe(): Probe {
    return TestBed.createComponent(IntegrationFailuresComponent)
      .componentInstance as unknown as Probe;
  }

  describe('load', () => {
    it('asks the dead-letter endpoint', async () => {
      probe().ngOnInit();
      await settle();
      const [config] = request.calls.mostRecent().args as [{ method: string; url: string }];
      expect(config.method).toBe('GET');
      expect(config.url).toBe('/api/app/case-tracker/dead-letters');
    });

    it('publishes the returned rows and clears the loading flag', async () => {
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      expect(cmp.rows()).toEqual([failure]);
      expect(cmp.loading()).toBeFalse();
    });

    it('treats a null response as an empty list', async () => {
      request.and.returnValue(of(null));
      const cmp = probe();
      await cmp.load();
      expect(cmp.rows()).toEqual([]);
    });

    it('explains a failed load and still clears the loading flag', async () => {
      request.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      await cmp.load();
      expect(cmp.error()).toBe('The failure list could not be loaded. Please try again.');
      expect(cmp.loading()).toBeFalse();
    });
  });

  describe('retry', () => {
    it('posts to the per-office dead-letter retry path', async () => {
      const cmp = probe();
      await cmp.retry(failure);
      const [config] = request.calls.mostRecent().args as [{ method: string; url: string }];
      expect(config.method).toBe('POST');
      expect(config.url).toBe('/api/app/case-tracker/offices/office-1/dead-letters/dl-1/retry');
    });

    // THE LOAD-BEARING ONE, and the opposite of the twin's behaviour.
    it('removes the retried row locally', async () => {
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      request.and.returnValue(of(undefined));
      await cmp.retry(failure);
      expect(cmp.rows()).toEqual([]);
    });

    it('does not reload the list after a retry', async () => {
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      request.calls.reset();
      request.and.returnValue(of(undefined));
      await cmp.retry(failure);
      const calls = request.calls.allArgs() as Array<[{ method: string }]>;
      expect(calls.filter(([config]) => config.method === 'GET')).toHaveSize(0);
    });

    it('removes only the retried row', async () => {
      const other = { ...failure, id: 'dl-2', confirmationNumber: 'RCN-1002' };
      request.and.returnValue(of([failure, other]));
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      request.and.returnValue(of(undefined));
      await cmp.retry(failure);
      expect(cmp.rows()).toEqual([other]);
    });

    it('marks only the row being retried, and clears it afterwards', async () => {
      const cmp = probe();
      const inFlight = cmp.retry(failure);
      expect(cmp.retrying()).toBe('dl-1');
      await inFlight;
      expect(cmp.retrying()).toBeNull();
    });

    it('keeps the row and names the confirmation number when the retry fails', async () => {
      const cmp = probe();
      cmp.ngOnInit();
      await settle();
      request.and.returnValue(throwError(() => new Error('boom')));
      await cmp.retry(failure);
      expect(cmp.rows()).toEqual([failure]);
      expect(cmp.error()).toBe('Retry failed for RCN-1001. It may already have been retried.');
    });

    it('falls back to the appointment id when there is no confirmation number', async () => {
      request.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      await cmp.retry({ ...failure, confirmationNumber: '' });
      expect(cmp.error()).toBe('Retry failed for appt-1. It may already have been retried.');
    });

    it('releases the retrying flag when the retry fails', async () => {
      request.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      await cmp.retry(failure);
      expect(cmp.retrying()).toBeNull();
    });
  });

  // #917: office filter + "Retry all for this office".
  describe('office filter', () => {
    const otherOffice = {
      ...failure,
      id: 'dl-9',
      officeId: 'office-2',
      officeName: 'Another Office',
    };

    it('offers each office that has failures, once, by name', async () => {
      request.and.returnValue(of([failure, { ...failure, id: 'dl-2' }, otherOffice]));
      const cmp = probe();
      await cmp.load();
      expect(cmp.offices()).toEqual([
        { id: 'office-2', name: 'Another Office' },
        { id: 'office-1', name: 'Example Downtown Clinic' },
      ]);
    });

    it('narrows the table to the selected office', async () => {
      request.and.returnValue(of([failure, otherOffice]));
      const cmp = probe();
      await cmp.load();
      cmp.selectOffice('office-2');
      expect(cmp.visibleRows()).toEqual([otherOffice]);
    });

    it('falls back to all offices when the selected office has nothing left', async () => {
      request.and.returnValue(of([failure, otherOffice]));
      const cmp = probe();
      await cmp.load();
      cmp.selectOffice('office-2');
      request.and.returnValue(of(undefined));
      await cmp.retry(otherOffice);
      expect(cmp.officeFilter()).toBe('');
      expect(cmp.visibleRows()).toEqual([failure]);
    });
  });

  describe('retryAll', () => {
    const result = { requeued: 2, alreadyDelivered: 1, notRetried: 0, remaining: 0 };

    it('posts to the per-office retry-all path, reports the counts, and reloads', async () => {
      const cmp = probe();
      await cmp.load();
      cmp.selectOffice('office-1');
      cmp.confirmingRetryAll.set(true);
      request.calls.reset();
      request.and.returnValues(of(result), of([]));

      await cmp.retryAll();

      const calls = request.calls.allArgs() as Array<[{ method: string; url: string }]>;
      expect(calls[0][0].method).toBe('POST');
      expect(calls[0][0].url).toBe('/api/app/case-tracker/offices/office-1/dead-letters/retry-all');
      expect(calls[1][0].method).toBe('GET');
      expect(cmp.notice()).toBe('2 queued to send again. 1 already delivered.');
      expect(cmp.confirmingRetryAll()).toBeFalse();
      expect(cmp.retryingAll()).toBeFalse();
    });

    it('does nothing without a selected office', async () => {
      const cmp = probe();
      request.calls.reset();
      await cmp.retryAll();
      expect(request).not.toHaveBeenCalled();
    });

    it('explains a failed retry-all and still reloads the list', async () => {
      const cmp = probe();
      await cmp.load();
      cmp.selectOffice('office-1');
      request.calls.reset();
      request.and.returnValues(
        throwError(() => new Error('boom')),
        of([failure]),
      );

      await cmp.retryAll();

      expect(cmp.notice()).toBe(
        'Retry all did not finish. The list below shows what is still outstanding.',
      );
      expect(cmp.error()).toBeNull(); // a notice, so the reloaded table still shows
      expect(request.calls.count()).toBe(2);
      expect(cmp.retryingAll()).toBeFalse();
    });
  });

  describe('describeRetryAll', () => {
    it('names only the parts that happened', () => {
      expect(
        describeRetryAll({ requeued: 3, alreadyDelivered: 0, notRetried: 0, remaining: 0 }),
      ).toBe('3 queued to send again.');
    });

    it('says what is still listed and what is left for another press', () => {
      expect(
        describeRetryAll({ requeued: 1, alreadyDelivered: 0, notRetried: 2, remaining: 5 }),
      ).toBe(
        '1 queued to send again. 2 could not be retried and are still listed. ' +
          '5 not yet retried; select Retry all again.',
      );
    });

    it('treats a missing response as nothing done', () => {
      expect(describeRetryAll(null)).toBe('0 queued to send again.');
    });
  });
});
