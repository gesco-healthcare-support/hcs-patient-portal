import { inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ListService, TrackByService } from '@abp/ng.core';

import { finalize, tap } from 'rxjs/operators';

import { appointmentStatusTypeOptions } from '../../../proxy/enums/appointment-status-type.enum';
import type { AppointmentWithNavigationPropertiesDto } from '../../../proxy/appointments/models';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';

export abstract class AbstractAppointmentDetailViewService {
  protected readonly fb = inject(FormBuilder);
  protected readonly track = inject(TrackByService);

  public readonly proxyService = inject(AppointmentService);
  public readonly list = inject(ListService);

  public readonly getPatientLookup = this.proxyService.getPatientLookup;

  public readonly getIdentityUserLookup = this.proxyService.getIdentityUserLookup;

  public readonly getAppointmentTypeLookup = this.proxyService.getAppointmentTypeLookup;

  public readonly getLocationLookup = this.proxyService.getLocationLookup;

  public readonly getDoctorAvailabilityLookup = this.proxyService.getDoctorAvailabilityLookup;

  appointmentStatusTypeOptions = appointmentStatusTypeOptions;

  isBusy = false;
  isVisible = false;
  selected = {} as any;
  form: FormGroup | undefined;

  protected createRequest() {
    const formValues = {
      ...this.form.value,
    };

    if (this.selected) {
      return this.proxyService.update(this.selected.appointment.id, {
        ...formValues,
        concurrencyStamp: this.selected.appointment.concurrencyStamp,
      });
    }

    return this.proxyService.create(formValues);
  }

  buildForm() {
    const {
      panelNumber,
      appointmentDate,
      isPatientAlreadyExist,
      requestConfirmationNumber,
      dueDate,
      internalUserComments,
      appointmentApproveDate,
      appointmentStatus,
      patientId,
      identityUserId,
      appointmentTypeId,
      locationId,
      doctorAvailabilityId,
    } = this.selected?.appointment || {};

    this.form = this.fb.group({
      panelNumber: [panelNumber ?? null, [Validators.maxLength(50)]],
      appointmentDate: [appointmentDate ?? null, [Validators.required]],
      isPatientAlreadyExist: [isPatientAlreadyExist ?? false, []],
      requestConfirmationNumber: [
        requestConfirmationNumber ?? null,
        [Validators.required, Validators.maxLength(50)],
      ],
      dueDate: [dueDate ?? null, []],
      internalUserComments: [internalUserComments ?? null, [Validators.maxLength(250)]],
      appointmentApproveDate: [appointmentApproveDate ?? null, []],
      appointmentStatus: [appointmentStatus ?? null, [Validators.required]],
      patientId: [patientId ?? null, [Validators.required]],
      identityUserId: [identityUserId ?? null, [Validators.required]],
      appointmentTypeId: [appointmentTypeId ?? null, [Validators.required]],
      locationId: [locationId ?? null, [Validators.required]],
      doctorAvailabilityId: [doctorAvailabilityId ?? null, [Validators.required]],
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

  update(record: AppointmentWithNavigationPropertiesDto) {
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
