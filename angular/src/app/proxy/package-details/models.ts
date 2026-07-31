import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { DocumentDto } from '../documents/models';

export interface GetPackageDetailsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentTypeId?: string | null;
  isActive?: boolean | null;
}

export interface PackageDetailCreateDto {
  packageName: string;
  appointmentTypeId: string | null;
  isActive?: boolean;
}

export interface PackageDetailDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  packageName?: string;
  appointmentTypeId?: string | null;
  isActive?: boolean;
}

export interface PackageDetailUpdateDto {
  packageName: string;
  appointmentTypeId: string | null;
  isActive?: boolean;
}

export interface PackageDetailWithDocumentsDto {
  package?: PackageDetailDto;
  linkedDocuments?: DocumentDto[];
}
