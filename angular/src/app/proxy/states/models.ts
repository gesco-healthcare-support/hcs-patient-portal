import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface GetStatesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  name?: string | null;
}

export interface StateCreateDto {
  name: string;
}

export interface StateDto extends FullAuditedEntityDto<string> {
  name?: string;
  concurrencyStamp?: string;
}

export interface StateUpdateDto {
  name: string;
  concurrencyStamp?: string;
}
