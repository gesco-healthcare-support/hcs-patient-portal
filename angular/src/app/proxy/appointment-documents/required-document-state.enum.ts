import { mapEnumToOptions } from '@abp/ng.core';

export enum RequiredDocumentState {
  NotUploaded = 0,
  AwaitingReview = 1,
  Rejected = 2,
}

export const requiredDocumentStateOptions = mapEnumToOptions(RequiredDocumentState);
