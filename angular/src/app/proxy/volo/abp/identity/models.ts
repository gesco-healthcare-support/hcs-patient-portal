import type { CreationAuditedEntityDto, EntityDto, ExtensibleEntityDto, ExtensibleFullAuditedEntityDto, ExtensibleObject, ExtensiblePagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { IdentityClaimValueType } from './identity-claim-value-type.enum';
import type { ImportUsersFromFileType } from './import-users-from-file-type.enum';
import type { IRemoteStreamContent } from '../content/models';

export interface IdentityUserDto extends ExtensibleFullAuditedEntityDto<string> {
  tenantId?: string | null;
  userName?: string;
  email?: string;
  name?: string;
  surname?: string;
  emailConfirmed?: boolean;
  phoneNumber?: string;
  phoneNumberConfirmed?: boolean;
  supportTwoFactor?: boolean;
  twoFactorEnabled?: boolean;
  isActive?: boolean;
  lockoutEnabled?: boolean;
  isLockedOut?: boolean;
  lockoutEnd?: string | null;
  shouldChangePasswordOnNextLogin?: boolean;
  concurrencyStamp?: string;
  roleNames?: string[];
  accessFailedCount?: number;
  lastPasswordChangeTime?: string | null;
  isExternal?: boolean;
}

export interface ClaimTypeDto extends ExtensibleEntityDto<string> {
  name?: string;
  required?: boolean;
  isStatic?: boolean;
  regex?: string;
  regexDescription?: string;
  description?: string;
  valueType?: IdentityClaimValueType;
  valueTypeAsString?: string;
  concurrencyStamp?: string;
  creationTime?: string;
}

export interface DownloadTokenResultDto {
  token?: string;
}

export interface ExternalLoginProviderDto {
  name?: string;
  canObtainUserInfoWithoutPassword?: boolean;
}

export interface GetIdentityUserListAsFileInput extends GetIdentityUsersInput {
  token: string;
}

export interface GetIdentityUsersInput extends ExtensiblePagedAndSortedResultRequestDto {
  filter?: string;
  roleId?: string | null;
  organizationUnitId?: string | null;
  id?: string | null;
  userName?: string;
  phoneNumber?: string;
  emailAddress?: string;
  name?: string;
  surname?: string;
  isLockedOut?: boolean | null;
  notActive?: boolean | null;
  emailConfirmed?: boolean | null;
  isExternal?: boolean | null;
  maxCreationTime?: string | null;
  minCreationTime?: string | null;
  maxModifitionTime?: string | null;
  minModifitionTime?: string | null;
}

export interface GetImportInvalidUsersFileInput {
  token: string;
}

export interface GetImportUsersSampleFileInput {
  fileType?: ImportUsersFromFileType;
  token: string;
}

export interface IdentityRoleDto extends ExtensibleEntityDto<string> {
  name?: string;
  isDefault?: boolean;
  isStatic?: boolean;
  isPublic?: boolean;
  userCount?: number;
  concurrencyStamp?: string;
  creationTime?: string;
}

export interface IdentityRoleLookupDto extends EntityDto<string> {
  name?: string;
}

export interface IdentityUserClaimDto {
  userId?: string;
  claimType?: string;
  claimValue?: string;
}

export interface IdentityUserCreateDto extends IdentityUserCreateOrUpdateDtoBase {
  password: string;
  sendConfirmationEmail?: boolean;
  emailConfirmed?: boolean;
  phoneNumberConfirmed?: boolean;
}

export interface IdentityUserCreateOrUpdateDtoBase extends ExtensibleObject {
  userName: string;
  name?: string;
  surname?: string;
  email: string;
  phoneNumber?: string;
  isActive?: boolean;
  shouldChangePasswordOnNextLogin?: boolean;
  lockoutEnabled?: boolean;
  roleNames?: string[];
  organizationUnitIds?: string[];
}

export interface IdentityUserUpdateDto extends IdentityUserCreateOrUpdateDtoBase {
  emailConfirmed?: boolean;
  phoneNumberConfirmed?: boolean;
  concurrencyStamp?: string;
}

export interface IdentityUserUpdatePasswordInput {
  newPassword: string;
}

export interface IdentityUserUpdateRolesDto {
  roleNames: string[];
}

export interface ImportExternalUserInput {
  provider: string;
  userNameOrEmailAddress: string;
  password?: string;
}

export interface ImportUsersFromFileInputWithStream {
  file?: IRemoteStreamContent;
  fileType?: ImportUsersFromFileType;
}

export interface ImportUsersFromFileOutput {
  allCount?: number;
  succeededCount?: number;
  failedCount?: number;
  invalidUsersDownloadToken?: string;
  isAllSucceeded?: boolean;
}

export interface OrganizationUnitDto extends ExtensibleFullAuditedEntityDto<string> {
  parentId?: string | null;
  code?: string;
  displayName?: string;
  roles?: OrganizationUnitRoleDto[];
}

export interface OrganizationUnitLookupDto extends EntityDto<string> {
  displayName?: string;
}

export interface OrganizationUnitRoleDto extends CreationAuditedEntityDto {
  organizationUnitId?: string;
  roleId?: string;
}

export interface OrganizationUnitWithDetailsDto extends ExtensibleFullAuditedEntityDto<string> {
  parentId?: string | null;
  code?: string;
  displayName?: string;
  roles?: IdentityRoleDto[];
  userCount?: number;
  concurrencyStamp?: string;
}
