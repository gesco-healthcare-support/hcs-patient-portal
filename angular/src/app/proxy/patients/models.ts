import type { Gender } from '../enums/gender.enum';
import type { PhoneNumberType } from '../enums/phone-number-type.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { StateDto } from '../states/models';
import type { AppointmentLanguageDto } from '../appointment-languages/models';
import type { IdentityUserDto } from '../volo/abp/identity/models';
import type { SaasTenantDto } from '../volo/saas/host/dtos/models';

export interface CreatePatientForAppointmentBookingInput {
  firstName: string;
  lastName: string;
  middleName?: string | null;
  email: string;
  genderId?: Gender;
  dateOfBirth?: string;
  phoneNumber?: string | null;
  socialSecurityNumber?: string | null;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  refferedBy?: string | null;
  cellPhoneNumber?: string | null;
  phoneNumberTypeId?: PhoneNumberType;
  street?: string | null;
  interpreterVendorName?: string | null;
  apptNumber?: string | null;
  othersLanguageName?: string | null;
  stateId?: string | null;
  appointmentLanguageId?: string | null;
}

export interface GetPatientsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  middleName?: string | null;
  email?: string | null;
  genderId?: Gender | null;
  dateOfBirthMin?: string | null;
  dateOfBirthMax?: string | null;
  phoneNumber?: string | null;
  socialSecurityNumber?: string | null;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  refferedBy?: string | null;
  cellPhoneNumber?: string | null;
  street?: string | null;
  interpreterVendorName?: string | null;
  apptNumber?: string | null;
  stateId?: string | null;
  appointmentLanguageId?: string | null;
  identityUserId?: string | null;
}

export interface PatientCreateDto {
  firstName: string;
  lastName: string;
  middleName?: string | null;
  email: string;
  genderId?: Gender;
  dateOfBirth?: string;
  phoneNumber?: string | null;
  socialSecurityNumber?: string | null;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  refferedBy?: string | null;
  cellPhoneNumber?: string | null;
  phoneNumberTypeId?: PhoneNumberType;
  street?: string | null;
  interpreterVendorName?: string | null;
  apptNumber?: string | null;
  othersLanguageName?: string | null;
  stateId?: string | null;
  appointmentLanguageId?: string | null;
  identityUserId?: string;
  tenantId?: string | null;
}

export interface PatientDto extends FullAuditedEntityDto<string> {
  firstName?: string;
  lastName?: string;
  middleName?: string | null;
  email?: string;
  genderId?: Gender;
  dateOfBirth?: string;
  phoneNumber?: string | null;
  socialSecurityNumber?: string | null;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  refferedBy?: string | null;
  cellPhoneNumber?: string | null;
  phoneNumberTypeId?: PhoneNumberType;
  street?: string | null;
  interpreterVendorName?: string | null;
  apptNumber?: string | null;
  othersLanguageName?: string | null;
  stateId?: string | null;
  appointmentLanguageId?: string | null;
  identityUserId?: string;
  tenantId?: string | null;
  concurrencyStamp?: string;
}

export interface PatientUpdateDto {
  firstName: string;
  lastName: string;
  middleName?: string | null;
  email: string;
  genderId?: Gender;
  dateOfBirth?: string;
  phoneNumber?: string | null;
  socialSecurityNumber?: string | null;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  refferedBy?: string | null;
  cellPhoneNumber?: string | null;
  phoneNumberTypeId?: PhoneNumberType;
  street?: string | null;
  interpreterVendorName?: string | null;
  apptNumber?: string | null;
  othersLanguageName?: string | null;
  stateId?: string | null;
  appointmentLanguageId?: string | null;
  identityUserId?: string;
  tenantId?: string | null;
  concurrencyStamp?: string;
}

export interface PatientWithNavigationPropertiesDto {
  patient?: PatientDto;
  state?: StateDto | null;
  appointmentLanguage?: AppointmentLanguageDto | null;
  identityUser?: IdentityUserDto | null;
  tenant?: SaasTenantDto | null;
  isExisting?: boolean;
}
