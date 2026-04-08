import type {
  ApplicantAttorneyCreateDto,
  ApplicantAttorneyDto,
  ApplicantAttorneyUpdateDto,
  ApplicantAttorneyWithNavigationPropertiesDto,
  GetApplicantAttorneysInput,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type {
  AppFileDescriptorDto,
  DownloadTokenResultDto,
  GetFileInput,
  LookupDto,
  LookupRequestDto,
} from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ApplicantAttorneyService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: ApplicantAttorneyCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApplicantAttorneyDto>(
      {
        method: 'POST',
        url: '/api/app/applicant-attorneys',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/app/applicant-attorneys/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApplicantAttorneyDto>(
      {
        method: 'GET',
        url: `/api/app/applicant-attorneys/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  getDownloadToken = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DownloadTokenResultDto>(
      {
        method: 'GET',
        url: '/api/app/applicant-attorneys/download-token',
      },
      { apiName: this.apiName, ...config },
    );

  getFile = (input: GetFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>(
      {
        method: 'GET',
        responseType: 'blob',
        url: '/api/app/applicant-attorneys/file',
        params: { downloadToken: input.downloadToken, fileId: input.fileId },
      },
      { apiName: this.apiName, ...config },
    );

  getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/applicant-attorneys/identity-user-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getList = (input: GetApplicantAttorneysInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ApplicantAttorneyWithNavigationPropertiesDto>>(
      {
        method: 'GET',
        url: '/api/app/applicant-attorneys',
        params: {
          filterText: input.filterText,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
          firmName: input.firmName,
          firmAddress: input.firmAddress,
          webAddress: input.webAddress,
          phoneNumber: input.phoneNumber,
          faxNumber: input.faxNumber,
          street: input.street,
          city: input.city,
          zipCode: input.zipCode,
          stateId: input.stateId,
          identityUserId: input.identityUserId,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getStateLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/applicant-attorneys/state-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApplicantAttorneyWithNavigationPropertiesDto>(
      {
        method: 'GET',
        url: `/api/app/applicant-attorneys/with-navigation-properties/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  update = (id: string, input: ApplicantAttorneyUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApplicantAttorneyDto>(
      {
        method: 'PUT',
        url: `/api/app/applicant-attorneys/${id}`,
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  uploadFile = (input: FormData, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppFileDescriptorDto>(
      {
        method: 'POST',
        url: '/api/app/applicant-attorneys/upload-file',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );
}
