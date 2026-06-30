
export interface CreateInternalUserDto {
  email: string;
  firstName: string;
  lastName: string;
  roleName: string;
  tenantId?: string | null;
  phoneNumber?: string | null;
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
