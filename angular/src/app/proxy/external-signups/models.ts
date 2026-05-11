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
}

export interface InviteExternalUserDto {
  email: string;
  userType: ExternalUserType;
}

export interface InviteExternalUserResultDto {
  inviteUrl?: string;
  emailEnqueued?: boolean;
  email?: string;
  roleName?: string;
  tenantName?: string;
}

export interface MarkEmailConfirmedDto {
  email?: string;
}
