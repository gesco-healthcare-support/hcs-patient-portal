import type { BookingStatus } from '../enums/booking-status.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { LocationDto } from '../locations/models';
import type { AppointmentTypeDto } from '../appointment-types/models';

export interface DoctorAvailabilityBulkDeleteResultDto {
  deletedCount?: number;
  skippedSlotIds?: string[];
}

export interface DoctorAvailabilityCreateDto {
  availableDate?: string;
  fromTime?: string;
  toTime?: string;
  bookingStatusId?: BookingStatus;
  locationId?: string;
  appointmentTypeIds?: string[];
  capacity?: number;
}

export interface DoctorAvailabilityDeleteByDateInputDto {
  locationId?: string;
  availableDate?: string;
}

export interface DoctorAvailabilityDeleteBySlotInputDto {
  locationId?: string;
  availableDate?: string;
  fromTime?: string;
  toTime?: string;
}

export interface DoctorAvailabilityDto extends FullAuditedEntityDto<string> {
  availableDate?: string;
  fromTime?: string;
  toTime?: string;
  bookingStatusId?: BookingStatus;
  locationId?: string;
  appointmentTypeIds?: string[];
  capacity?: number;
  concurrencyStamp?: string;
}

export interface DoctorAvailabilityGenerateInputDto {
  fromDate?: string;
  toDate?: string;
  fromTime?: string;
  toTime?: string;
  bookingStatusId?: BookingStatus;
  locationId?: string;
  appointmentTypeIds?: string[];
  capacity?: number;
  appointmentDurationMinutes?: number;
}

export interface DoctorAvailabilitySlotPreviewDto {
  availableDate?: string;
  fromTime?: string;
  toTime?: string;
  bookingStatusId?: BookingStatus;
  locationId?: string;
  appointmentTypeIds?: string[];
  capacity?: number;
  timeId?: number;
  isConflict?: boolean;
}

export interface DoctorAvailabilitySlotsPreviewDto {
  dates?: string;
  days?: string;
  monthId?: number;
  locationName?: string | null;
  time?: string;
  sameTimeValidation?: string | null;
  doctorAvailabilities?: DoctorAvailabilitySlotPreviewDto[];
}

export interface DoctorAvailabilityUpdateDto {
  availableDate?: string;
  fromTime?: string;
  toTime?: string;
  bookingStatusId?: BookingStatus;
  locationId?: string;
  appointmentTypeIds?: string[];
  capacity?: number;
  concurrencyStamp?: string;
}

export interface DoctorAvailabilityWithNavigationPropertiesDto {
  doctorAvailability?: DoctorAvailabilityDto;
  location?: LocationDto | null;
  appointmentTypes?: AppointmentTypeDto[];
}

export interface GetDoctorAvailabilitiesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  availableDateMin?: string | null;
  availableDateMax?: string | null;
  fromTimeMin?: string | null;
  fromTimeMax?: string | null;
  toTimeMin?: string | null;
  toTimeMax?: string | null;
  bookingStatusId?: BookingStatus | null;
  locationId?: string | null;
}

export interface GetDoctorAvailabilityLookupInput {
  locationId?: string;
  appointmentTypeId?: string | null;
  availableDateFrom?: string | null;
  availableDateTo?: string | null;
}
