import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface AppointmentTypeFieldConfigBatchItemDto {
  fieldName?: string;
  hidden?: boolean;
  readOnly?: boolean;
  required?: boolean;
  defaultValue?: string | null;
}

export interface AppointmentTypeFieldConfigCreateDto {
  appointmentTypeId?: string;
  fieldName?: string;
  hidden?: boolean;
  readOnly?: boolean;
  required?: boolean;
  defaultValue?: string | null;
}

export interface AppointmentTypeFieldConfigDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  appointmentTypeId?: string;
  fieldName?: string;
  hidden?: boolean;
  readOnly?: boolean;
  required?: boolean;
  defaultValue?: string | null;
  concurrencyStamp?: string | null;
}

export interface AppointmentTypeFieldConfigUpdateDto {
  hidden?: boolean;
  readOnly?: boolean;
  required?: boolean;
  defaultValue?: string | null;
  concurrencyStamp?: string | null;
}
