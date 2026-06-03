import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentDocumentTypeCreateDto {
  name: string;
  appointmentTypeId?: string | null;
  isActive?: boolean;
}

export interface AppointmentDocumentTypeDto extends FullAuditedEntityDto<string> {
  name?: string;
  appointmentTypeId?: string | null;
  isSystem?: boolean;
  isActive?: boolean;
}

export interface AppointmentDocumentTypeUpdateDto {
  name: string;
  appointmentTypeId?: string | null;
  isActive?: boolean;
}

export interface GetAppointmentDocumentTypesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentTypeId?: string | null;
}
