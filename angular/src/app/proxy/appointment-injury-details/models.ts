import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentInjuryDetailDto extends FullAuditedEntityDto<string> {
  appointmentId: string;
  dateOfInjury: string;
  toDateOfInjury?: string;
  claimNumber: string;
  isCumulativeInjury: boolean;
  wcabAdj?: string;
  bodyPartsSummary: string;
  wcabOfficeId?: string;
  concurrencyStamp?: string;
}

export interface AppointmentInjuryDetailCreateDto {
  appointmentId: string;
  dateOfInjury: string;
  toDateOfInjury?: string;
  claimNumber: string;
  isCumulativeInjury: boolean;
  wcabAdj?: string;
  bodyPartsSummary: string;
  wcabOfficeId?: string;
}

export interface AppointmentInjuryDetailUpdateDto extends AppointmentInjuryDetailCreateDto {
  concurrencyStamp?: string;
}

export interface AppointmentBodyPartDto extends FullAuditedEntityDto<string> {
  appointmentInjuryDetailId: string;
  bodyPartDescription: string;
}

export interface AppointmentClaimExaminerDto extends FullAuditedEntityDto<string> {
  appointmentInjuryDetailId: string;
  name?: string;
  claimExaminerNumber?: string;
  email?: string;
  phoneNumber?: string;
  fax?: string;
  street?: string;
  city?: string;
  zip?: string;
  stateId?: string;
  isActive: boolean;
  concurrencyStamp?: string;
}

export interface AppointmentClaimExaminerCreateDto {
  appointmentInjuryDetailId: string;
  name?: string;
  claimExaminerNumber?: string;
  email?: string;
  phoneNumber?: string;
  fax?: string;
  street?: string;
  city?: string;
  zip?: string;
  stateId?: string;
  isActive: boolean;
}

export interface AppointmentPrimaryInsuranceDto extends FullAuditedEntityDto<string> {
  appointmentInjuryDetailId: string;
  name?: string;
  insuranceNumber?: string;
  attention?: string;
  phoneNumber?: string;
  faxNumber?: string;
  street?: string;
  city?: string;
  zip?: string;
  stateId?: string;
  isActive: boolean;
  concurrencyStamp?: string;
}

export interface AppointmentPrimaryInsuranceCreateDto {
  appointmentInjuryDetailId: string;
  name?: string;
  insuranceNumber?: string;
  attention?: string;
  phoneNumber?: string;
  faxNumber?: string;
  street?: string;
  city?: string;
  zip?: string;
  stateId?: string;
  isActive: boolean;
}

export interface AppointmentInjuryDetailWithNavigationPropertiesDto {
  appointmentInjuryDetail: AppointmentInjuryDetailDto;
  appointment?: unknown;
  wcabOffice?: { id: string; name?: string };
  bodyParts: AppointmentBodyPartDto[];
  claimExaminer?: AppointmentClaimExaminerDto;
  primaryInsurance?: AppointmentPrimaryInsuranceDto;
}

/** Booker form draft -- one row in the Claim Information table-of-injuries.
 *  Multi-injury support per OLD (each row carries its own examiner + insurance). */
export interface AppointmentInjuryDraft {
  injuryDetailId?: string;
  injuryDetailConcurrencyStamp?: string;
  appointmentId?: string;
  isCumulativeInjury: boolean;
  dateOfInjury: string | null;
  toDateOfInjury: string | null;
  claimNumber: string;
  wcabOfficeId: string | null;
  wcabAdj: string | null;
  bodyPartsSummary: string;
  primaryInsurance: {
    id?: string;
    concurrencyStamp?: string;
    isActive: boolean;
    name: string | null;
    insuranceNumber: string | null;
    attention: string | null;
    phoneNumber: string | null;
    faxNumber: string | null;
    street: string | null;
    city: string | null;
    stateId: string | null;
    zip: string | null;
  };
  claimExaminer: {
    id?: string;
    concurrencyStamp?: string;
    isActive: boolean;
    name: string | null;
    email: string | null;
    phoneNumber: string | null;
    fax: string | null;
    street: string | null;
    claimExaminerNumber: string | null;
    city: string | null;
    stateId: string | null;
    zip: string | null;
  };
}
