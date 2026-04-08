import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { StateDto } from '../states/models';
import type { IdentityUserDto } from '../volo/abp/identity/models';

export interface ApplicantAttorneyCreateDto {
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

export interface ApplicantAttorneyDto extends FullAuditedEntityDto<string> {
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

export interface ApplicantAttorneyUpdateDto {
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

export interface ApplicantAttorneyWithNavigationPropertiesDto {
  applicantAttorney?: ApplicantAttorneyDto;
  state?: StateDto;
  identityUser: IdentityUserDto;
}

export interface GetApplicantAttorneysInput extends PagedAndSortedResultRequestDto {
  filterText?: string;
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
