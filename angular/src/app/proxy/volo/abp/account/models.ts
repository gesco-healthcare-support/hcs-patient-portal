import type { ExtensibleObject } from '@abp/ng.core';

export interface ChangePasswordInput {
  currentPassword?: string;
  newPassword: string;
}

export interface ProfileDto extends ExtensibleObject {
  userName?: string;
  email?: string;
  emailConfirmed?: boolean;
  name?: string;
  surname?: string;
  phoneNumber?: string;
  phoneNumberConfirmed?: boolean;
  isExternal?: boolean;
  hasPassword?: boolean;
  supportsMultipleTimezone?: boolean;
  timezone?: string;
  concurrencyStamp?: string;
}

export interface UpdateProfileDto extends ExtensibleObject {
  userName: string;
  email?: string;
  name?: string;
  surname?: string;
  phoneNumber?: string;
  timezone?: string;
  concurrencyStamp?: string;
}
