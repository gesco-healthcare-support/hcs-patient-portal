import type { DoctorPreferredLocationDto, ToggleDoctorPreferredLocationInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DoctorPreferredLocationsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getByDoctor = (doctorId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorPreferredLocationDto[]>({
      method: 'GET',
      url: `/api/app/doctor-preferred-locations/by-doctor/${doctorId}`,
    },
    { apiName: this.apiName,...config });
  

  toggle = (input: ToggleDoctorPreferredLocationInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorPreferredLocationDto>({
      method: 'POST',
      url: '/api/app/doctor-preferred-locations/toggle',
      body: input,
    },
    { apiName: this.apiName,...config });
}