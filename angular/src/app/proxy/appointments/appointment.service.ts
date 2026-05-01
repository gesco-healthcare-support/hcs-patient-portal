import type {
  AppointmentCreateDto,
  AppointmentDto,
  AppointmentSendBackInfoDto,
  AppointmentUpdateDto,
  AppointmentWithNavigationPropertiesDto,
  GetAppointmentsInput,
  RejectAppointmentInput,
  SendBackAppointmentInput,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type {
  AppFileDescriptorDto,
  DownloadTokenResultDto,
  GetFileInput,
  LookupDto,
  LookupRequestDto,
} from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: AppointmentCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>(
      {
        method: 'POST',
        url: '/api/app/appointments',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/app/appointments/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>(
      {
        method: 'GET',
        url: `/api/app/appointments/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  getAppointmentTypeLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/appointments/appointment-type-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getDoctorAvailabilityLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/appointments/doctor-availability-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getDownloadToken = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DownloadTokenResultDto>(
      {
        method: 'GET',
        url: '/api/app/appointments/download-token',
      },
      { apiName: this.apiName, ...config },
    );

  getFile = (input: GetFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>(
      {
        method: 'GET',
        responseType: 'blob',
        url: '/api/app/appointments/file',
        params: { downloadToken: input.downloadToken, fileId: input.fileId },
      },
      { apiName: this.apiName, ...config },
    );

  getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/appointments/identity-user-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getList = (input: GetAppointmentsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentWithNavigationPropertiesDto>>(
      {
        method: 'GET',
        url: '/api/app/appointments',
        params: {
          filterText: input.filterText,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
          panelNumber: input.panelNumber,
          appointmentDateMin: input.appointmentDateMin,
          appointmentDateMax: input.appointmentDateMax,
          isPatientAlreadyExist: input.isPatientAlreadyExist,
          requestConfirmationNumber: input.requestConfirmationNumber,
          dueDateMin: input.dueDateMin,
          dueDateMax: input.dueDateMax,
          internalUserComments: input.internalUserComments,
          appointmentApproveDateMin: input.appointmentApproveDateMin,
          appointmentApproveDateMax: input.appointmentApproveDateMax,
          appointmentStatus: input.appointmentStatus,
          patientId: input.patientId,
          identityUserId: input.identityUserId,
          accessorIdentityUserId: input.accessorIdentityUserId,
          appointmentTypeId: input.appointmentTypeId,
          locationId: input.locationId,
          doctorAvailabilityId: input.doctorAvailabilityId,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getLocationLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/appointments/location-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getPatientLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/appointments/patient-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentWithNavigationPropertiesDto>(
      {
        method: 'GET',
        url: `/api/app/appointments/with-navigation-properties/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  update = (id: string, input: AppointmentUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>(
      {
        method: 'PUT',
        url: `/api/app/appointments/${id}`,
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  uploadFile = (input: FormData, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppFileDescriptorDto>(
      {
        method: 'POST',
        url: '/api/app/appointments/upload-file',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  approve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>(
      {
        method: 'POST',
        url: `/api/app/appointments/${id}/approve`,
      },
      { apiName: this.apiName, ...config },
    );

  reject = (id: string, input: RejectAppointmentInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>(
      {
        method: 'POST',
        url: `/api/app/appointments/${id}/reject`,
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  sendBack = (id: string, input: SendBackAppointmentInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>(
      {
        method: 'POST',
        url: `/api/app/appointments/${id}/send-back`,
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  saveAndResubmit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>(
      {
        method: 'POST',
        url: `/api/app/appointments/${id}/save-and-resubmit`,
      },
      { apiName: this.apiName, ...config },
    );

  getLatestUnresolvedSendBackInfo = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentSendBackInfoDto | null>(
      {
        method: 'GET',
        url: `/api/app/appointments/${id}/send-back-info/latest`,
      },
      { apiName: this.apiName, ...config },
    );
}
