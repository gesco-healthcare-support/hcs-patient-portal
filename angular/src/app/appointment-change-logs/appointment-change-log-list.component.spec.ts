import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AppointmentChangeLogService } from '../proxy/appointment-change-logs';
import { AppointmentChangeLogListComponent } from './appointment-change-log-list.component';

/**
 * The global change-log list: filters, paging, and the row -> appointment link.
 *
 * Every assertion about a filter is made on the ARGUMENT handed to getList rather than on
 * what comes back, because the component's job here is precisely to translate a form into a
 * query. A blank control must reach the backend as `undefined` and not as an empty string --
 * the backend WhereIfs on a non-empty value, so an empty string would filter everything out
 * rather than filter nothing.
 *
 * Note the deliberate asymmetry in the proxy that this exercises: the INPUT carries
 * `fieldName` while the returned DTO carries `propertyName`.
 */
describe('AppointmentChangeLogListComponent', () => {
  let getList: jasmine.Spy;
  let navigate: jasmine.Spy;

  const logRow = {
    appointmentId: 'appt-1',
    entityType: 'Appointment',
    propertyName: 'AppointmentDate',
    oldValue: '2026-01-01',
    newValue: '2026-01-02',
    valueRedacted: false,
    changeType: 'Updated',
    changeTime: '2026-01-02T10:00:00',
  };

  beforeEach(() => {
    getList = jasmine.createSpy('getList').and.returnValue(of({ items: [logRow], totalCount: 1 }));
    navigate = jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      imports: [AppointmentChangeLogListComponent],
      providers: [
        { provide: AppointmentChangeLogService, useValue: { getList } },
        { provide: Router, useValue: { navigate } },
      ],
    });
  });

  function make(): AppointmentChangeLogListComponent {
    return TestBed.createComponent(AppointmentChangeLogListComponent).componentInstance;
  }

  function lastInput(): Record<string, unknown> {
    return getList.calls.mostRecent().args[0] as Record<string, unknown>;
  }

  describe('load', () => {
    it('loads on init', () => {
      make().ngOnInit();
      expect(getList).toHaveBeenCalled();
    });

    it('sends undefined rather than empty strings for untouched filters', () => {
      make().load();
      const input = lastInput();
      expect(input['requestConfirmationNumber']).toBeUndefined();
      expect(input['entityType']).toBeUndefined();
      expect(input['fieldName']).toBeUndefined();
      expect(input['changeType']).toBeUndefined();
      expect(input['startTime']).toBeUndefined();
      expect(input['endTime']).toBeUndefined();
    });

    // Positive control for the test above: the same six keys DO travel when set, so the
    // undefineds above are the blank-handling and not the mapping being broken outright.
    it('passes every populated filter through', () => {
      const cmp = make();
      cmp.filterForm.setValue({
        requestConfirmationNumber: 'RCN-1',
        entityType: 'Injury Detail',
        fieldName: 'BodyPart',
        changeType: 'Updated',
        startTime: '2026-01-01',
        endTime: '2026-01-31',
      });
      cmp.load();
      // Asserted field by field rather than through objectContaining, so a failure names the
      // filter that broke instead of printing two whole objects and leaving the reader to
      // diff them.
      const input = lastInput();
      expect(input['requestConfirmationNumber']).toBe('RCN-1');
      expect(input['entityType']).toBe('Injury Detail');
      expect(input['fieldName']).toBe('BodyPart');
      expect(input['changeType']).toBe('Updated');
      expect(input['startTime']).toBe('2026-01-01');
      expect(input['endTime']).toBe('2026-01-31');
    });

    it('asks for the current page by offset', () => {
      const cmp = make();
      cmp.pageIndex = 3;
      cmp.load();
      expect(lastInput()['skipCount']).toBe(75);
      expect(lastInput()['maxResultCount']).toBe(25);
    });

    it('publishes rows and total, and clears loading', () => {
      const cmp = make();
      cmp.load();
      expect(cmp.rows).toEqual([logRow]);
      expect(cmp.totalCount).toBe(1);
      expect(cmp.isLoading).toBeFalse();
    });

    it('treats a result with no items as empty', () => {
      getList.and.returnValue(of({}));
      const cmp = make();
      cmp.load();
      expect(cmp.rows).toEqual([]);
      expect(cmp.totalCount).toBe(0);
    });

    it('empties the table and clears loading when the request fails', () => {
      getList.and.returnValue(throwError(() => new Error('boom')));
      const cmp = make();
      cmp.rows = [logRow];
      cmp.totalCount = 99;
      cmp.load();
      expect(cmp.rows).toEqual([]);
      expect(cmp.totalCount).toBe(0);
      expect(cmp.isLoading).toBeFalse();
    });
  });

  describe('filters', () => {
    it('returns to the first page when filters are applied', () => {
      const cmp = make();
      cmp.pageIndex = 4;
      cmp.applyFilters();
      expect(cmp.pageIndex).toBe(0);
      expect(lastInput()['skipCount']).toBe(0);
    });

    it('blanks every control and returns to the first page when cleared', () => {
      const cmp = make();
      cmp.filterForm.setValue({
        requestConfirmationNumber: 'RCN-1',
        entityType: 'Appointment',
        fieldName: 'Status',
        changeType: 'Deleted',
        startTime: '2026-01-01',
        endTime: '2026-01-31',
      });
      cmp.pageIndex = 2;
      cmp.clearFilters();
      expect(cmp.filterForm.getRawValue()).toEqual({
        requestConfirmationNumber: '',
        entityType: '',
        fieldName: '',
        changeType: '',
        startTime: '',
        endTime: '',
      });
      expect(cmp.pageIndex).toBe(0);
    });

    it('reloads after clearing', () => {
      const cmp = make();
      getList.calls.reset();
      cmp.clearFilters();
      expect(getList).toHaveBeenCalled();
    });
  });

  describe('paging', () => {
    it('reports a single page when there are no rows', () => {
      const cmp = make();
      cmp.totalCount = 0;
      expect(cmp.totalPages).toBe(1);
    });

    it('reports one page for an exactly full page', () => {
      const cmp = make();
      cmp.totalCount = 25;
      expect(cmp.totalPages).toBe(1);
    });

    it('rounds a partial page up', () => {
      const cmp = make();
      cmp.totalCount = 26;
      expect(cmp.totalPages).toBe(2);
    });

    it('will not page back past the first page', () => {
      const cmp = make();
      cmp.pageIndex = 0;
      getList.calls.reset();
      cmp.prevPage();
      expect(cmp.pageIndex).toBe(0);
      expect(getList).not.toHaveBeenCalled();
    });

    it('pages back and reloads when there is a previous page', () => {
      const cmp = make();
      cmp.pageIndex = 2;
      cmp.prevPage();
      expect(cmp.pageIndex).toBe(1);
      expect(lastInput()['skipCount']).toBe(25);
    });

    it('will not page past the last page', () => {
      const cmp = make();
      cmp.totalCount = 30; // two pages
      cmp.pageIndex = 1;
      getList.calls.reset();
      cmp.nextPage();
      expect(cmp.pageIndex).toBe(1);
      expect(getList).not.toHaveBeenCalled();
    });

    it('pages forward and reloads when there is a next page', () => {
      const cmp = make();
      cmp.totalCount = 30;
      cmp.pageIndex = 0;
      cmp.nextPage();
      expect(cmp.pageIndex).toBe(1);
      expect(lastInput()['skipCount']).toBe(25);
    });
  });

  describe('opening an appointment', () => {
    it('navigates to the appointment view', () => {
      make().openAppointment('appt-1');
      expect(navigate).toHaveBeenCalledWith(['/appointments/view', 'appt-1']);
    });

    it('does nothing without an id', () => {
      make().openAppointment(null);
      expect(navigate).not.toHaveBeenCalled();
    });

    it('does nothing for an empty id', () => {
      make().openAppointment('');
      expect(navigate).not.toHaveBeenCalled();
    });
  });
});
