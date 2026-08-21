import { inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ListService, TrackByService } from '@abp/ng.core';

import { finalize, tap } from 'rxjs/operators';

import type { AppointmentDocumentTypeDto } from '../../../proxy/appointment-document-types/models';
import { AppointmentDocumentTypeService } from '../../../proxy/appointment-document-types/appointment-document-type.service';
import type { AppointmentTypeDto } from '../../../proxy/appointment-types/models';
import { AppointmentTypeService } from '../../../proxy/appointment-types/appointment-type.service';

export abstract class AbstractAppointmentDocumentTypeDetailViewService {
  protected readonly fb = inject(FormBuilder);
  protected readonly track = inject(TrackByService);
  protected readonly appointmentTypeService = inject(AppointmentTypeService);

  public readonly proxyService = inject(AppointmentDocumentTypeService);
  public readonly list = inject(ListService);

  isBusy = false;
  isVisible = false;
  selected = {} as any;
  form: FormGroup | undefined;

  // Populates the "applies to" picker; null means the category applies to
  // every appointment type.
  appointmentTypes: AppointmentTypeDto[] = [];

  loadAppointmentTypes() {
    this.appointmentTypeService
      .getList({ maxResultCount: 1000, skipCount: 0 })
      .subscribe((res) => (this.appointmentTypes = res.items ?? []));
  }

  protected createRequest() {
    const formValues = {
      ...this.form.value,
    };

    if (this.selected) {
      return this.proxyService.update(this.selected.id, formValues);
    }

    return this.proxyService.create(formValues);
  }

  buildForm() {
    const { name, appointmentTypeId, isActive } = this.selected || {};

    this.form = this.fb.group({
      name: [name ?? null, [Validators.required, Validators.maxLength(100)]],
      appointmentTypeId: [appointmentTypeId ?? null],
      isActive: [this.selected ? !!isActive : true],
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

  update(record: AppointmentDocumentTypeDto) {
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
