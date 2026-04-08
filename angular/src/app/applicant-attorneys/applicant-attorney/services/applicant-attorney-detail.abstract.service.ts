import { inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ListService, TrackByService } from '@abp/ng.core';

import { finalize, tap } from 'rxjs/operators';

import type { ApplicantAttorneyWithNavigationPropertiesDto } from '../../../proxy/applicant-attorneys/models';
import { ApplicantAttorneyService } from '../../../proxy/applicant-attorneys/applicant-attorney.service';

export abstract class AbstractApplicantAttorneyDetailViewService {
  protected readonly fb = inject(FormBuilder);
  protected readonly track = inject(TrackByService);

  public readonly proxyService = inject(ApplicantAttorneyService);
  public readonly list = inject(ListService);

  public readonly getStateLookup = this.proxyService.getStateLookup;

  public readonly getIdentityUserLookup = this.proxyService.getIdentityUserLookup;

  isBusy = false;
  isVisible = false;
  selected = {} as any;
  form: FormGroup | undefined;

  protected createRequest() {
    const formValues = {
      ...this.form.value,
    };

    if (this.selected) {
      return this.proxyService.update(this.selected.applicantAttorney.id, {
        ...formValues,
        concurrencyStamp: this.selected.applicantAttorney.concurrencyStamp,
      });
    }

    return this.proxyService.create(formValues);
  }

  buildForm() {
    const {
      firmName,
      firmAddress,
      webAddress,
      phoneNumber,
      faxNumber,
      street,
      city,
      zipCode,
      stateId,
      identityUserId,
    } = this.selected?.applicantAttorney || {};

    this.form = this.fb.group({
      firmName: [firmName ?? null, [Validators.maxLength(50)]],
      firmAddress: [firmAddress ?? null, [Validators.maxLength(100)]],
      webAddress: [webAddress ?? null, [Validators.maxLength(100)]],
      phoneNumber: [phoneNumber ?? null, [Validators.maxLength(20)]],
      faxNumber: [faxNumber ?? null, [Validators.maxLength(19)]],
      street: [street ?? null, [Validators.maxLength(255)]],
      city: [city ?? null, [Validators.maxLength(50)]],
      zipCode: [zipCode ?? null, [Validators.maxLength(10)]],
      stateId: [stateId ?? null, []],
      identityUserId: [identityUserId ?? null, [Validators.required]],
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

  update(record: ApplicantAttorneyWithNavigationPropertiesDto) {
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
