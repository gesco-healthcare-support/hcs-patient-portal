import type { ConfirmationNumberLookupDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ConfirmationNumberLookupsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  request = (input: ConfirmationNumberLookupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/confirmation-number-lookups',
      body: input,
    },
    { apiName: this.apiName,...config });
}