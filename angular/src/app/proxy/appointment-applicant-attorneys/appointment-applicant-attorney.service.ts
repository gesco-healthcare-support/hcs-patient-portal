import type { AppointmentApplicantAttorneyCreateDto, AppointmentApplicantAttorneyDto, AppointmentApplicantAttorneyUpdateDto, AppointmentApplicantAttorneyWithNavigationPropertiesDto, GetAppointmentApplicantAttorneysInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentApplicantAttorneyService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentApplicantAttorneyCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentApplicantAttorneyDto>({
      method: 'POST',
      url: '/api/app/appointment-applicant-attorneys',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-applicant-attorneys/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentApplicantAttorneyDto>({
      method: 'GET',
      url: `/api/app/appointment-applicant-attorneys/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getApplicantAttorneyLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-applicant-attorneys/applicant-attorney-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getAppointmentLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-applicant-attorneys/appointment-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-applicant-attorneys/identity-user-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentApplicantAttorneysInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentApplicantAttorneyWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/appointment-applicant-attorneys',
      params: { filterText: input.filterText, appointmentId: input.appointmentId, applicantAttorneyId: input.applicantAttorneyId, identityUserId: input.identityUserId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentApplicantAttorneyWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/appointment-applicant-attorneys/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentApplicantAttorneyUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentApplicantAttorneyDto>({
      method: 'PUT',
      url: `/api/app/appointment-applicant-attorneys/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}