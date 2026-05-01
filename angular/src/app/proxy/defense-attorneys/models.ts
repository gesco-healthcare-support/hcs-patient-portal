import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { StateDto } from '../states/models';
import type { IdentityUserDto } from '../volo/abp/identity/models';

export interface DefenseAttorneyCreateDto {
  firmName?: string;
  firmAddress?: string;
  webAddress?: string;
  phoneNumber?: string;
  faxNumber?: string;
  street?: string;
  city?: string;
  zipCode?: string;
  stateId?: string;
  identityUserId: string;
}

export interface DefenseAttorneyDto extends FullAuditedEntityDto<string> {
  firmName?: string;
  firmAddress?: string;
  webAddress?: string;
  phoneNumber?: string;
  faxNumber?: string;
  street?: string;
  city?: string;
  zipCode?: string;
  stateId?: string;
  identityUserId: string;
  concurrencyStamp?: string;
}

export interface DefenseAttorneyUpdateDto {
  firmName?: string;
  firmAddress?: string;
  webAddress?: string;
  phoneNumber?: string;
  faxNumber?: string;
  street?: string;
  city?: string;
  zipCode?: string;
  stateId?: string;
  identityUserId: string;
  concurrencyStamp?: string;
}

export interface DefenseAttorneyWithNavigationPropertiesDto {
  defenseAttorney?: DefenseAttorneyDto;
  state?: StateDto;
  identityUser: IdentityUserDto;
}

export interface GetDefenseAttorneysInput extends PagedAndSortedResultRequestDto {
  filterText?: string;
  firmName?: string;
  phoneNumber?: string;
  city?: string;
  stateId?: string;
  identityUserId?: string;
}
