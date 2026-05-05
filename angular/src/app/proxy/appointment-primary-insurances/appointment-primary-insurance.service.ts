import type { AppointmentPrimaryInsuranceCreateDto, AppointmentPrimaryInsuranceDto, AppointmentPrimaryInsuranceUpdateDto, GetAppointmentPrimaryInsurancesInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentPrimaryInsuranceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentPrimaryInsuranceCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentPrimaryInsuranceDto>({
      method: 'POST',
      url: '/api/app/appointment-primary-insurances',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-primary-insurances/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentPrimaryInsuranceDto>({
      method: 'GET',
      url: `/api/app/appointment-primary-insurances/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentPrimaryInsurancesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentPrimaryInsuranceDto>>({
      method: 'GET',
      url: '/api/app/appointment-primary-insurances',
      params: { filterText: input.filterText, appointmentInjuryDetailId: input.appointmentInjuryDetailId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getStateLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-primary-insurances/state-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentPrimaryInsuranceUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentPrimaryInsuranceDto>({
      method: 'PUT',
      url: `/api/app/appointment-primary-insurances/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}