import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { StateDto } from '../states/models';
import type { IdentityUserDto } from '../volo/abp/identity/models';

export interface DefenseAttorneyCreateDto {
  firmName?: string | null;
  firmAddress?: string | null;
  webAddress?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zipCode?: string | null;
  stateId?: string | null;
  identityUserId?: string;
}

export interface DefenseAttorneyDto extends FullAuditedEntityDto<string> {
  firmName?: string | null;
  firmAddress?: string | null;
  webAddress?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zipCode?: string | null;
  stateId?: string | null;
  identityUserId?: string;
  concurrencyStamp?: string;
}

export interface DefenseAttorneyUpdateDto {
  firmName?: string | null;
  firmAddress?: string | null;
  webAddress?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zipCode?: string | null;
  stateId?: string | null;
  identityUserId?: string;
  concurrencyStamp?: string;
}

export interface DefenseAttorneyWithNavigationPropertiesDto {
  defenseAttorney?: DefenseAttorneyDto;
  state?: StateDto | null;
  identityUser?: IdentityUserDto | null;
}

export interface GetDefenseAttorneysInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  firmName?: string | null;
  phoneNumber?: string | null;
  city?: string | null;
  stateId?: string | null;
  identityUserId?: string | null;
}
