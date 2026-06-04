import { mapEnumToOptions } from '@abp/ng.core';

export enum ExternalUserType {
  Patient = 1,
  ClaimExaminer = 2,
  ApplicantAttorney = 3,
  DefenseAttorney = 4,
}

export const externalUserTypeOptions = mapEnumToOptions(ExternalUserType);
