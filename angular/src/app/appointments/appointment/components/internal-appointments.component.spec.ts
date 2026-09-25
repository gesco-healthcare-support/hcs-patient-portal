import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { LocalizationService, PermissionService, RestService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { InternalAppointmentsComponent } from './internal-appointments.component';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { ChangeRequestType } from '../../../proxy/appointment-change-requests/change-request-type.enum';

/**
 * The internal appointments list -- the staff landing surface at `/appointments`.
 *
 * <p>It sat at 46 of 223 covered lines. What is pinned here is the query pipeline rather than
 * the presentation: how search, chips, filters, sorting and paging combine into one server
 * request, and what each of them resets. Those interactions are where a list page goes wrong
 * quietly -- a filter that does not reset the page number shows an empty page 7 of a 2-page
 * result, and nothing throws.</p>
 *
 * <p>Two behaviours get particular attention because they were bugs. A failed list request is
 * NOT an empty result: it clears rows, raises an error state and toasts, so the table never
 * reads "0 appointments" beside populated status chips. And the counts endpoint deliberately
 * ignores the status filter, so each chip shows its true total within the OTHER filters.</p>
 *
 * <p>Built with `createComponent` and never change-detected. Permissions are set BEFORE
 * construction because `canDelete` and `canCreate` are field initialisers -- they read the
 * policy once, at construction, not on access.</p>
 *
 * <p>All patients, panel numbers and locations below are synthetic.</p>
 */
describe('InternalAppointmentsComponent', () => {
  let service: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let confirmationWarn: jasmine.Spy;
  let router: { navigate: jasmine.Spy; navigateByUrl: jasmine.Spy };
  let queryParams: Subject<{ get(key: string): string | null }>;
  let granted: Set<string>;

  interface Probe {
    [key: string]: any;
  }

  function row(over: Record<string, unknown> = {}) {
    return {
      appointment: {
        id: 'appt-1',
        requestConfirmationNumber: 'C0001',
        appointmentStatus: AppointmentStatusType.Pending,
        appointmentDate: '2026-10-01T09:00:00',
        panelNumber: 'PN-0001',
        creationTime: '2026-09-01T09:00:00',
        ...over,
      },
      patient: { firstName: 'Ada', lastName: 'Lovelace' },
      appointmentType: { name: 'AME' },
      location: { name: 'Encino' },
    };
  }

  function create(options: { policies?: string[] } = {}): Probe {
    queryParams = new Subject();
    granted = new Set(options.policies ?? []);

    service = {
      getList: jasmine.createSpy('getList').and.returnValue(of({ items: [], totalCount: 0 })),
      getStatusCounts: jasmine.createSpy('getStatusCounts').and.returnValue(of([])),
      getAppointmentTypeLookup: jasmine
        .createSpy('getAppointmentTypeLookup')
        .and.returnValue(of({ items: [] })),
      getLocationLookup: jasmine.createSpy('getLocationLookup').and.returnValue(of({ items: [] })),
      getIdentityUserLookup: jasmine
        .createSpy('getIdentityUserLookup')
        .and.returnValue(of({ items: [] })),
      delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
    };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    confirmationWarn = jasmine.createSpy('warn').and.returnValue(of(Confirmation.Status.confirm));
    router = {
      navigate: jasmine.createSpy('navigate'),
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    };

    TestBed.configureTestingModule({
      imports: [InternalAppointmentsComponent],
      providers: [
        { provide: AppointmentService, useValue: service },
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (p: string) => granted.has(p) },
        },
        { provide: ConfirmationService, useValue: { warn: confirmationWarn } },
        { provide: ToasterService, useValue: toaster },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { queryParamMap: queryParams } },
        // The component's standalone `imports` pull in the reschedule and cancellation modals,
        // whose own DI chain is AppointmentChangeRequestService -> RestService -> CORE_OPTIONS.
        // Without these two, createComponent fails with NG0201 before a single test runs --
        // which is what 78 FAILED / 0 SUCCESS looked like.
        {
          provide: AppointmentChangeRequestService,
          useValue: { getActiveForAppointment: () => of(null) },
        },
        { provide: RestService, useValue: { request: () => of(null) } },
      ],
    });

    return TestBed.createComponent(InternalAppointmentsComponent)
      .componentInstance as unknown as Probe;
  }

  /** Run ngOnInit and deliver one query-param emission, which is what triggers the first load. */
  function started(c: Probe, status: string | null = null): void {
    c.ngOnInit();
    queryParams.next({ get: () => status });
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('initial load', () => {
    it('loads the lookups, the list and the counts', () => {
      const c = create();
      started(c);
      expect(service['getAppointmentTypeLookup']).toHaveBeenCalled();
      expect(service['getLocationLookup']).toHaveBeenCalled();
      expect(service['getList']).toHaveBeenCalled();
      expect(service['getStatusCounts']).toHaveBeenCalled();
    });

    it('stores the rows and total', () => {
      const c = create();
      service['getList'].and.returnValue(of({ items: [row()], totalCount: 42 }));
      started(c);
      expect(c.rows().length).toBe(1);
      expect(c.totalCount()).toBe(42);
      expect(c.loading()).toBeFalse();
    });

    it('treats a page with no items as an empty result, not an error', () => {
      const c = create();
      service['getList'].and.returnValue(of({}));
      started(c);
      expect(c.rows()).toEqual([]);
      expect(c.totalCount()).toBe(0);
      expect(c.loadError()).toBeFalse();
    });

    it('distinguishes a FAILED request from an empty one', () => {
      /**
       * The bug this exists for: a failed load used to leave the table showing "0 of 0"
       * beside populated status chips, which reads as "there are no appointments" rather
       * than "we could not fetch them". The fixture seeds rows first so the clear is
       * observable -- against an already-empty table it would prove nothing.
       */
      const c = create();
      service['getList'].and.returnValue(of({ items: [row()], totalCount: 1 }));
      started(c);
      expect(c.rows().length).toBe(1);

      service['getList'].and.returnValue(throwError(() => ({ status: 500 })));
      c.retry();

      expect(c.rows()).toEqual([]);
      expect(c.totalCount()).toBe(0);
      expect(c.loadError()).toBeTrue();
      expect(c.loading()).toBeFalse();
      expect(toaster.error).toHaveBeenCalled();
    });

    it('clears the error state on a successful retry', () => {
      const c = create();
      service['getList'].and.returnValue(throwError(() => ({ status: 500 })));
      started(c);
      expect(c.loadError()).toBeTrue();

      service['getList'].and.returnValue(of({ items: [row()], totalCount: 1 }));
      c.retry();

      expect(c.loadError()).toBeFalse();
      expect(c.rows().length).toBe(1);
    });

    it('keeps a counts failure quiet so one outage does not double-toast', () => {
      // loadList owns the single user-facing error toast; the chips just fall back to zero.
      const c = create();
      service['getStatusCounts'].and.returnValue(throwError(() => ({ status: 500 })));
      started(c);
      expect(toaster.error).not.toHaveBeenCalled();
      expect(c.chipCount('all')).toBe(0);
    });
  });

  describe('status chips', () => {
    it('defaults to the all segment with no query parameter', () => {
      const c = create();
      started(c, null);
      expect(c.activeSegment()).toBe('all');
    });

    it('adopts the segment from a deep-linked status', () => {
      // Dashboard links arrive as /appointments?appointmentStatus=N with no chip click.
      const c = create();
      started(c, String(AppointmentStatusType.Pending));
      expect(c.activeSegment()).not.toBe('all');
    });

    it('ignores a non-numeric status parameter', () => {
      const c = create();
      started(c, 'not-a-number');
      expect(c.activeSegment()).toBe('all');
    });

    it('treats an empty status parameter as all', () => {
      const c = create();
      started(c, '');
      expect(c.activeSegment()).toBe('all');
    });

    it('writes the chip choice to the URL rather than setting state directly', () => {
      /**
       * The query parameter is the SINGLE driver for the active chip: setSegment writes it
       * and the subscription reacts. Setting the signal here as well would give the chip two
       * sources of truth and let a back-button URL disagree with the highlighted chip.
       */
      const c = create();
      started(c);
      c.setSegment('all');
      expect(router.navigate).toHaveBeenCalled();
      const extras = router.navigate.calls.mostRecent().args[1];
      expect(extras.queryParams.appointmentStatus).toBeNull();
      expect(extras.queryParamsHandling).toBe('merge');
      expect(extras.replaceUrl).toBeTrue();
    });

    it('resets to the first page when the segment changes', () => {
      const c = create();
      started(c);
      c.page.set(5);
      queryParams.next({ get: () => String(AppointmentStatusType.Pending) });
      expect(c.page()).toBe(1);
    });

    it('does not reset the page when the segment is unchanged', () => {
      // A re-emission of the same parameter must not yank the operator back to page 1.
      const c = create();
      started(c, null);
      c.page.set(3);
      queryParams.next({ get: () => null });
      expect(c.page()).toBe(3);
    });

    it('reports zero for a segment with no counted rows', () => {
      const c = create();
      started(c);
      expect(c.chipCount('all')).toBe(0);
    });

    it('never sends the status filter to the counts endpoint', () => {
      /**
       * The counts endpoint ignores the status filter by design, so every chip shows its true
       * total within the OTHER active filters. If the segment leaked into this request, each
       * chip would report the count of the chip already selected and every other chip would
       * read zero.
       *
       * The refresh is driven through clearSearch rather than ngOnInit deliberately. ngOnInit
       * calls loadCounts BEFORE the first query-param emission, so the segment is still 'all'
       * at that point and segmentStatuses('all') is [] -- the request looks correct even with
       * the `forList` guard removed. Only a counts refresh raised while a real segment is
       * active can see the difference. A mutation run on 2026-09-17 proved that: replacing
       * `if (forList)` with `if (true)` killed no test until this one existed.
       */
      const c = create();
      started(c, String(AppointmentStatusType.Pending));
      expect(c.activeSegment()).not.toBe('all');

      c.clearSearch();

      const listInput = service['getList'].calls.mostRecent().args[0];
      const countsInput = service['getStatusCounts'].calls.mostRecent().args[0];
      expect(listInput.appointmentStatuses.length).toBeGreaterThan(0);
      expect(countsInput.appointmentStatuses).toBeUndefined();
    });
  });

  describe('search', () => {
    it('debounces before querying', fakeAsync(() => {
      const c = create();
      started(c);
      service['getList'].calls.reset();

      c.onSearch('lovelace');
      expect(service['getList']).not.toHaveBeenCalled();

      tick(300);
      expect(service['getList']).toHaveBeenCalled();
    }));

    it('cancels the previous timer so only the last keystroke queries', fakeAsync(() => {
      // Without clearTimeout every character would fire its own request.
      const c = create();
      started(c);
      service['getList'].calls.reset();

      c.onSearch('l');
      tick(100);
      c.onSearch('lo');
      tick(100);
      c.onSearch('lov');
      tick(300);

      expect(service['getList']).toHaveBeenCalledTimes(1);
    }));

    it('resets to the first page on a new search', fakeAsync(() => {
      const c = create();
      started(c);
      c.page.set(4);
      c.onSearch('lovelace');
      tick(300);
      expect(c.page()).toBe(1);
    }));

    it('sends the trimmed term to the server', fakeAsync(() => {
      const c = create();
      started(c);
      c.onSearch('  lovelace  ');
      tick(300);
      expect(service['getList'].calls.mostRecent().args[0].filterText).toBe('lovelace');
    }));

    it('omits an all-whitespace term rather than filtering on it', fakeAsync(() => {
      const c = create();
      started(c);
      c.onSearch('   ');
      tick(300);
      expect(service['getList'].calls.mostRecent().args[0].filterText).toBeUndefined();
    }));

    it('clears immediately without waiting for the debounce', () => {
      // Clearing is an explicit action, not typing: making the operator wait 300ms for the
      // full list to come back would feel broken.
      const c = create();
      started(c);
      c.search.set('lovelace');
      service['getList'].calls.reset();

      c.clearSearch();

      expect(c.search()).toBe('');
      expect(c.page()).toBe(1);
      expect(service['getList']).toHaveBeenCalled();
    });
  });

  describe('the filter drawer', () => {
    it('seeds the draft from the applied filters when opening', () => {
      /**
       * The draft is a working copy. Seeding it means reopening the drawer shows what is
       * currently applied rather than a blank form -- and the fixture applies a filter
       * first, or the copy would be indistinguishable from the empty default.
       */
      const c = create();
      started(c);
      c.filters.set({ panelNumber: 'PN-0001' });

      c.toggleFilters();

      expect(c.showFilters()).toBeTrue();
      expect(c.draft().panelNumber).toBe('PN-0001');
    });

    it('does not re-seed the draft when closing', () => {
      const c = create();
      started(c);
      c.toggleFilters();
      c.updateDraft({ panelNumber: 'typed-but-not-applied' });

      c.toggleFilters();

      expect(c.showFilters()).toBeFalse();
      expect(c.draft().panelNumber).toBe('typed-but-not-applied');
    });

    it('merges a draft patch rather than replacing it', () => {
      const c = create();
      started(c);
      c.updateDraft({ panelNumber: 'PN-0001' });
      c.updateDraft({ locationId: 'loc-1' });
      expect(c.draft()).toEqual({ panelNumber: 'PN-0001', locationId: 'loc-1' });
    });

    it('applies the draft, closes the drawer and re-pages', () => {
      const c = create();
      started(c);
      c.page.set(4);
      c.updateDraft({ panelNumber: 'PN-0001' });

      c.applyFilters();

      expect(c.filters().panelNumber).toBe('PN-0001');
      expect(c.showFilters()).toBeFalse();
      expect(c.page()).toBe(1);
    });

    it('sends the applied filters to the server', () => {
      const c = create();
      started(c);
      c.updateDraft({
        panelNumber: 'PN-0001',
        appointmentTypeId: 'type-1',
        locationId: 'loc-1',
        identityUserId: 'identity-1',
        dateMin: '2026-10-01',
        dateMax: '2026-10-31',
      });

      c.applyFilters();

      const input = service['getList'].calls.mostRecent().args[0];
      expect(input.panelNumber).toBe('PN-0001');
      expect(input.appointmentTypeId).toBe('type-1');
      expect(input.identityUserId).toBe('identity-1');
      expect(input.appointmentDateMin).toBe('2026-10-01');
    });

    it('extends the upper date bound to the end of that day', () => {
      /**
       * A date-only upper bound would exclude every appointment ON the chosen day, because
       * they all carry a time. Someone filtering "to 31 October" means the whole of it.
       */
      const c = create();
      started(c);
      c.updateDraft({ dateMax: '2026-10-31' });

      c.applyFilters();

      expect(service['getList'].calls.mostRecent().args[0].appointmentDateMax).toBe(
        '2026-10-31T23:59:59',
      );
    });

    it('clears the filters, the draft and the booker results together', () => {
      /**
       * A REMOVAL needing all three seeded. A stale booker result list left behind would
       * reopen the drawer showing a suggestion for a filter that is no longer applied.
       */
      const c = create();
      started(c);
      c.filters.set({ panelNumber: 'PN-0001' });
      c.draft.set({ panelNumber: 'PN-0001' });
      c.bookerResults.set([{ id: 'u1', displayName: 'Ada Lovelace' }]);
      c.page.set(3);

      c.clearFilters();

      expect(c.filters()).toEqual({});
      expect(c.draft()).toEqual({});
      expect(c.bookerResults()).toEqual([]);
      expect(c.page()).toBe(1);
    });

    it('counts only the filters that are set', () => {
      const c = create();
      started(c);
      expect(c.activeFilterCount()).toBe(0);
      c.filters.set({ panelNumber: 'PN-0001', locationId: 'loc-1', dateMin: '' });
      expect(c.activeFilterCount()).toBe(2);
    });

    it('does not count the booker LABEL, only the resolved id', () => {
      // bookerLabel is display text; filtering happens on identityUserId. Counting the
      // label would show an active filter for a half-typed name that filters nothing.
      const c = create();
      started(c);
      c.filters.set({ bookerLabel: 'Ada' });
      expect(c.activeFilterCount()).toBe(0);
    });
  });

  describe('the booker typeahead', () => {
    it('does not search on a term shorter than two characters', fakeAsync(() => {
      const c = create();
      started(c);
      c.onBookerInput('a');
      tick(300);
      expect(service['getIdentityUserLookup']).not.toHaveBeenCalled();
      expect(c.bookerResults()).toEqual([]);
    }));

    it('searches after the debounce for a longer term', fakeAsync(() => {
      const c = create();
      started(c);
      service['getIdentityUserLookup'].and.returnValue(
        of({ items: [{ id: 'u1', displayName: 'Ada Lovelace' }] }),
      );

      c.onBookerInput('ada');
      tick(300);

      expect(service['getIdentityUserLookup']).toHaveBeenCalled();
      expect(c.bookerResults().length).toBe(1);
    }));

    it('clears the resolved id while the operator retypes', () => {
      /**
       * A REMOVAL needing the id seeded: typing after a pick must invalidate the previous
       * selection, or the filter would keep querying the OLD booker while the box shows a
       * different name.
       */
      const c = create();
      started(c);
      c.updateDraft({ identityUserId: 'identity-1', bookerLabel: 'Ada Lovelace' });

      c.onBookerInput('gra');

      expect(c.draft().identityUserId).toBeUndefined();
      expect(c.draft().bookerLabel).toBe('gra');
    });

    it('records the id and label on pick, and closes the suggestion list', () => {
      const c = create();
      started(c);
      c.bookerResults.set([{ id: 'u1', displayName: 'Ada Lovelace' }]);

      c.pickBooker({ id: 'u1', displayName: 'Ada Lovelace' });

      expect(c.draft().identityUserId).toBe('u1');
      expect(c.draft().bookerLabel).toBe('Ada Lovelace');
      expect(c.bookerResults()).toEqual([]);
    });
  });

  describe('paging and sorting', () => {
    it('computes the page count from the total and page size', () => {
      const c = create();
      started(c);
      c.totalCount.set(42);
      c.pageSize.set(10);
      expect(c.totalPages()).toBe(5);
      expect(c.pages().length).toBe(5);
    });

    it('never reports fewer than one page', () => {
      // A zero-page list would render no pager at all and read as broken.
      const c = create();
      started(c);
      c.totalCount.set(0);
      expect(c.totalPages()).toBe(1);
    });

    it('describes the visible range', () => {
      const c = create();
      started(c);
      c.totalCount.set(42);
      c.pageSize.set(10);
      c.page.set(2);
      expect(c.rangeStart()).toBe(11);
      expect(c.rangeEnd()).toBe(20);
    });

    it('caps the range end at the total', () => {
      const c = create();
      started(c);
      c.totalCount.set(42);
      c.pageSize.set(10);
      c.page.set(5);
      expect(c.rangeEnd()).toBe(42);
    });

    it('reports a zero range for an empty list', () => {
      const c = create();
      started(c);
      c.totalCount.set(0);
      expect(c.rangeStart()).toBe(0);
    });

    it('refuses to page out of bounds or to the current page', () => {
      const c = create();
      started(c);
      c.totalCount.set(42);
      c.pageSize.set(10);
      c.page.set(2);
      service['getList'].calls.reset();

      c.goToPage(0);
      c.goToPage(99);
      c.goToPage(2);

      expect(service['getList']).not.toHaveBeenCalled();
      expect(c.page()).toBe(2);
    });

    it('pages within bounds', () => {
      const c = create();
      started(c);
      c.totalCount.set(42);
      c.pageSize.set(10);

      c.goToPage(3);

      expect(c.page()).toBe(3);
      expect(service['getList'].calls.mostRecent().args[0].skipCount).toBe(20);
    });

    it('returns to the first page when the page size changes', () => {
      // Staying on page 7 after switching to 50-per-page would land past the end.
      const c = create();
      started(c);
      c.page.set(7);
      c.setPageSize(50);
      expect(c.pageSize()).toBe(50);
      expect(c.page()).toBe(1);
    });

    it('returns to the first page when the sort changes', () => {
      const c = create();
      started(c);
      c.page.set(3);
      c.onSort({ key: 'appointment.appointmentDate', dir: 'desc' });
      expect(c.page()).toBe(1);
      expect(c.sort().key).toBe('appointment.appointmentDate');
    });

    it('sends no sorting clause until a column is chosen', () => {
      // An empty clause lets the repository apply its own default order.
      const c = create();
      started(c);
      expect(service['getList'].calls.mostRecent().args[0].sorting).toBeUndefined();
    });
  });

  describe('selection', () => {
    it('toggles a row on and off', () => {
      const c = create();
      started(c);
      const r = row();
      expect(c.isSelected(r)).toBeFalse();
      c.toggleSelect(r);
      expect(c.isSelected(r)).toBeTrue();
      expect(c.selectedCount()).toBe(1);
      c.toggleSelect(r);
      expect(c.selectedCount()).toBe(0);
    });

    it('selects and deselects every row on the page', () => {
      const c = create();
      service['getList'].and.returnValue(
        of({ items: [row({ id: 'a1' }), row({ id: 'a2' })], totalCount: 2 }),
      );
      started(c);

      c.toggleSelectAll();
      expect(c.selectedCount()).toBe(2);
      expect(c.allOnPageSelected()).toBeTrue();

      c.toggleSelectAll();
      expect(c.selectedCount()).toBe(0);
    });

    it('reports an empty page as not fully selected', () => {
      // `list.length > 0 &&` -- without it, `every` on an empty array is vacuously true and
      // the header checkbox would show ticked over no rows.
      const c = create();
      started(c);
      expect(c.allOnPageSelected()).toBeFalse();
    });

    it('keeps selections made on other pages', () => {
      /**
       * Selection is keyed by id and holds the ROW, so a CSV export can span pages. Clearing
       * it on navigation would silently drop rows the operator had already ticked.
       */
      const c = create();
      service['getList'].and.returnValue(of({ items: [row({ id: 'a1' })], totalCount: 2 }));
      started(c);
      c.toggleSelectAll();

      service['getList'].and.returnValue(of({ items: [row({ id: 'a2' })], totalCount: 2 }));
      c.goToPage(2);

      expect(c.selectedCount()).toBe(1);
    });

    it('clears the whole selection', () => {
      const c = create();
      started(c);
      c.toggleSelect(row());
      c.clearSelection();
      expect(c.selectedCount()).toBe(0);
    });
  });

  describe('the row menu', () => {
    function clickEvent() {
      return {
        currentTarget: {
          getBoundingClientRect: () => ({ bottom: 100, right: 500 }),
        },
      } as unknown as MouseEvent;
    }

    it('opens at a position computed from the button', () => {
      /**
       * The dropdown is positioned fixed because the table scrolls horizontally: inside the
       * overflow clip it was cut off and only nudged the scrollbar instead of opening.
       */
      const c = create();
      started(c);

      c.toggleMenu(row(), clickEvent());

      expect(c.menuId()).toBe('appt-1');
      expect(c.menuPos()).toEqual({ top: 104, left: 310 });
    });

    it('keeps the menu on screen for a button near the left edge', () => {
      const c = create();
      started(c);
      const event = {
        currentTarget: { getBoundingClientRect: () => ({ bottom: 50, right: 40 }) },
      } as unknown as MouseEvent;

      c.toggleMenu(row(), event);

      expect(c.menuPos()!.left).toBe(8);
    });

    it('closes when the same row is clicked again', () => {
      const c = create();
      started(c);
      c.toggleMenu(row(), clickEvent());
      c.toggleMenu(row(), clickEvent());
      expect(c.menuId()).toBeNull();
    });

    it('closes on Escape', () => {
      // The overlay is a div and not focusable, so a template-bound keydown would never
      // fire; the listener belongs on the document.
      const c = create();
      started(c);
      c.toggleMenu(row(), clickEvent());

      c.onEscapeKey();

      expect(c.menuId()).toBeNull();
    });

    it('is inert on Escape when no menu is open', () => {
      const c = create();
      started(c);
      expect(() => c.onEscapeKey()).not.toThrow();
      expect(c.menuId()).toBeNull();
    });

    it('closes the menu before navigating to the detail page', () => {
      const c = create();
      started(c);
      c.toggleMenu(row(), clickEvent());

      c.review(row());

      expect(c.menuId()).toBeNull();
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/view', 'appt-1']);
    });

    it('opens one change-request modal at a time', () => {
      const c = create();
      started(c);

      c.openReschedule(row());
      expect(c.rescheduleVisible()).toBeTrue();
      expect(c.cancelVisible()).toBeFalse();

      c.openCancel(row());
      expect(c.cancelVisible()).toBeTrue();
      expect(c.rescheduleVisible()).toBeFalse();
    });
  });

  describe('change requests', () => {
    it('confirms a filed request and reloads, without approving it', () => {
      /**
       * B3: a submitted change request stays Pending for a supervisor to finalize after both
       * parties consent. Chaining an approval here would bypass the consent flow entirely.
       */
      const c = create();
      started(c);
      service['getList'].calls.reset();

      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Cancel } as never);

      expect(toaster.success).toHaveBeenCalledWith('::Appointment:Toast:CancelRequested');
      expect(service['getList']).toHaveBeenCalled();
      expect(service['getStatusCounts']).toHaveBeenCalled();
    });

    it('uses the reschedule wording for a reschedule', () => {
      const c = create();
      started(c);
      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Reschedule } as never);
      expect(toaster.success).toHaveBeenCalledWith('::Appointment:Toast:RescheduleRequested');
    });
  });

  describe('deleting', () => {
    it('confirms before deleting a row', () => {
      const c = create();
      started(c);
      c.deleteRow(row());
      expect(confirmationWarn).toHaveBeenCalled();
      expect(service['delete']).toHaveBeenCalledWith('appt-1');
    });

    it('does not delete when the confirmation is declined', () => {
      const c = create();
      confirmationWarn.and.returnValue(of(Confirmation.Status.reject));
      started(c);

      c.deleteRow(row());

      expect(service['delete']).not.toHaveBeenCalled();
    });

    it('drops a deleted row from the selection', () => {
      /**
       * A REMOVAL needing the row selected first. Left behind, the CSV export would carry a
       * row that no longer exists and the bulk bar would count it.
       */
      const c = create();
      started(c);
      c.toggleSelect(row());
      expect(c.selectedCount()).toBe(1);

      c.deleteRow(row());

      expect(c.selectedCount()).toBe(0);
    });

    it('refuses a bulk delete without the permission', () => {
      // Intake lacks Appointments.Delete. The button is hidden, but the handler must not
      // rely on that -- a hidden control is not a gate.
      const c = create({ policies: [] });
      started(c);
      c.toggleSelect(row());

      c.bulkDelete();

      expect(confirmationWarn).not.toHaveBeenCalled();
      expect(service['delete']).not.toHaveBeenCalled();
    });

    it('refuses a bulk delete with nothing selected', () => {
      const c = create({ policies: ['CaseEvaluation.Appointments.Delete'] });
      started(c);
      c.bulkDelete();
      expect(confirmationWarn).not.toHaveBeenCalled();
    });

    it('deletes every selected row and clears the selection', () => {
      const c = create({ policies: ['CaseEvaluation.Appointments.Delete'] });
      service['getList'].and.returnValue(
        of({ items: [row({ id: 'a1' }), row({ id: 'a2' })], totalCount: 2 }),
      );
      started(c);
      c.toggleSelectAll();

      c.bulkDelete();

      expect(service['delete']).toHaveBeenCalledTimes(2);
      expect(c.selectedCount()).toBe(0);
    });

    it('names the row count in the bulk confirmation', () => {
      const c = create({ policies: ['CaseEvaluation.Appointments.Delete'] });
      service['getList'].and.returnValue(
        of({ items: [row({ id: 'a1' }), row({ id: 'a2' })], totalCount: 2 }),
      );
      started(c);
      c.toggleSelectAll();

      c.bulkDelete();

      expect(confirmationWarn.calls.mostRecent().args[2].messageLocalizationParams).toEqual(['2']);
    });
  });

  describe('permissions', () => {
    it('reads the delete and create policies at construction', () => {
      const c = create({
        policies: ['CaseEvaluation.Appointments.Delete', 'CaseEvaluation.Appointments.Create'],
      });
      expect(c.canDelete).toBeTrue();
      expect(c.canCreate).toBeTrue();
    });

    it('denies both when neither policy is granted', () => {
      const c = create({ policies: [] });
      expect(c.canDelete).toBeFalse();
      expect(c.canCreate).toBeFalse();
    });

    it('navigates to the booking wizard for a new appointment', () => {
      const c = create({ policies: ['CaseEvaluation.Appointments.Create'] });
      c.newAppointment();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/appointments/request');
    });
  });

  describe('CSV export', () => {
    function stubDownload() {
      const click = spyOn(HTMLAnchorElement.prototype, 'click');
      spyOn(URL, 'createObjectURL').and.returnValue('blob:fake');
      spyOn(URL, 'revokeObjectURL');
      return click;
    }

    it('does nothing with an empty selection', () => {
      const c = create();
      started(c);
      const click = stubDownload();
      c.exportCsv();
      expect(click).not.toHaveBeenCalled();
    });

    it('exports the selected rows', () => {
      const c = create();
      started(c);
      c.toggleSelect(row());
      const click = stubDownload();

      c.exportCsv();

      expect(click).toHaveBeenCalled();
    });

    it('releases the blob URL after the download', () => {
      const c = create();
      started(c);
      c.toggleSelect(row());
      stubDownload();

      c.exportCsv();

      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:fake');
    });

    it('strips non-ASCII characters from every cell', async () => {
      /**
       * HIPAA/export hygiene, and a real interoperability rule: the CSV is opened in Excel
       * downstream. The fixture uses a name carrying a non-ASCII character so the filter has
       * something to remove -- with a plain ASCII row it would be a no-op.
       *
       * The blob is captured from `createObjectURL` and read back, rather than spying on the
       * Blob constructor. Replacing a global constructor is the kind of fixture that breaks
       * for reasons unrelated to the code under test.
       */
      const c = create();
      started(c);
      const r = row();
      r.patient = { firstName: 'Zoë', lastName: 'Lovelace' };
      c.toggleSelect(r);
      spyOn(HTMLAnchorElement.prototype, 'click');
      spyOn(URL, 'revokeObjectURL');
      let captured: Blob | null = null;
      spyOn(URL, 'createObjectURL').and.callFake((blob: Blob) => {
        captured = blob;
        return 'blob:fake';
      });

      c.exportCsv();

      const text = await (captured as unknown as Blob).text();
      expect(text).toContain('Lovelace');
      expect(text).not.toContain('̈');
    });
  });

  describe('row display helpers', () => {
    it('reads the row id from the appointment', () => {
      const c = create();
      started(c);
      expect(c.rowId(row())).toBe('appt-1');
      expect(c.rowId({} as never)).toBe('');
    });

    it('joins the patient name', () => {
      const c = create();
      started(c);
      expect(c.patientName(row())).toBe('Ada Lovelace');
    });

    it('is empty when the row carries no patient', () => {
      const c = create();
      started(c);
      expect(c.patientName({ appointment: { id: 'a1' } } as never)).toBe('');
    });

    it('derives initials and an avatar colour', () => {
      const c = create();
      started(c);
      expect(c.initials(row())).toBeTruthy();
      expect(c.avatar(row())).toBeTruthy();
    });

    it('falls back to the row id for an avatar with no name', () => {
      // avatarColor needs a non-empty seed; a blank one would give every unnamed row the
      // same colour.
      const c = create();
      started(c);
      expect(c.avatar({ appointment: { id: 'appt-9' } } as never)).toBeTruthy();
    });

    it('shows a decide-by only for a pending appointment', () => {
      /**
       * Urgency is a review-queue concept. An approved appointment has already been decided,
       * so showing a countdown against it would be meaningless.
       */
      const c = create();
      started(c);
      expect(c.decideBy(row({ appointmentStatus: AppointmentStatusType.Pending }))).not.toBeNull();
      expect(c.decideBy(row({ appointmentStatus: AppointmentStatusType.Approved }))).toBeNull();
    });

    it('reports whether a row has actions available', () => {
      const c = create();
      started(c);
      expect(typeof c.actionable(row())).toBe('boolean');
    });
  });
});
