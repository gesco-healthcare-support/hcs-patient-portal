import { mapEnumToOptions } from '@abp/ng.core';

export enum InvitationStatus {
  Pending = 0,
  Accepted = 1,
  Expired = 2,
  Revoked = 3,
}

export const invitationStatusOptions = mapEnumToOptions(InvitationStatus);
