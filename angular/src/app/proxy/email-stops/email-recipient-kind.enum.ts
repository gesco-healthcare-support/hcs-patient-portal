import { mapEnumToOptions } from '@abp/ng.core';

export enum EmailRecipientKind {
  Patient = 1,
  ApplicantAttorney = 2,
  ApplicantParalegal = 3,
  DefenseAttorney = 4,
  DefenseParalegal = 5,
  ClaimExaminer = 6,
  Booker = 7,
  AuthorizedUser = 8,
}

export const emailRecipientKindOptions = mapEnumToOptions(EmailRecipientKind);
