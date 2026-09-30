import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { StateDto } from '../states/models';
import type { IdentityUserDto } from '../volo/abp/identity/models';

export interface ClaimExaminerCreateDto {
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zipCode?: string | null;
  stateId?: string | null;
  identityUserId?: string | null;
}

export interface ClaimExaminerDto extends FullAuditedEntityDto<string> {
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zipCode?: string | null;
  stateId?: string | null;
  identityUserId?: string | null;
  concurrencyStamp?: string;
}

export interface ClaimExaminerUpdateDto {
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zipCode?: string | null;
  stateId?: string | null;
  identityUserId?: string | null;
  concurrencyStamp?: string;
}

export interface ClaimExaminerWithNavigationPropertiesDto {
  claimExaminer?: ClaimExaminerDto;
  state?: StateDto | null;
  identityUser?: IdentityUserDto | null;
}

export interface GetClaimExaminersInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  city?: string | null;
  stateId?: string | null;
  identityUserId?: string | null;
}
