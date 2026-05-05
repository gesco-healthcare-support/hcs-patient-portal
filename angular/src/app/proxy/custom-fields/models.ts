import type { CustomFieldType } from '../enums/custom-field-type.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CustomFieldCreateDto {
  fieldLabel: string;
  fieldType: CustomFieldType;
  fieldLength?: number | null;
  multipleValues?: string | null;
  defaultValue?: string | null;
  isMandatory?: boolean;
  appointmentTypeId: string | null;
  isActive?: boolean;
}

export interface CustomFieldDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  fieldLabel?: string;
  displayOrder?: number;
  fieldType?: CustomFieldType;
  fieldLength?: number | null;
  multipleValues?: string | null;
  defaultValue?: string | null;
  isMandatory?: boolean;
  appointmentTypeId?: string | null;
  isActive?: boolean;
}

export interface CustomFieldUpdateDto {
  fieldLabel: string;
  displayOrder?: number;
  fieldType: CustomFieldType;
  fieldLength?: number | null;
  multipleValues?: string | null;
  defaultValue?: string | null;
  isMandatory?: boolean;
  appointmentTypeId: string | null;
  isActive?: boolean;
}

export interface GetCustomFieldsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentTypeId?: string | null;
  isActive?: boolean | null;
}
