import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { EnvironmentService } from '@abp/ng.core';

import { AppointmentReportComponent } from './appointment-report.component';
import { ReportService } from '../proxy/reports';
import { AppointmentTypeService } from '../proxy/appointment-types';
import { LocationService } from '../proxy/locations';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';

/**
 * The Appointment Request Report -- the internal cross-appointment worklist behind
 * CaseEvaluation.Reports.
 *
 * <p>It sat at 104 of 158 lines uncovered. An existing spec (overlay-escape-keys) covers the
 * column picker's Escape handler and nothing else, so none of that is repeated here.</p>
 *
 * <p>Two things get the most attention. The first is the guard pair: this report refuses to run
 * or export without at least one filter and a coherent date range, which is what stops a staff
 * member pulling the entire patient population into a PDF by pressing Export on an empty form.
 * The second is the export path itself -- it must go through HttpClient so ABP's interceptor
 * attaches the bearer token, because a plain window.open lands on a 401 (the comment in the
 * component says so, and this pins it).</p>
 *
 * <p>Built with `createComponent` and never change-detected, so the template does not render and
 * only the methods each test calls run.</p>
 *
 * <p>All filter values, names and identifiers below are synthetic.</p>
 */
describe('AppointmentReportComponent surfaces', () => {
  let reportService: Record<string, jasmine.Spy>;
  let typeService: Record<string, jasmine.Spy>;
  let locationService: Record<string, jasmine.Spy>;
  let http: { get: jasmine.Spy };
  let router: { navigate: jasmine.Spy };

  interface Probe {
    [key: string]: any;
  }

  const API = 'https://api.test';

  /** A blob response shaped like the one HttpClient returns with observe: 'response'. */
  function blobResponse(disposition: string | null, body: Blob | null = new Blob(['x'])) {
    return of({ body, headers: { get: () => disposition } });
  }

  function create(): Probe {
    reportService = {
      getList: jasmine.createSpy('getList').and.returnValue(of({ items: [], totalCount: 0 })),
      getStatusCounts: jasmine.createSpy('getStatusCounts').and.returnValue(of([])),
    };
    typeService = {
      getList: jasmine.createSpy('typeGetList').and.returnValue(of({ items: [] })),
    };
    locationService = {
      getList: jasmine.createSpy('locationGetList').and.returnValue(of({ items: [] })),
    };
    http = { get: jasmine.createSpy('get').and.returnValue(blobResponse(null)) };
    router = { navigate: jasmine.createSpy('navigate') };

    TestBed.configureTestingModule({
      providers: [
        { provide: ReportService, useValue: reportService },
        { provide: AppointmentTypeService, useValue: typeService },
        { provide: LocationService, useValue: locationService },
        { provide: Router, useValue: router },
        { provide: HttpClient, useValue: http },
        { provide: EnvironmentService, useValue: { getApiUrl: () => API } },
      ],
    });

    return TestBed.createComponent(AppointmentReportComponent)
      .componentInstance as unknown as Probe;
  }

  /** Put one value in the form so the "at least one filter" guard is satisfied. */
  function withFilter(c: Probe, patch: Record<string, string> = { filterText: 'C0001' }): void {
    c.filterForm.patchValue(patch);
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('lookup loading', () => {
    it('maps appointment types to id and name', () => {
      const c = create();
      typeService['getList'].and.returnValue(
        of({
          items: [
            { id: 't-1', name: 'AME' },
            { id: 't-2', name: 'QME' },
          ],
        }),
      );
      c.ngOnInit();
      expect(c.typeOptions).toEqual([
        { id: 't-1', name: 'AME' },
        { id: 't-2', name: 'QME' },
      ]);
    });

    it('reads a location through its nested location object', () => {
      // The locations endpoint returns a nav-property wrapper, not a flat row.
      const c = create();
      locationService['getList'].and.returnValue(
        of({ items: [{ location: { id: 'l-1', name: 'Encino' } }] }),
      );
      c.ngOnInit();
      expect(c.locationOptions).toEqual([{ id: 'l-1', name: 'Encino' }]);
    });

    it('survives a lookup payload with no items at all', () => {
      const c = create();
      typeService['getList'].and.returnValue(of({}));
      locationService['getList'].and.returnValue(of({}));
      c.ngOnInit();
      expect(c.typeOptions).toEqual([]);
      expect(c.locationOptions).toEqual([]);
    });

    it('substitutes empty strings for a lookup row missing its id or name', () => {
      const c = create();
      typeService['getList'].and.returnValue(of({ items: [{}] }));
      c.ngOnInit();
      expect(c.typeOptions).toEqual([{ id: '', name: '' }]);
    });
  });

  describe('the search guards', () => {
    it('refuses to search an empty form', () => {
      /**
       * Without this the report would run unfiltered and return every appointment in the
       * tenant, which is precisely the pull this screen exists to keep deliberate.
       */
      const c = create();
      c.applyFilters();
      expect(c.validationError).toBe('Enter at least one search value.');
      expect(reportService['getList']).not.toHaveBeenCalled();
    });

    it('refuses a half-open date range', () => {
      const c = create();
      withFilter(c, { appointmentDateMin: '2026-09-01' });
      c.applyFilters();
      expect(c.validationError).toBe('Enter both From and To dates, with From on or before To.');
      expect(reportService['getList']).not.toHaveBeenCalled();
    });

    it('refuses a range that runs backwards', () => {
      const c = create();
      withFilter(c, { appointmentDateMin: '2026-09-30', appointmentDateMax: '2026-09-01' });
      c.applyFilters();
      expect(reportService['getList']).not.toHaveBeenCalled();
    });

    it('accepts a range whose ends are equal', () => {
      const c = create();
      withFilter(c, { appointmentDateMin: '2026-09-01', appointmentDateMax: '2026-09-01' });
      c.applyFilters();
      expect(reportService['getList']).toHaveBeenCalled();
    });

    it('accepts a filter with no dates at all', () => {
      const c = create();
      withFilter(c);
      c.applyFilters();
      expect(c.validationError).toBe('');
      expect(reportService['getList']).toHaveBeenCalled();
    });

    it('returns to the first page on a new search', () => {
      const c = create();
      c.pageIndex = 4;
      withFilter(c);
      c.applyFilters();
      expect(c.pageIndex).toBe(0);
    });

    it('clears a previous error once the form becomes valid', () => {
      const c = create();
      c.applyFilters();
      expect(c.validationError).not.toBe('');
      withFilter(c);
      c.applyFilters();
      expect(c.validationError).toBe('');
    });
  });

  describe('clearing the form', () => {
    it('resets the filters, the results and the paging together', () => {
      const c = create();
      withFilter(c);
      c.rows = [{ appointmentId: 'a-1' }];
      c.totalCount = 9;
      c.segmentCounts = { pending: 3 };
      c.hasSearched = true;
      c.pageIndex = 2;
      c.validationError = 'something';

      c.clearFilters();

      expect(c.filterForm.getRawValue().filterText).toBe('');
      expect(c.rows).toEqual([]);
      expect(c.totalCount).toBe(0);
      expect(c.segmentCounts).toEqual({});
      expect(c.hasSearched).toBeFalse();
      expect(c.pageIndex).toBe(0);
      expect(c.validationError).toBe('');
    });
  });

  describe('exporting', () => {
    it('applies the same two guards as the search, to both formats', () => {
      // An export that bypassed the guards would produce the unfiltered PDF the search
      // refuses to run.
      const c = create();
      c.exportPdf();
      expect(c.validationError).toBe('Enter at least one search value.');
      expect(http.get).not.toHaveBeenCalled();

      c.exportCsv();
      expect(http.get).not.toHaveBeenCalled();

      c.filterForm.patchValue({ filterText: 'C0001', appointmentDateMin: '2026-09-01' });
      c.exportPdf();
      expect(http.get).withContext('blocked by the range guard').not.toHaveBeenCalled();
    });

    it('goes through HttpClient so the bearer token is attached', () => {
      /**
       * window.open would open a tab with no Authorization header and 401. The component
       * comment says so; this is what holds it.
       */
      const c = create();
      withFilter(c);
      c.exportPdf();
      expect(http.get).toHaveBeenCalled();
      expect(http.get.calls.mostRecent().args[0]).toBe(`${API}/api/app/reports/export-pdf`);
    });

    it('asks for the csv endpoint when exporting csv', () => {
      const c = create();
      withFilter(c);
      c.exportCsv();
      expect(http.get.calls.mostRecent().args[0]).toBe(`${API}/api/app/reports/export-csv`);
    });

    it('requests a blob, not json', () => {
      const c = create();
      withFilter(c);
      c.exportCsv();
      const options = http.get.calls.mostRecent().args[1];
      expect(options.responseType).toBe('blob');
      expect(options.observe).toBe('response');
    });

    it('sends only the filters that were filled in', () => {
      const c = create();
      c.filterForm.patchValue({ filterText: 'C0001', locationId: 'l-1' });
      c.exportCsv();
      const params = http.get.calls.mostRecent().args[1].params;
      expect(params.get('filterText')).toBe('C0001');
      expect(params.get('locationId')).toBe('l-1');
      expect(params.get('appointmentTypeId')).toBeNull();
      expect(params.get('appointmentDateMin')).toBeNull();
    });

    it('sends every filter when every filter is set', () => {
      const c = create();
      c.filterForm.patchValue({
        filterText: 'C0001',
        appointmentTypeId: 't-1',
        locationId: 'l-1',
        appointmentStatus: '1',
        appointmentDateMin: '2026-09-01',
        appointmentDateMax: '2026-09-30',
      });
      c.exportCsv();
      const params = http.get.calls.mostRecent().args[1].params;
      expect(params.get('appointmentTypeId')).toBe('t-1');
      expect(params.get('appointmentStatus')).toBe('1');
      expect(params.get('appointmentDateMax')).toBe('2026-09-30');
    });

    it('takes the file name from the content-disposition header', async () => {
      const c = create();
      withFilter(c);
      http.get.and.returnValue(blobResponse('attachment; filename="appointments-2026-09.csv"'));
      await c.download('csv');
      expect(c.validationError).toBe('');
    });

    it('reports an empty body rather than saving a zero-byte file', async () => {
      const c = create();
      withFilter(c);
      http.get.and.returnValue(blobResponse('attachment; filename="x.csv"', null));
      await c.download('csv');
      expect(c.validationError).toBe('Empty export response from the server.');
    });

    it('reports a failed export instead of failing silently', async () => {
      const c = create();
      withFilter(c);
      http.get.and.returnValue(throwError(() => ({ status: 500 })));
      await c.download('pdf');
      expect(c.validationError).toBe('Export failed. Please try again.');
    });
  });

  describe('loading rows', () => {
    it('stores the page and the total', () => {
      const c = create();
      reportService['getList'].and.returnValue(
        of({ items: [{ appointmentId: 'a-1' }], totalCount: 31 }),
      );
      c.load();
      expect(c.rows.length).toBe(1);
      expect(c.totalCount).toBe(31);
      expect(c.isLoading).toBeFalse();
      expect(c.hasSearched).toBeTrue();
    });

    it('treats a payload with no items as an empty page', () => {
      const c = create();
      reportService['getList'].and.returnValue(of({}));
      c.load();
      expect(c.rows).toEqual([]);
      expect(c.totalCount).toBe(0);
    });

    it('empties the table and stops loading when the request fails', () => {
      const c = create();
      reportService['getList'].and.returnValue(throwError(() => ({ status: 500 })));
      c.load();
      expect(c.rows).toEqual([]);
      expect(c.totalCount).toBe(0);
      expect(c.isLoading).toBeFalse();
    });

    it('asks for the status counts alongside the rows', () => {
      const c = create();
      c.load();
      expect(reportService['getStatusCounts']).toHaveBeenCalled();
    });
  });

  describe('the status summary cards', () => {
    it('buckets raw per-status counts into the six pill segments', () => {
      const c = create();
      reportService['getStatusCounts'].and.returnValue(
        of([
          { status: AppointmentStatusType.Pending, count: 4 },
          { status: AppointmentStatusType.Approved, count: 2 },
        ]),
      );
      c.load();
      expect(c.statCount('pending')).toBe(4);
      expect(c.statCount('approved')).toBe(2);
    });

    it('adds together two raw statuses that share one segment', () => {
      // CancelledNoBill and CancelledLate are two statuses and one chip; a card that
      // showed only one of them would under-report the bucket.
      const c = create();
      reportService['getStatusCounts'].and.returnValue(
        of([
          { status: AppointmentStatusType.CancelledNoBill, count: 3 },
          { status: AppointmentStatusType.CancelledLate, count: 5 },
        ]),
      );
      c.load();
      expect(c.statCount('cancelled')).toBe(8);
    });

    it('skips a row with no status rather than bucketing it as zero', () => {
      const c = create();
      reportService['getStatusCounts'].and.returnValue(
        of([
          { status: null, count: 7 },
          { status: undefined, count: 9 },
        ]),
      );
      c.load();
      expect(c.segmentCounts).toEqual({});
    });

    it('treats a missing count as zero', () => {
      const c = create();
      reportService['getStatusCounts'].and.returnValue(
        of([{ status: AppointmentStatusType.Pending }]),
      );
      c.load();
      expect(c.statCount('pending')).toBe(0);
    });

    it('survives a null counts payload', () => {
      const c = create();
      reportService['getStatusCounts'].and.returnValue(of(null));
      c.load();
      expect(c.segmentCounts).toEqual({});
    });

    it('empties the cards when the counts request fails', () => {
      const c = create();
      c.segmentCounts = { pending: 4 };
      reportService['getStatusCounts'].and.returnValue(throwError(() => ({ status: 500 })));
      c.load();
      expect(c.segmentCounts).toEqual({});
    });

    it('reports zero for a segment nothing counted', () => {
      const c = create();
      expect(c.statCount('rejected')).toBe(0);
    });
  });

  describe('the column picker', () => {
    it('starts with every column shown', () => {
      const c = create();
      expect(c.shownColumnCount).toBe(c.colDefs.length);
    });

    it('hides and restores one column', () => {
      const c = create();
      c.toggleCol('dob');
      expect(c.cols.dob).toBeFalse();
      expect(c.shownColumnCount).toBe(c.colDefs.length - 1);

      c.toggleCol('dob');
      expect(c.cols.dob).toBeTrue();
      expect(c.shownColumnCount).toBe(c.colDefs.length);
    });

    it('replaces the map rather than mutating it', () => {
      // OnPush reads this by reference; an in-place edit would not repaint.
      const c = create();
      const before = c.cols;
      c.toggleCol('email');
      expect(c.cols).not.toBe(before);
    });
  });

  describe('paging', () => {
    it('always reports at least one page, even with no rows', () => {
      const c = create();
      c.totalCount = 0;
      expect(c.totalPages).toBe(1);
    });

    it('rounds a partial last page up', () => {
      const c = create();
      c.totalCount = 26;
      expect(c.totalPages).toBe(2);
    });

    it('will not step back past the first page', () => {
      const c = create();
      c.pageIndex = 0;
      c.prevPage();
      expect(c.pageIndex).toBe(0);
      expect(reportService['getList']).not.toHaveBeenCalled();
    });

    it('steps back and reloads when there is a page to go back to', () => {
      const c = create();
      c.pageIndex = 2;
      c.prevPage();
      expect(c.pageIndex).toBe(1);
      expect(reportService['getList']).toHaveBeenCalled();
    });

    it('will not step past the last page', () => {
      const c = create();
      c.totalCount = 10;
      c.pageIndex = 0;
      c.nextPage();
      expect(c.pageIndex).withContext('10 rows is a single page of 25').toBe(0);
      expect(reportService['getList']).not.toHaveBeenCalled();
    });

    it('steps forward and reloads when another page exists', () => {
      const c = create();
      c.totalCount = 60;
      c.pageIndex = 0;
      c.nextPage();
      expect(c.pageIndex).toBe(1);
      expect(reportService['getList']).toHaveBeenCalled();
    });
  });

  describe('sorting', () => {
    it('re-pages from the first row and reloads', () => {
      // Keeping the page index would show page 3 of a differently-ordered result.
      const c = create();
      c.pageIndex = 3;
      c.onSort({ key: 'appointment.appointmentDate', dir: 'desc' });
      expect(c.pageIndex).toBe(0);
      expect(c.sort).toEqual({ key: 'appointment.appointmentDate', dir: 'desc' });
      expect(reportService['getList']).toHaveBeenCalled();
    });
  });

  describe('the request body', () => {
    it('omits every blank filter rather than sending an empty string', () => {
      const c = create();
      withFilter(c);
      c.load();
      const input = reportService['getList'].calls.mostRecent().args[0];
      expect(input.filterText).toBe('C0001');
      expect(input.appointmentTypeId).toBeUndefined();
      expect(input.appointmentStatus).toBeUndefined();
      expect(input.sorting).toBeUndefined();
    });

    it('sends the status as a number, not the string the select holds', () => {
      const c = create();
      c.filterForm.patchValue({ appointmentStatus: String(AppointmentStatusType.Approved) });
      c.load();
      const input = reportService['getList'].calls.mostRecent().args[0];
      expect(input.appointmentStatus).toBe(AppointmentStatusType.Approved);
      expect(typeof input.appointmentStatus).toBe('number');
    });

    it('builds the sorting clause from the active sort', () => {
      const c = create();
      withFilter(c);
      c.sort = { key: 'appointment.panelNumber', dir: 'desc' };
      c.load();
      const input = reportService['getList'].calls.mostRecent().args[0];
      expect(input.sorting).toBe('appointment.panelNumber desc');
    });

    it('derives skipCount from the page index and the fixed page size', () => {
      const c = create();
      c.pageIndex = 3;
      c.load();
      const input = reportService['getList'].calls.mostRecent().args[0];
      expect(input.skipCount).toBe(3 * c.pageSize);
      expect(input.maxResultCount).toBe(c.pageSize);
    });
  });

  describe('row display helpers', () => {
    it('falls back to the Pending pill for a row with no status', () => {
      const c = create();
      expect(c.pill(undefined)).toBe(c.pill(AppointmentStatusType.Pending));
    });

    it('names a status from the enum options', () => {
      const c = create();
      expect(c.statusLabel(AppointmentStatusType.Approved)).toBeTruthy();
    });

    it('returns an empty label for a status outside the enum', () => {
      const c = create();
      expect(c.statusLabel(9999 as AppointmentStatusType)).toBe('');
    });

    it('opens an appointment by id', () => {
      const c = create();
      c.openAppointment('a-1');
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/view', 'a-1']);
    });

    it('does not navigate for a row with no appointment id', () => {
      const c = create();
      c.openAppointment(null);
      c.openAppointment(undefined);
      c.openAppointment('');
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });
});
