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
  

  upload = (appointmentId: string, form: UploadAppointmentDocumentForm, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/documents`,
      body: form.file,
    },
    { apiName: this.apiName,...config });
  

  uploadJointDeclaration = (appointmentId: string, form: UploadAppointmentDocumentForm, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/documents/upload-jdf`,
      body: form.file,
    },
    { apiName: this.apiName,...config });
  

  uploadPackage = (appointmentId: string, id: string, form: UploadAppointmentDocumentForm, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/documents/${id}/upload-package`,
      body: form.file,
    },
    { apiName: this.apiName,...config });
}