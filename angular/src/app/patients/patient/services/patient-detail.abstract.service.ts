import { inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ListService, TrackByService } from '@abp/ng.core';

import { finalize, tap } from 'rxjs/operators';

import { genderOptions } from '../../../proxy/enums/gender.enum';
import { phoneNumberTypeOptions } from '../../../proxy/enums/phone-number-type.enum';
import type { PatientWithNavigationPropertiesDto } from '../../../proxy/patients/models';
import { PatientService } from '../../../proxy/patients/patient.service';

export abstract class AbstractPatientDetailViewService {
  protected readonly fb = inject(FormBuilder);
  protected readonly track = inject(TrackByService);

  public readonly proxyService = inject(PatientService);
  public readonly list = inject(ListService);

  public readonly getStateLookup = this.proxyService.getStateLookup;

  public readonly getAppointmentLanguageLookup = this.proxyService.getAppointmentLanguageLookup;

  public readonly getIdentityUserLookup = this.proxyService.getIdentityUserLookup;

  public readonly getTenantLookup = this.proxyService.getTenantLookup;

  genderOptions = genderOptions;
  phoneNumberTypeOptions = phoneNumberTypeOptions;

  isBusy = false;
  isVisible = false;
  selected = {} as any;
  form: FormGroup | undefined;

  protected createRequest() {
    const formValues = {
      ...this.form.value,
    };

    if (this.selected) {
      return this.proxyService.update(this.selected.patient.id, {
        ...formValues,
        concurrencyStamp: this.selected.patient.concurrencyStamp,
      });
    }

    return this.proxyService.create(formValues);
  }

  buildForm() {
    const {
      firstName,
      lastName,
      middleName,
      email,
      genderId,
      dateOfBirth,
      phoneNumber,
      socialSecurityNumber,
      address,
      city,
      zipCode,
      refferedBy,
      cellPhoneNumber,
      phoneNumberTypeId,
      street,
      interpreterVendorName,
      apptNumber,
      othersLanguageName,
      stateId,
      appointmentLanguageId,
      identityUserId,
      tenantId,
    } = this.selected?.patient || {};

    this.form = this.fb.group({
      firstName: [firstName ?? null, [Validators.required, Validators.maxLength(50)]],
      lastName: [lastName ?? null, [Validators.required, Validators.maxLength(50)]],
      middleName: [middleName ?? null, [Validators.maxLength(50)]],
      email: [email ?? null, [Validators.required, Validators.maxLength(50), Validators.email]],
      genderId: [genderId ?? null, [Validators.required]],
      dateOfBirth: [dateOfBirth ?? null, [Validators.required]],
      phoneNumber: [phoneNumber ?? null, [Validators.maxLength(20)]],
      socialSecurityNumber: [socialSecurityNumber ?? null, [Validators.maxLength(20)]],
      address: [address ?? null, [Validators.maxLength(100)]],
      city: [city ?? null, [Validators.maxLength(50)]],
      zipCode: [zipCode ?? null, [Validators.maxLength(15)]],
      refferedBy: [refferedBy ?? null, [Validators.maxLength(50)]],
      cellPhoneNumber: [cellPhoneNumber ?? null, [Validators.maxLength(12)]],
      phoneNumberTypeId: [phoneNumberTypeId ?? null, [Validators.required]],
      street: [street ?? null, [Validators.maxLength(255)]],
      interpreterVendorName: [interpreterVendorName ?? null, [Validators.maxLength(255)]],
      apptNumber: [apptNumber ?? null, [Validators.maxLength(100)]],
      othersLanguageName: [othersLanguageName ?? null, [Validators.maxLength(100)]],
      stateId: [stateId ?? null, []],
      appointmentLanguageId: [appointmentLanguageId ?? null, []],
      identityUserId: [identityUserId ?? null, [Validators.required]],
      tenantId: [tenantId ?? null, []],
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

  update(record: PatientWithNavigationPropertiesDto) {
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
