import { inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ListService, TrackByService } from '@abp/ng.core';

import { finalize, tap } from 'rxjs/operators';

import type { LocationWithNavigationPropertiesDto } from '../../../proxy/locations/models';
import { LocationService } from '../../../proxy/locations/location.service';

export abstract class AbstractLocationDetailViewService {
  protected readonly fb = inject(FormBuilder);
  protected readonly track = inject(TrackByService);

  public readonly proxyService = inject(LocationService);
  public readonly list = inject(ListService);

  public readonly getStateLookup = this.proxyService.getStateLookup;

  public readonly getAppointmentTypeLookup = this.proxyService.getAppointmentTypeLookup;

  isBusy = false;
  isVisible = false;
  selected = {} as any;
  form: FormGroup | undefined;

  protected createRequest() {
    const formValues = {
      ...this.form.value,
    };

    if (this.selected) {
      return this.proxyService.update(this.selected.location.id, {
        ...formValues,
        concurrencyStamp: this.selected.location.concurrencyStamp,
      });
    }

    return this.proxyService.create(formValues);
  }

  buildForm() {
    const { name, address, city, zipCode, parkingFee, isActive, stateId, appointmentTypeId } =
      this.selected?.location || {};

    this.form = this.fb.group({
      name: [name ?? null, [Validators.required, Validators.maxLength(50)]],
      address: [address ?? null, [Validators.maxLength(100)]],
      city: [city ?? null, [Validators.maxLength(50)]],
      zipCode: [zipCode ?? null, [Validators.maxLength(15)]],
      parkingFee: [parkingFee ?? null, [Validators.required]],
      isActive: [isActive ?? true, []],
      stateId: [stateId ?? null, []],
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

  update(record: LocationWithNavigationPropertiesDto) {
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
