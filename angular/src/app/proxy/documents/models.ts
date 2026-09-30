import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface DocumentCreateDto {
  name: string;
  contentType?: string | null;
  isActive?: boolean;
}

export interface DocumentDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  name?: string;
  blobName?: string;
  contentType?: string | null;
  isActive?: boolean;
}

export interface DocumentUpdateDto {
  name: string;
  contentType?: string | null;
  isActive?: boolean;
}

export interface GetDocumentsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  isActive?: boolean | null;
}
