// Issue #117 (2026-05-13) -- HAND-PATCHED FILE. The ABP proxy generator
// (`abp generate-proxy -t ng -m app`) emits `body: form.file` for the
// three upload methods (sends the IFormFile as a raw JSON body) which
// fails server-side because the controller expects `[FromForm]
// UploadAppointmentDocumentForm`. ABP has historically not supported
// generating multipart proxies for `IFormFile` properties. The 4
// hand-patched method bodies below build a `FormData` payload that the
// RestService passes through unchanged so the request is correctly
// encoded as multipart/form-data. If `abp generate-proxy` is re-run,
// the upload, uploadPackage, and uploadJointDeclaration methods will
// regress -- re-apply the same FormData pattern.
import type { AppointmentDocumentDto, PatientPortalDocumentDto, RejectDocumentInput, UploadAppointmentDocumentForm } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentDocumentService {
  private restService = inject(RestService);
  apiName = 'Default';

  // Issue #117 (2026-05-13) -- build the multipart FormData expected
  // by the [FromForm] controller. `file` is required (server checks),
  // `documentName` is optional and only appended when present.
  private static buildUploadFormData(form: UploadAppointmentDocumentForm): FormData {
    const fd = new FormData();
    if (form.documentName !== undefined && form.documentName !== null && form.documentName !== '') {
      fd.append('DocumentName', form.documentName);
    }
    if (form.file) {
      fd.append('File', form.file as unknown as Blob);
    }
    return fd;
  }
  

  approve = (appointmentId: string, id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/documents/${id}/approve`,
    },
    { apiName: this.apiName,...config });
  

  delete = (appointmentId: string, id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointments/${appointmentId}/documents/${id}`,
    },
    { apiName: this.apiName,...config });
  

  download = (appointmentId: string, id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/app/appointments/${appointmentId}/documents/${id}/download`,
    },
    { apiName: this.apiName,...config });
  

  getCombinedForAppointment = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PatientPortalDocumentDto[]>({
      method: 'GET',
      url: `/api/app/appointments/${appointmentId}/documents/combined`,
    },
    { apiName: this.apiName,...config });
  

  getList = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto[]>({
      method: 'GET',
      url: `/api/app/appointments/${appointmentId}/documents`,
    },
    { apiName: this.apiName,...config });
  

  regeneratePacket = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/packet/regenerate`,
    },
    { apiName: this.apiName,...config });
  

  reject = (appointmentId: string, id: string, input: RejectDocumentInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/documents/${id}/reject`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  // Issue #117 (2026-05-13) HAND-PATCHED: was `body: form.file`. Now
  // sends FormData so the [FromForm] controller binds correctly.
  upload = (appointmentId: string, form: UploadAppointmentDocumentForm, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/documents`,
      body: AppointmentDocumentService.buildUploadFormData(form),
    },
    { apiName: this.apiName,...config });


  // Issue #117 (2026-05-13) HAND-PATCHED: was `body: form.file`.
  uploadJointDeclaration = (appointmentId: string, form: UploadAppointmentDocumentForm, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/documents/upload-jdf`,
      body: AppointmentDocumentService.buildUploadFormData(form),
    },
    { apiName: this.apiName,...config });


  // Issue #117 (2026-05-13) HAND-PATCHED: was `body: form.file`.
  uploadPackage = (appointmentId: string, id: string, form: UploadAppointmentDocumentForm, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/documents/${id}/upload-package`,
      body: AppointmentDocumentService.buildUploadFormData(form),
    },
    { apiName: this.apiName,...config });
}