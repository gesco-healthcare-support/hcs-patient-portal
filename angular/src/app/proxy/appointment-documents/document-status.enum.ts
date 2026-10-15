import { mapEnumToOptions } from '@abp/ng.core';

export enum DocumentStatus {
  Uploaded = 1,
  Accepted = 2,
  Rejected = 3,
  Pending = 4,
}

export const documentStatusOptions = mapEnumToOptions(DocumentStatus);
