import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentTypeCreateDto {
  name: string;
  description?: string | null;
}

export interface AppointmentTypeDto extends FullAuditedEntityDto<string> {
  name?: string;
  description?: string | null;
}

export interface AppointmentTypeUpdateDto {
  name: string;
  description?: string | null;
}

export interface GetAppointmentTypesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  name?: string | null;
}
