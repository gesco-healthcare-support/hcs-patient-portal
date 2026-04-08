import { inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ListService, TrackByService } from '@abp/ng.core';

import { finalize, tap } from 'rxjs/operators';

import type { WcabOfficeWithNavigationPropertiesDto } from '../../../proxy/wcab-offices/models';
import { WcabOfficeService } from '../../../proxy/wcab-offices/wcab-office.service';

export abstract class AbstractWcabOfficeDetailViewService {
  protected readonly fb = inject(FormBuilder);
  protected readonly track = inject(TrackByService);

  public readonly proxyService = inject(WcabOfficeService);
  public readonly list = inject(ListService);

  public readonly getStateLookup = this.proxyService.getStateLookup;

  isBusy = false;
  isVisible = false;
  selected = {} as any;
  form: FormGroup | undefined;

  protected createRequest() {
    const formValues = {
      ...this.form.value,
    };

    if (this.selected) {
      return this.proxyService.update(this.selected.wcabOffice.id, {
        ...formValues,
        concurrencyStamp: this.selected.wcabOffice.concurrencyStamp,
      });
    }

    return this.proxyService.create(formValues);
  }

  buildForm() {
    const { name, abbreviation, address, city, zipCode, isActive, stateId } =
      this.selected?.wcabOffice || {};

    this.form = this.fb.group({
      name: [name ?? null, [Validators.required, Validators.maxLength(50)]],
      abbreviation: [abbreviation ?? null, [Validators.required, Validators.maxLength(50)]],
      address: [address ?? null, [Validators.maxLength(100)]],
      city: [city ?? null, [Validators.maxLength(50)]],
      zipCode: [zipCode ?? null, [Validators.maxLength(15)]],
      isActive: [isActive ?? true, []],
      stateId: [stateId ?? null, []],
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

  update(record: WcabOfficeWithNavigationPropertiesDto) {
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
