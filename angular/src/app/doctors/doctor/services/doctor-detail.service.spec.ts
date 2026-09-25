import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ListService } from '@abp/ng.core';

import { DoctorService } from '../../../proxy/doctors/doctor.service';
import type { DoctorWithNavigationPropertiesDto } from '../../../proxy/doctors/models';
import { DoctorDetailViewService } from './doctor-detail.service';

/**
 * The doctor create/edit modal's view service: the form it builds, the create and update requests
 * it sends, and the busy and visible flags around a submit.
 *
 * <p>The proxy is a recording fake and FormBuilder is the real one, so the validators under test are
 * the service's own.</p>
 *
 * <p>NOT TESTED: a failed submit. `submitForm` subscribes without an error handler, so RxJS rethrows
 * the error asynchronously, and under karma that fails whichever test happens to be running.</p>
 */
describe('DoctorDetailViewService', () => {
  const stored: DoctorWithNavigationPropertiesDto = {
    doctor: {
      id: 'TEST-doctor-1',
      firstName: 'TEST-Ann',
      lastName: 'TEST-Lee',
      email: 'ann.lee@test.local',
      gender: 2,
      concurrencyStamp: 'TEST-stamp-1',
    },
    appointmentTypes: [{ id: 'TEST-type-1' }],
    locations: [{ id: 'TEST-location-1' }],
  } as DoctorWithNavigationPropertiesDto;

  let created: unknown[];
  let updated: { id: string; input: Record<string, unknown> }[];
  let refreshes: number;
  function build(): DoctorDetailViewService {
    created = [];
    updated = [];
    refreshes = 0;

    const proxy = {
      create: (input: unknown) => {
        created.push(input);
        return of({});
      },
      update: (id: string, input: Record<string, unknown>) => {
        updated.push({ id, input });
        return of({});
      },
      getWithNavigationProperties: () => of(stored),
      getAppointmentTypeLookup: () => of({ items: [] }),
      getLocationLookup: () => of({ items: [] }),
    };
    const list = {
      get: () => {
        refreshes += 1;
      },
    };

    TestBed.configureTestingModule({
      providers: [
        DoctorDetailViewService,
        { provide: DoctorService, useValue: proxy },
        { provide: ListService, useValue: list },
      ],
    });
    return TestBed.inject(DoctorDetailViewService);
  }

  afterEach(() => TestBed.resetTestingModule());

  function fillValidForm(service: DoctorDetailViewService) {
    service.form!.patchValue({
      firstName: 'TEST-Bo',
      lastName: 'TEST-Kim',
      email: 'bo.kim@test.local',
      gender: 1,
      appointmentTypeIds: [{ id: 'TEST-type-2' }],
      locationIds: [{ id: 'TEST-location-2' }, { id: 'TEST-location-3' }],
    });
  }

  it('opens an empty form for a new doctor', () => {
    const service = build();

    service.create();

    expect(service.selected).toBeUndefined();
    expect(service.isVisible).toBe(true);
    expect(service.form!.value).toEqual({
      firstName: null,
      lastName: null,
      email: null,
      gender: null,
      appointmentTypeIds: [],
      locationIds: [],
    });
    expect(service.form!.invalid).toBe(true);
  });

  it('loads the stored doctor and fills the form with it for an edit', () => {
    const service = build();

    service.update({ doctor: { id: 'TEST-doctor-1' } } as DoctorWithNavigationPropertiesDto);

    expect(service.selected).toBe(stored);
    expect(service.isVisible).toBe(true);
    expect(service.form!.value.firstName).toBe('TEST-Ann');
    expect(service.form!.value.email).toBe('ann.lee@test.local');
    expect(service.form!.value.locationIds).toEqual([{ id: 'TEST-location-1' }]);
  });

  it('rejects a malformed email and an over-long first name', () => {
    const service = build();
    service.create();
    fillValidForm(service);
    expect(service.form!.valid).toBe(true);

    service.form!.patchValue({ email: 'not-an-email' });
    expect(service.form!.get('email')!.hasError('email')).toBe(true);

    service.form!.patchValue({ email: 'bo.kim@test.local', firstName: 'x'.repeat(51) });
    expect(service.form!.get('firstName')!.hasError('maxlength')).toBe(true);
  });

  it('creates a new doctor with the picked lookup items reduced to ids, then closes and refreshes', () => {
    const service = build();
    service.create();
    fillValidForm(service);

    service.submitForm();

    expect(created).toEqual([
      {
        firstName: 'TEST-Bo',
        lastName: 'TEST-Kim',
        email: 'bo.kim@test.local',
        gender: 1,
        appointmentTypeIds: ['TEST-type-2'],
        locationIds: ['TEST-location-2', 'TEST-location-3'],
      },
    ]);
    expect(updated).toEqual([]);
    expect(service.isVisible).toBe(false);
    expect(service.isBusy).toBe(false);
    expect(refreshes).toBe(1);
  });

  it('updates the stored doctor with its concurrency stamp', () => {
    const service = build();
    service.update({ doctor: { id: 'TEST-doctor-1' } } as DoctorWithNavigationPropertiesDto);

    service.submitForm();

    expect(created).toEqual([]);
    expect(updated.length).toBe(1);
    expect(updated[0].id).toBe('TEST-doctor-1');
    expect(updated[0].input['concurrencyStamp']).toBe('TEST-stamp-1');
    expect(updated[0].input['appointmentTypeIds']).toEqual(['TEST-type-1']);
  });

  it('sends nothing while the form is invalid', () => {
    const service = build();
    service.create();

    service.submitForm();

    expect(created).toEqual([]);
    expect(service.isBusy).toBe(false);
    expect(service.isVisible).toBe(true);
  });

  it('follows the modal when it is closed from outside', () => {
    const service = build();
    service.create();

    service.changeVisible(false);
    expect(service.isVisible).toBe(false);

    service.showForm();
    service.hideForm();
    expect(service.isVisible).toBe(false);
  });
});
