import type {
  AppointmentLanguageCreateDto,
  AppointmentLanguageDto,
  AppointmentLanguageUpdateDto,
  GetAppointmentLanguagesInput,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentLanguageService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: AppointmentLanguageCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentLanguageDto>(
      {
        method: 'POST',
        url: '/api/app/appointment-languages',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/app/appointment-languages/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentLanguageDto>(
      {
        method: 'GET',
        url: `/api/app/appointment-languages/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  getList = (input: GetAppointmentLanguagesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentLanguageDto>>(
      {
        method: 'GET',
        url: '/api/app/appointment-languages',
        params: {
          filterText: input.filterText,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  update = (id: string, input: AppointmentLanguageUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentLanguageDto>(
      {
        method: 'PUT',
        url: `/api/app/appointment-languages/${id}`,
        body: input,
      },
      { apiName: this.apiName, ...config },
    );
}
