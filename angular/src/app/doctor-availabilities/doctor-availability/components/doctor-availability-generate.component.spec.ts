import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DoctorAvailabilityService } from '../../../proxy/doctor-availabilities/doctor-availability.service';
import { DoctorAvailabilityGenerateComponent } from './doctor-availability-generate.component';

/**
 * Pure-helper coverage for the multi-axis generate form. Tests instantiate
 * the component class inside TestBed's injection context to satisfy inject()
 * calls without compiling the full LeptonX/ABP template tree.
 */
describe('DoctorAvailabilityGenerateComponent', () => {
  function createComponent(): DoctorAvailabilityGenerateComponent {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: DoctorAvailabilityService,
          useValue: { getLocationLookup: () => {}, getAppointmentTypeLookup: () => {} },
        },
        { provide: Router, useValue: { navigate: () => {} } },
        { provide: ToasterService, useValue: { success: () => {} } },
        { provide: LocalizationService, useValue: { instant: (key: unknown) => String(key) } },
      ],
    });
    return TestBed.runInInjectionContext(() => new DoctorAvailabilityGenerateComponent());
  }

  afterEach(() => TestBed.resetTestingModule());

  it('buildPayload: when all 7 weekdays checked, sends empty SelectedDays array (server-side "any weekday" sentinel)', () => {
    const c = createComponent();
    c.selectedDaysGroup.patchValue({
      0: true,
      1: true,
      2: true,
      3: true,
      4: true,
      5: true,
      6: true,
    });
    c.form.patchValue({
      locationId: 'loc-1',
      fromDate: '2026-06-01',
      toDate: '2026-06-07',
    });
    c.timeRanges.at(0).patchValue({ fromTime: '08:00', toTime: '10:00' });

    const payload = c.buildPayload();

    expect(payload.selectedDays).toEqual([]);
  });

  it('buildPayload: when Mon + Wed + Fri checked, sends [1, 3, 5]', () => {
    const c = createComponent();
    c.selectedDaysGroup.patchValue({
      0: false,
      1: true,
      2: false,
      3: true,
      4: false,
      5: true,
      6: false,
    });
    c.form.patchValue({
      locationId: 'loc-1',
      fromDate: '2026-06-01',
      toDate: '2026-06-07',
    });
    c.timeRanges.at(0).patchValue({ fromTime: '08:00', toTime: '10:00' });

    const payload = c.buildPayload();

    expect(payload.selectedDays).toEqual([1, 3, 5]);
  });

  it('buildPayload: normalizes "HH:mm" times to "HH:mm:00"', () => {
    const c = createComponent();
    c.form.patchValue({
      locationId: 'loc-1',
      fromDate: '2026-06-01',
      toDate: '2026-06-07',
    });
    c.timeRanges.at(0).patchValue({ fromTime: '08:00', toTime: '10:30' });

    const payload = c.buildPayload();

    expect(payload.timeRanges?.[0].fromTime).toBe('08:00:00');
    expect(payload.timeRanges?.[0].toTime).toBe('10:30:00');
  });

  it('addTimeRange: appends a fresh empty row', () => {
    const c = createComponent();

    expect(c.timeRanges.length).toBe(1);

    c.addTimeRange();

    expect(c.timeRanges.length).toBe(2);
    expect(c.timeRanges.at(1).value).toEqual({
      fromTime: null,
      toTime: null,
      appointmentDurationMinutes: null,
    });
  });

  it('removeTimeRange: refuses to remove the last remaining row', () => {
    const c = createComponent();

    expect(c.timeRanges.length).toBe(1);

    c.removeTimeRange(0);

    expect(c.timeRanges.length).toBe(1);
  });

  it('buildPayload: capacity defaults to 3 (locked decision 2026-05-27)', () => {
    const c = createComponent();
    c.form.patchValue({
      locationId: 'loc-1',
      fromDate: '2026-06-01',
      toDate: '2026-06-07',
    });
    c.timeRanges.at(0).patchValue({ fromTime: '08:00', toTime: '10:00' });

    const payload = c.buildPayload();

    expect(payload.capacity).toBe(3);
  });
});
