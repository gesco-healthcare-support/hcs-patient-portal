import type { AppointmentDto, ApproveAppointmentInput, RejectAppointmentInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentApprovalService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  approveAppointment = (id: string, input: ApproveAppointmentInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: `/api/app/appointment-approvals/${id}/approve`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getInternalUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-approvals/internal-user-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  rejectAppointment = (id: string, input: RejectAppointmentInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: `/api/app/appointment-approvals/${id}/reject`,
      body: input,
    },
    { apiName: this.apiName,...config });
}