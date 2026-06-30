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
