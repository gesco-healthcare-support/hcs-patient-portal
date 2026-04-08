import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentLanguageCreateDto {
  name: string;
}

export interface AppointmentLanguageDto extends FullAuditedEntityDto<string> {
  name?: string;
}

export interface AppointmentLanguageUpdateDto {
  name: string;
}

export interface GetAppointmentLanguagesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
}
