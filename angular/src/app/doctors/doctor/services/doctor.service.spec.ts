import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ListService } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';

import { DoctorService } from '../../../proxy/doctors/doctor.service';
import type { DoctorWithNavigationPropertiesDto } from '../../../proxy/doctors/models';
import { DoctorViewService } from './doctor.service';

/**
 * The doctor list page's view service: the delete-after-confirm flow, the list query it hands
 * to ABP's ListService, and clearing the filters.
 *
 * <p>Every collaborator is a recording fake. The ListService fake runs the query function it is
 * given against a fixed page request, as ListService does when the page loads.</p>
 */
describe('DoctorViewService', () => {
  const record = { doctor: { id: 'TEST-doctor-1' } } as DoctorWithNavigationPropertiesDto;
  const page = { items: [record], totalCount: 1 };

  let deletedIds: string[];
  let listQueries: Record<string, unknown>[];
  let refreshes: number;
  let answer: Confirmation.Status;

  function build(): DoctorViewService {
    deletedIds = [];
    listQueries = [];
    refreshes = 0;

    const proxy = {
      delete: (id: string) => {
        deletedIds.push(id);
        return of(undefined);
      },
      getList: (input: Record<string, unknown>) => {
        listQueries.push(input);
        return of(page);
      },
      getWithNavigationProperties: () => of(record),
    };
    const confirmation = { warn: () => of(answer) };
    const list = {
      get: () => {
        refreshes += 1;
      },
      hookToQuery: (fn: (query: Record<string, unknown>) => unknown) =>
        fn({ skipCount: 20, maxResultCount: 10, filter: 'TEST-smith' }),
    };

    TestBed.configureTestingModule({
      providers: [
        DoctorViewService,
        { provide: DoctorService, useValue: proxy },
        { provide: ConfirmationService, useValue: confirmation },
        { provide: ListService, useValue: list },
      ],
    });
    return TestBed.inject(DoctorViewService);
  }

  afterEach(() => TestBed.resetTestingModule());

  it('deletes the doctor after the user confirms, then refreshes the list', () => {
    answer = Confirmation.Status.confirm;
    const service = build();

    service.delete(record);

    expect(deletedIds).toEqual(['TEST-doctor-1']);
    expect(refreshes).toBe(1);
  });

  it('deletes nothing and does not refresh when the user cancels', () => {
    answer = Confirmation.Status.reject;
    const service = build();

    service.delete(record);

    expect(deletedIds).toEqual([]);
    expect(refreshes).toBe(0);
  });

  it('queries with the page, the filters and the typed text, and keeps the page it gets back', () => {
    answer = Confirmation.Status.confirm;
    const service = build();
    service.filters = { firstName: 'TEST-Ann' } as DoctorViewService['filters'];

    service.hookToQuery();

    expect(listQueries).toEqual([
      {
        skipCount: 20,
        maxResultCount: 10,
        filter: 'TEST-smith',
        firstName: 'TEST-Ann',
        filterText: 'TEST-smith',
      },
    ]);
    expect(service.data).toBe(page);
  });

  it('clears the filters and refreshes the list', () => {
    answer = Confirmation.Status.confirm;
    const service = build();
    service.filters = { firstName: 'TEST-Ann' } as DoctorViewService['filters'];

    service.clearFilters();

    expect(service.filters).toEqual({} as DoctorViewService['filters']);
    expect(refreshes).toBe(1);
  });
});
