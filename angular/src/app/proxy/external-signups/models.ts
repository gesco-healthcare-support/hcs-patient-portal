import type { ExternalUserType } from './external-user-type.enum';
import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { InvitationStatus } from '../invitations/invitation-status.enum';

export interface DeleteTestUsersDto {
  emails?: string[];
}

export interface DeleteTestUsersResultDto {
  deleted?: string[];
  notFound?: string[];
}

export interface ExternalUserLookupDto {
  identityUserId?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  userRole?: string;
  firmName?: string;
}

export interface ExternalUserProfileDto {
  identityUserId?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  userRole?: string;
  isExternalUser?: boolean;
  isAccessor?: boolean;
  firmName?: string;
}

export interface ExternalUserSignUpDto {
  userType: ExternalUserType;
  firstName?: string | null;
  lastName?: string | null;
  email: string;
  password: string;
  confirmPassword: string;
  firmName?: string | null;
  firmEmail?: string | null;
  tenantId?: string | null;
  inviteToken?: string | null;
}

export interface GetInvitesInput extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface InvitationDto extends EntityDto<string> {
  email?: string;
  userType?: ExternalUserType;
  roleName?: string;
  firstName?: string | null;
  lastName?: string | null;
  firmName?: string | null;
  invitedByUserId?: string;
  invitedByName?: string | null;
  creationTime?: string;
  expiresAt?: string;
  acceptedAt?: string | null;
  status?: InvitationStatus;
}

export interface InvitationValidationDto {
  email?: string;
  userType?: ExternalUserType;
  roleName?: string;
  tenantName?: string;
  expiresAt?: string;
  firstName?: string | null;
  lastName?: string | null;
  firmName?: string | null;
}

export interface InviteExternalUserDto {
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  firmName?: string | null;
  userType: ExternalUserType;
}

export interface InviteExternalUserResultDto {
  inviteUrl?: string;
  email?: string;
  roleName?: string;
  tenantName?: string;
  expiresAt?: string;
}

export interface MarkEmailConfirmedDto {
  email?: string;
}
