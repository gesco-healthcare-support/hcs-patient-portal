import type { ResetPasswordInput, SendPasswordResetCodeInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ExternalAccountService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  resetPassword = (input: ResetPasswordInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/public/external-account/reset-password',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  sendPasswordResetCode = (input: SendPasswordResetCodeInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/public/external-account/send-password-reset-code',
      body: input,
    },
    { apiName: this.apiName,...config });
}