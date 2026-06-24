import type { EntityDto } from '@abp/ng.core';

export interface DoctorPreferredLocationDto extends EntityDto {
  tenantId?: string | null;
  doctorId?: string;
  locationId?: string;
  isActive?: boolean;
}

export interface ToggleDoctorPreferredLocationInput {
  doctorId: string;
  locationId: string;
  isActive?: boolean;
}
