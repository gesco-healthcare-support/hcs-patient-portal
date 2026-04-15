import { inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ListService, TrackByService } from '@abp/ng.core';

import { finalize, tap } from 'rxjs/operators';

import { genderOptions } from '../../../proxy/enums/gender.enum';
import type { DoctorWithNavigationPropertiesDto } from '../../../proxy/doctors/models';
import { DoctorService } from '../../../proxy/doctors/doctor.service';

export abstract class AbstractDoctorDetailViewService {
  protected readonly fb = inject(FormBuilder);
  protected readonly track = inject(TrackByService);

  public readonly proxyService = inject(DoctorService);
  public readonly list = inject(ListService);

  public readonly getAppointmentTypeLookup = this.proxyService.getAppointmentTypeLookup;

  public readonly getLocationLookup = this.proxyService.getLocationLookup;

  public readonly getIdentityUserLookup = this.proxyService.getIdentityUserLookup;

  public readonly getTenantLookup = this.proxyService.getTenantLookup;

  genderOptions = genderOptions;

  isBusy = false;
  isVisible = false;
  selected = {} as any;
  form: FormGroup | undefined;

  protected createRequest() {
    const formValues = {
      ...this.form.value,
      appointmentTypeIds: this.form.value.appointmentTypeIds.map(({ id }) => id),
      locationIds: this.form.value.locationIds.map(({ id }) => id),
    };

    if (this.selected) {
      return this.proxyService.update(this.selected.doctor.id, {
        ...formValues,
        concurrencyStamp: this.selected.doctor.concurrencyStamp,
      });
    }

    return this.proxyService.create(formValues);
  }

  buildForm() {
    const { firstName, lastName, email, gender, identityUserId, tenantId } =
      this.selected?.doctor || {};

    const { appointmentTypes = [], locations = [] } = this.selected || {};

    this.form = this.fb.group({
      firstName: [firstName ?? null, [Validators.required, Validators.maxLength(50)]],
      lastName: [lastName ?? null, [Validators.required, Validators.maxLength(50)]],
      email: [email ?? null, [Validators.required, Validators.maxLength(49), Validators.email]],
      gender: [gender ?? null, [Validators.required]],
      identityUserId: [identityUserId ?? null, []],
      tenantId: [tenantId ?? null, []],
      appointmentTypeIds: [appointmentTypes, []],
      locationIds: [locations, []],
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

  update(record: DoctorWithNavigationPropertiesDto) {
    this.proxyService.getWithNavigationProperties(record.doctor.id).subscribe((data) => {
      this.selected = data;
      this.showForm();
    });
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
