import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentStatusCreateDto {
  name: string;
}

export interface AppointmentStatusDto extends FullAuditedEntityDto<string> {
  name?: string;
}

export interface AppointmentStatusUpdateDto {
  name: string;
}

export interface GetAppointmentStatusesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
}
