import type { AppointmentDocumentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentDocumentService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto[]>(
      {
        method: 'GET',
        url: `/api/app/appointments/${appointmentId}/documents`,
      },
      { apiName: this.apiName, ...config },
    );

  upload = (
    appointmentId: string,
    body: FormData,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, AppointmentDocumentDto>(
      {
        method: 'POST',
        url: `/api/app/appointments/${appointmentId}/documents`,
        body,
      },
      { apiName: this.apiName, ...config },
    );

  /** Returns the absolute URL the browser should hit for a download. The
   * RestService applies the configured API base URL + auth headers via
   * <a> click; for inline streaming we pass through window.open. */
  buildDownloadUrl = (appointmentId: string, id: string): string =>
    `/api/app/appointments/${appointmentId}/documents/${id}/download`;

  delete = (appointmentId: string, id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/app/appointments/${appointmentId}/documents/${id}`,
      },
      { apiName: this.apiName, ...config },
    );
}
