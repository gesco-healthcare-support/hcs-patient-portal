import { inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ListService, TrackByService } from '@abp/ng.core';
import { formatDate } from '@angular/common';

import { finalize, tap } from 'rxjs/operators';

import { bookingStatusOptions } from '../../../proxy/enums/booking-status.enum';
import type { DoctorAvailabilityWithNavigationPropertiesDto } from '../../../proxy/doctor-availabilities/models';
import { DoctorAvailabilityService } from '../../../proxy/doctor-availabilities/doctor-availability.service';

export abstract class AbstractDoctorAvailabilityDetailViewService {
  protected readonly fb = inject(FormBuilder);
  protected readonly track = inject(TrackByService);

  public readonly proxyService = inject(DoctorAvailabilityService);
  public readonly list = inject(ListService);

  public readonly getLocationLookup = this.proxyService.getLocationLookup;

  public readonly getAppointmentTypeLookup = this.proxyService.getAppointmentTypeLookup;

  bookingStatusOptions = bookingStatusOptions;

  isBusy = false;
  isVisible = false;
  selected = {} as any;
  form: FormGroup | undefined;

  protected normalizeTime(value: string | null | undefined): string | null {
    if (!value) {
      return null;
    }

    const trimmed = value.trim();
    if (!trimmed) {
      return null;
    }

    const main = trimmed.split('.')[0];
    const parts = main.split(':');

    if (parts.length === 2) {
      return `${parts[0]}:${parts[1]}:00`;
    }

    return main;
  }

  protected normalizeDate(value: string | Date | null | undefined): string | null {
    if (!value) {
      return null;
    }

    const date = value instanceof Date ? value : new Date(value);
    if (isNaN(date.getTime())) {
      return null;
    }

    return formatDate(date, 'yyyy-MM-dd', 'en');
  }

  protected createRequest() {
    const formValues = {
      ...this.form.value,
      availableDate: this.normalizeDate(this.form.value.availableDate),
      fromTime: this.normalizeTime(this.form.value.fromTime),
      toTime: this.normalizeTime(this.form.value.toTime),
    };

    if (this.selected) {
      return this.proxyService.update(this.selected.doctorAvailability.id, {
        ...formValues,
        concurrencyStamp: this.selected.doctorAvailability.concurrencyStamp,
      });
    }

    return this.proxyService.create(formValues);
  }

  buildForm() {
    const { availableDate, fromTime, toTime, bookingStatusId, locationId, appointmentTypeId } =
      this.selected?.doctorAvailability || {};

    this.form = this.fb.group({
      availableDate: [this.normalizeDate(availableDate) ?? null, [Validators.required]],
      fromTime: [this.normalizeTime(fromTime) ?? null, [Validators.required]],
      toTime: [this.normalizeTime(toTime) ?? null, [Validators.required]],
      bookingStatusId: [bookingStatusId ?? this.bookingStatusOptions[0]?.value ?? null, [Validators.required]],
      locationId: [locationId ?? null, [Validators.required]],
      appointmentTypeId: [appointmentTypeId ?? null, []],
    });
  }

  showForm() {
    this.buildForm();
    this.isVisible = true;
  }

  create() {
    this.selected = undefined;
    this.showForm();
  }

  update(record: DoctorAvailabilityWithNavigationPropertiesDto) {
    this.selected = record;
    this.showForm();
  }

  hideForm() {
    this.isVisible = false;
  }

  submitForm() {
    if (this.form.invalid) return;

    this.isBusy = true;

    const request = this.createRequest().pipe(
      finalize(() => (this.isBusy = false)),
      tap(() => this.hideForm()),
    );

    request.subscribe(this.list.get);
  }

  changeVisible($event: boolean) {
    this.isVisible = $event;
  }
}
