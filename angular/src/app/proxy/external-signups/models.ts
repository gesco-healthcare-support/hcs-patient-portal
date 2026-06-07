import type { ExternalUserType } from './external-user-type.enum';

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
}

export interface ExternalUserProfileDto {
  identityUserId?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  userRole?: string;
  isExternalUser?: boolean;
  isAccessor?: boolean;
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

export interface InvitationValidationDto {
  email?: string;
  userType?: ExternalUserType;
  roleName?: string;
  tenantName?: string;
  expiresAt?: string;
  firstName?: string | null;
  lastName?: string | null;
}

export interface InviteExternalUserDto {
  email: string;
  firstName?: string | null;
  lastName?: string | null;
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
