import type { AppointmentChangeRequestDto, ApproveCancellationInput, ApproveRescheduleInput, GetChangeRequestsInput, RejectChangeRequestInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentChangeRequestApprovalService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  approveCancellation = (id: string, input: ApproveCancellationInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentChangeRequestDto>({
      method: 'POST',
      url: `/api/app/appointment-change-request-approvals/${id}/approve-cancellation`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  approveReschedule = (id: string, input: ApproveRescheduleInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentChangeRequestDto>({
      method: 'POST',
      url: `/api/app/appointment-change-request-approvals/${id}/approve-reschedule`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getPending = (input: GetChangeRequestsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentChangeRequestDto>>({
      method: 'GET',
      url: '/api/app/appointment-change-request-approvals/pending',
      params: { requestStatus: input.requestStatus, changeRequestType: input.changeRequestType, createdFromUtc: input.createdFromUtc, createdToUtc: input.createdToUtc, filterText: input.filterText, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  rejectCancellation = (id: string, input: RejectChangeRequestInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentChangeRequestDto>({
      method: 'POST',
      url: `/api/app/appointment-change-request-approvals/${id}/reject-cancellation`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  rejectReschedule = (id: string, input: RejectChangeRequestInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentChangeRequestDto>({
      method: 'POST',
      url: `/api/app/appointment-change-request-approvals/${id}/reject-reschedule`,
      body: input,
    },
    { apiName: this.apiName,...config });
}