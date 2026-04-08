import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { StateDto } from '../states/models';

export interface GetWcabOfficesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  name?: string | null;
  abbreviation?: string | null;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  isActive?: boolean | null;
  stateId?: string | null;
}

export interface WcabOfficeCreateDto {
  name: string;
  abbreviation: string;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  isActive?: boolean;
  stateId?: string | null;
}

export interface WcabOfficeDto extends FullAuditedEntityDto<string> {
  name?: string;
  abbreviation?: string;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  isActive?: boolean;
  stateId?: string | null;
  concurrencyStamp?: string;
}

export interface WcabOfficeExcelDownloadDto {
  downloadToken?: string;
  filterText?: string | null;
  name?: string | null;
  abbreviation?: string | null;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  isActive?: boolean | null;
  stateId?: string | null;
}

export interface WcabOfficeUpdateDto {
  name: string;
  abbreviation: string;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  isActive?: boolean;
  stateId?: string | null;
  concurrencyStamp?: string;
}

export interface WcabOfficeWithNavigationPropertiesDto {
  wcabOffice?: WcabOfficeDto;
  state?: StateDto | null;
}
