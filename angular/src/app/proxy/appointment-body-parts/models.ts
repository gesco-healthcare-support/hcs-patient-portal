import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentBodyPartCreateDto {
  appointmentInjuryDetailId?: string;
  bodyPartDescription: string;
}

export interface AppointmentBodyPartDto extends FullAuditedEntityDto<string> {
  appointmentInjuryDetailId?: string;
  bodyPartDescription?: string;
}

export interface AppointmentBodyPartUpdateDto {
  appointmentInjuryDetailId?: string;
  bodyPartDescription: string;
}

export interface GetAppointmentBodyPartsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentInjuryDetailId?: string | null;
}
