import type {
  GetPatientsInput,
  PatientCreateDto,
  PatientDto,
  PatientUpdateDto,
  PatientWithNavigationPropertiesDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PatientService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: PatientCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PatientDto>(
      {
        method: 'POST',
        url: '/api/app/patients',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/app/patients/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PatientDto>(
      {
        method: 'GET',
        url: `/api/app/patients/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  getAppointmentLanguageLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/patients/appointment-language-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/patients/identity-user-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getList = (input: GetPatientsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PatientWithNavigationPropertiesDto>>(
      {
        method: 'GET',
        url: '/api/app/patients',
        params: {
          filterText: input.filterText,
          firstName: input.firstName,
          lastName: input.lastName,
          middleName: input.middleName,
          email: input.email,
          genderId: input.genderId,
          dateOfBirthMin: input.dateOfBirthMin,
          dateOfBirthMax: input.dateOfBirthMax,
          phoneNumber: input.phoneNumber,
          socialSecurityNumber: input.socialSecurityNumber,
          address: input.address,
          city: input.city,
          zipCode: input.zipCode,
          refferedBy: input.refferedBy,
          cellPhoneNumber: input.cellPhoneNumber,
          street: input.street,
          interpreterVendorName: input.interpreterVendorName,
          apptNumber: input.apptNumber,
          stateId: input.stateId,
          appointmentLanguageId: input.appointmentLanguageId,
          identityUserId: input.identityUserId,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getStateLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/patients/state-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getTenantLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/patients/tenant-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PatientWithNavigationPropertiesDto>(
      {
        method: 'GET',
        url: `/api/app/patients/with-navigation-properties/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  update = (id: string, input: PatientUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PatientDto>(
      {
        method: 'PUT',
        url: `/api/app/patients/${id}`,
        body: input,
      },
      { apiName: this.apiName, ...config },
    );
}
