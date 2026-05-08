import type { AppointmentClaimExaminerCreateDto, AppointmentClaimExaminerDto, AppointmentClaimExaminerUpdateDto, GetAppointmentClaimExaminersInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentClaimExaminerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentClaimExaminerCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentClaimExaminerDto>({
      method: 'POST',
      url: '/api/app/appointment-claim-examiners',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-claim-examiners/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentClaimExaminerDto>({
      method: 'GET',
      url: `/api/app/appointment-claim-examiners/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentClaimExaminersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentClaimExaminerDto>>({
      method: 'GET',
      url: '/api/app/appointment-claim-examiners',
      params: { filterText: input.filterText, appointmentInjuryDetailId: input.appointmentInjuryDetailId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getStateLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-claim-examiners/state-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentClaimExaminerUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentClaimExaminerDto>({
      method: 'PUT',
      url: `/api/app/appointment-claim-examiners/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}