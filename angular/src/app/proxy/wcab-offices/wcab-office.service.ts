import type {
  GetWcabOfficesInput,
  WcabOfficeCreateDto,
  WcabOfficeDto,
  WcabOfficeExcelDownloadDto,
  WcabOfficeUpdateDto,
  WcabOfficeWithNavigationPropertiesDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { DownloadTokenResultDto, LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class WcabOfficeService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: WcabOfficeCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WcabOfficeDto>(
      {
        method: 'POST',
        url: '/api/app/wcab-offices',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/app/wcab-offices/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  deleteAll = (input: GetWcabOfficesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: '/api/app/wcab-offices/all',
        params: {
          filterText: input.filterText,
          name: input.name,
          abbreviation: input.abbreviation,
          address: input.address,
          city: input.city,
          zipCode: input.zipCode,
          isActive: input.isActive,
          stateId: input.stateId,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  deleteByIds = (wcabofficeIds: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: '/api/app/wcab-offices',
        params: { wcabofficeIds },
      },
      { apiName: this.apiName, ...config },
    );

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WcabOfficeDto>(
      {
        method: 'GET',
        url: `/api/app/wcab-offices/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  getDownloadToken = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DownloadTokenResultDto>(
      {
        method: 'GET',
        url: '/api/app/wcab-offices/download-token',
      },
      { apiName: this.apiName, ...config },
    );

  getList = (input: GetWcabOfficesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WcabOfficeWithNavigationPropertiesDto>>(
      {
        method: 'GET',
        url: '/api/app/wcab-offices',
        params: {
          filterText: input.filterText,
          name: input.name,
          abbreviation: input.abbreviation,
          address: input.address,
          city: input.city,
          zipCode: input.zipCode,
          isActive: input.isActive,
          stateId: input.stateId,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getListAsExcelFile = (input: WcabOfficeExcelDownloadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>(
      {
        method: 'GET',
        responseType: 'blob',
        url: '/api/app/wcab-offices/as-excel-file',
        params: {
          downloadToken: input.downloadToken,
          filterText: input.filterText,
          name: input.name,
          abbreviation: input.abbreviation,
          address: input.address,
          city: input.city,
          zipCode: input.zipCode,
          isActive: input.isActive,
          stateId: input.stateId,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getStateLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/wcab-offices/state-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WcabOfficeWithNavigationPropertiesDto>(
      {
        method: 'GET',
        url: `/api/app/wcab-offices/with-navigation-properties/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  update = (id: string, input: WcabOfficeUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WcabOfficeDto>(
      {
        method: 'PUT',
        url: `/api/app/wcab-offices/${id}`,
        body: input,
      },
      { apiName: this.apiName, ...config },
    );
}
