import type { AssignIntakeOfficeDto, IntakeOfficeAssignmentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class IntakeAssignmentsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  assign = (input: AssignIntakeOfficeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/intake-assignments/assign',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getAssignableOperators = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/intake-assignments/assignable-operators',
    },
    { apiName: this.apiName,...config });
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<IntakeOfficeAssignmentDto>>({
      method: 'GET',
      url: '/api/app/intake-assignments',
    },
    { apiName: this.apiName,...config });
  

  getMyOffices = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/intake-assignments/my-offices',
    },
    { apiName: this.apiName,...config });
  

  getOfficeOptions = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/intake-assignments/office-options',
    },
    { apiName: this.apiName,...config });
  

  unassign = (operatorUserId: string, officeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/intake-assignments/unassign',
      params: { operatorUserId, officeId },
    },
    { apiName: this.apiName,...config });
}