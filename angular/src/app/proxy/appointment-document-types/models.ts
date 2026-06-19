import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentDocumentTypeCreateDto {
  name: string;
  appointmentTypeIds?: string[];
  appliesToAll?: boolean;
  isActive?: boolean;
}

export interface AppointmentDocumentTypeDto extends FullAuditedEntityDto<string> {
  name?: string;
  appointmentTypeIds?: string[];
  appliesToAll?: boolean;
  isSystem?: boolean;
  isActive?: boolean;
  usageCount?: number | null;
}

export interface AppointmentDocumentTypeUpdateDto {
  name: string;
  appointmentTypeIds?: string[];
  appliesToAll?: boolean;
  isActive?: boolean;
}

export interface GetAppointmentDocumentTypesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentTypeId?: string | null;
}
