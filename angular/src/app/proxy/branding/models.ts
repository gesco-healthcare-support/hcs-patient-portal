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

export interface SetBrandingDisplayNameInput {
  displayName?: string | null;
}

export interface UploadBrandingLogoForm {
  file?: IFormFile;
}
