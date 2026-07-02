import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AssignIntakeOfficeDto {
  operatorUserId: string;
  officeId: string;
}

export interface GetIntakeAssignmentsInput extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface IntakeOfficeAssignmentDto {
  id?: string;
  operatorUserId?: string;
  operatorName?: string;
  operatorEmail?: string;
  officeId?: string;
  officeName?: string;
}

export interface IntakeOfficeMetricsDto {
  officeId?: string;
  officeName?: string;
  pendingRequests?: number;
  todayAppointments?: number;
  pendingChangeRequests?: number;
}
