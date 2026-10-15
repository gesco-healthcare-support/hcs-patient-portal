import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

export interface BrandingDto {
  displayName?: string | null;
  hasLogo?: boolean;
  logoUrl?: string | null;
}

export interface GetOfficeBrandingInput extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface OfficeBrandingDto {
  officeId?: string;
  officeName?: string;
  displayName?: string | null;
  hasLogo?: boolean;
  logoUrl?: string | null;
}

export interface OfficeLetterheadDto {
  letterheadName?: string | null;
  letterheadTagline?: string | null;
  physicianName?: string | null;
  practiceName?: string | null;
  mailingStreet?: string | null;
  mailingCity?: string | null;
  mailingState?: string | null;
  mailingZip?: string | null;
  phone?: string | null;
  fax?: string | null;
  recordsDeliveryAddress?: string | null;
  recordsReleaseAddress?: string | null;
  missedAppointmentFee?: number | null;
  defaultPhysicianName?: string;
  defaultLetterheadName?: string;
  defaultPracticeName?: string;
}

export interface SetBrandingDisplayNameInput {
  displayName?: string | null;
}

export interface UpdateOfficeLetterheadInput {
  letterheadName?: string | null;
  letterheadTagline?: string | null;
  physicianName?: string | null;
  practiceName?: string | null;
  mailingStreet?: string | null;
  mailingCity?: string | null;
  mailingState?: string | null;
  mailingZip?: string | null;
  phone?: string | null;
  fax?: string | null;
  recordsDeliveryAddress?: string | null;
  recordsReleaseAddress?: string | null;
  missedAppointmentFee?: number | null;
}

export interface UploadBrandingLogoForm {
  file?: IFormFile;
}
