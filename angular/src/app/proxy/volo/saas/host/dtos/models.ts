import type {
  ExtensibleEntityDto,
  ExtensibleObject,
  PagedAndSortedResultRequestDto,
} from '@abp/ng.core';
import type { TenantActivationState } from '../../tenant-activation-state.enum';

export interface SaasTenantDto extends ExtensibleEntityDto<string> {
  name?: string;
  editionId?: string | null;
  editionEndDateUtc?: string | null;
  editionName?: string;
  hasDefaultConnectionString?: boolean;
  activationState?: TenantActivationState;
  activationEndDate?: string | null;
  concurrencyStamp?: string;
}

export interface EditionLookupDto extends ExtensibleEntityDto<string> {
  displayName?: string;
}

export interface GetTenantsInput extends PagedAndSortedResultRequestDto {
  filter?: string;
  getEditionNames?: boolean;
  editionId?: string | null;
  expirationDateMin?: string | null;
  expirationDateMax?: string | null;
  activationState?: TenantActivationState | null;
  activationEndDateMin?: string | null;
  activationEndDateMax?: string | null;
}

export interface SaasTenantConnectionStringsDto extends ExtensibleEntityDto {
  default?: string;
  databases?: SaasTenantDatabaseConnectionStringsDto[];
}

export interface SaasTenantCreateDto extends SaasTenantCreateOrUpdateDtoBase {
  adminEmailAddress: string;
  adminPassword: string;
  connectionStrings?: SaasTenantConnectionStringsDto;
}

export interface SaasTenantCreateOrUpdateDtoBase extends ExtensibleObject {
  name: string;
  editionId?: string | null;
  activationState?: TenantActivationState;
  activationEndDate?: string | null;
  editionEndDateUtc?: string | null;
}

export interface SaasTenantDatabaseConnectionStringsDto extends ExtensibleEntityDto {
  databaseName?: string;
  connectionString?: string;
}

export interface SaasTenantDatabasesDto extends ExtensibleEntityDto {
  databases?: string[];
}

export interface SaasTenantSetPasswordDto {
  username?: string;
  password?: string;
}

export interface SaasTenantUpdateDto extends SaasTenantCreateOrUpdateDtoBase {
  concurrencyStamp?: string;
}
