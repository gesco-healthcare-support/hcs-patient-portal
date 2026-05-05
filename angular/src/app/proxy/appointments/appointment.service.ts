import type { ApplicantAttorneyDetailsDto, AppointmentCreateDto, AppointmentDto, AppointmentUpdateDto, AppointmentWithNavigationPropertiesDto, DefenseAttorneyDetailsDto, GetAppointmentsInput, RejectAppointmentInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  approve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: `/api/app/appointments/${id}/approve`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: AppointmentCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: '/api/app/appointments',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createReval = (sourceConfirmationNumber: string, input: AppointmentCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: `/api/app/appointments/create-reval/${sourceConfirmationNumber}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointments/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'GET',
      url: `/api/app/appointments/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getApplicantAttorneyDetailsForBooking = (identityUserId?: string, email?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApplicantAttorneyDetailsDto>({
      method: 'GET',
      url: '/api/app/appointments/applicant-attorney-details-for-booking',
      params: { identityUserId, email },
    },
    { apiName: this.apiName,...config });
  

  getAppointmentApplicantAttorney = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApplicantAttorneyDetailsDto>({
      method: 'GET',
      url: `/api/app/appointments/${appointmentId}/applicant-attorney`,
    },
    { apiName: this.apiName,...config });
  

  getAppointmentDefenseAttorney = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DefenseAttorneyDetailsDto>({
      method: 'GET',
      url: `/api/app/appointments/${appointmentId}/defense-attorney`,
    },
    { apiName: this.apiName,...config });
  

  getAppointmentTypeLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointments/appointment-type-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getByConfirmationNumber = (requestConfirmationNumber: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/appointments/by-confirmation-number/${requestConfirmationNumber}`,
    },
    { apiName: this.apiName,...config });
  

  getDefenseAttorneyDetailsForBooking = (identityUserId?: string, email?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DefenseAttorneyDetailsDto>({
      method: 'GET',
      url: '/api/app/appointments/defense-attorney-details-for-booking',
      params: { identityUserId, email },
    },
    { apiName: this.apiName,...config });
  

  getDoctorAvailabilityLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointments/doctor-availability-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointments/identity-user-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/appointments',
      params: { filterText: input.filterText, panelNumber: input.panelNumber, appointmentDateMin: input.appointmentDateMin, appointmentDateMax: input.appointmentDateMax, identityUserId: input.identityUserId, accessorIdentityUserId: input.accessorIdentityUserId, appointmentTypeId: input.appointmentTypeId, locationId: input.locationId, appointmentStatus: input.appointmentStatus, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getLocationLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointments/location-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPatientLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointments/patient-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/appointments/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  reSubmit = (sourceConfirmationNumber: string, input: AppointmentCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: `/api/app/appointments/re-submit/${sourceConfirmationNumber}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  reject = (id: string, input: RejectAppointmentInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: `/api/app/appointments/${id}/reject`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'PUT',
      url: `/api/app/appointments/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  upsertApplicantAttorneyForAppointment = (appointmentId: string, input: ApplicantAttorneyDetailsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/applicant-attorney`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  upsertDefenseAttorneyForAppointment = (appointmentId: string, input: DefenseAttorneyDetailsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/defense-attorney`,
      body: input,
    },
    { apiName: this.apiName,...config });
}