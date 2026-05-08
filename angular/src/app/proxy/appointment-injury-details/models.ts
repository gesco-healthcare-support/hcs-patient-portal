import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AppointmentDto } from '../appointments/models';
import type { WcabOfficeDto } from '../wcab-offices/models';
import type { AppointmentBodyPartDto } from '../appointment-body-parts/models';
import type { AppointmentClaimExaminerDto } from '../appointment-claim-examiners/models';
import type { AppointmentPrimaryInsuranceDto } from '../appointment-primary-insurances/models';

export interface AppointmentInjuryDetailCreateDto {
  appointmentId?: string;
  dateOfInjury: string;
  toDateOfInjury?: string | null;
  claimNumber: string;
  isCumulativeInjury: boolean;
  wcabAdj?: string | null;
  bodyPartsSummary: string;
  wcabOfficeId?: string | null;
}

export interface AppointmentInjuryDetailDto extends FullAuditedEntityDto<string> {
  appointmentId?: string;
  dateOfInjury?: string;
  toDateOfInjury?: string | null;
  claimNumber?: string;
  isCumulativeInjury?: boolean;
  wcabAdj?: string | null;
  bodyPartsSummary?: string;
  wcabOfficeId?: string | null;
  concurrencyStamp?: string;
}

export interface AppointmentInjuryDetailUpdateDto {
  appointmentId?: string;
  dateOfInjury: string;
  toDateOfInjury?: string | null;
  claimNumber: string;
  isCumulativeInjury: boolean;
  wcabAdj?: string | null;
  bodyPartsSummary: string;
  wcabOfficeId?: string | null;
  concurrencyStamp?: string;
}

export interface AppointmentInjuryDetailWithNavigationPropertiesDto {
  appointmentInjuryDetail?: AppointmentInjuryDetailDto;
  appointment?: AppointmentDto | null;
  wcabOffice?: WcabOfficeDto | null;
  bodyParts?: AppointmentBodyPartDto[];
  claimExaminer?: AppointmentClaimExaminerDto | null;
  primaryInsurance?: AppointmentPrimaryInsuranceDto | null;
}

export interface GetAppointmentInjuryDetailsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentId?: string | null;
  claimNumber?: string | null;
}
