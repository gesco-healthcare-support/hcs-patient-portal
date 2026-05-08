import type { AppointmentDocumentDto, UploadAppointmentDocumentForm } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PublicDocumentUploadService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  uploadByVerificationCode = (id: string, verificationCode: string, form: UploadAppointmentDocumentForm, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/public/appointment-documents/${id}/upload-by-code/${verificationCode}`,
      body: form.file,
    },
    { apiName: this.apiName,...config });
}