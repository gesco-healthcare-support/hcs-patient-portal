import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ListService } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';

import { DoctorService } from '../../../proxy/doctors/doctor.service';
import type { DoctorWithNavigationPropertiesDto } from '../../../proxy/doctors/models';
import { DoctorViewService } from './doctor.service';

/**
 * A failed delete (#1113). ABP's HTTP handler has already toasted the user, so the view
 * service must not rethrow into the global ErrorHandler, and must not refresh the list as
 * though the delete had worked.
 */
describe('DoctorViewService failed delete', () => {
  const record = { doctor: { id: 'TEST-doctor-1' } } as DoctorWithNavigationPropertiesDto;

  afterEach(() => {
    jasmine.clock().uninstall();
    TestBed.resetTestingModule();
  });

  it('keeps the list as it was and raises no unhandled error', () => {
    let refreshes = 0;
    const list = {
      get: () => {
        refreshes += 1;
      },
      hookToQuery: () => of({ items: [record], totalCount: 1 }),
    };
    TestBed.configureTestingModule({
      providers: [
        DoctorViewService,
        { provide: DoctorService, useValue: { delete: () => throwError(() => new Error('500')) } },
        { provide: ConfirmationService, useValue: { warn: () => of(Confirmation.Status.confirm) } },
        { provide: ListService, useValue: list },
      ],
    });
    const service = TestBed.inject(DoctorViewService);
    service.data = { items: [record], totalCount: 1 };

    // RxJS reports an unhandled subscriber error on a timer; capture that timer.
    jasmine.clock().install();
    service.delete(record);

    expect(() => jasmine.clock().tick(1)).not.toThrow();
    expect(refreshes).toBe(0);
    expect(service.data.items).toEqual([record]);
  });
});
