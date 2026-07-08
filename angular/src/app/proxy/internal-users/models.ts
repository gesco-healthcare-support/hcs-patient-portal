import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CreateInternalUserDto {
  email: string;
  firstName: string;
  lastName: string;
  roleName: string;
  tenantId?: string | null;
  phoneNumber?: string | null;
}

export interface GetInternalUsersInput extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface InternalUserCreatedDto {
  userId?: string;
  email?: string;
  firstName?: string;
  lastName?: string;
  roleName?: string;
  tenantName?: string;
  welcomeEmailQueued?: boolean;
}

export interface InternalUserListDto {
  id?: string;
  fullName?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  role?: string;
  isActive?: boolean;
}
