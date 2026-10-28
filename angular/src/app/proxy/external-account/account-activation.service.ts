import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AccountActivationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  resendToOfficeAdministrator = (officeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/account-activation/offices/${officeId}/resend-administrator`,
    },
    { apiName: this.apiName,...config });
  

  resendToUser = (userId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/account-activation/users/${userId}/resend`,
    },
    { apiName: this.apiName,...config });
}