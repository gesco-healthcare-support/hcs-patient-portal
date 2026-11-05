import { mapEnumToOptions } from '@abp/ng.core';

export enum EmailRoutingNote {
  None = 0,
  SendsToParalegalInstead = 1,
  RequestReceiptOnly = 2,
  StartsAfterApproval = 3,
}

export const emailRoutingNoteOptions = mapEnumToOptions(EmailRoutingNote);
