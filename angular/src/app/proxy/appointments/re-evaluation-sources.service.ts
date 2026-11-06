import type { ReEvaluationSourceDto, ReEvaluationSourceLookupDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ReEvaluationSourcesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  lookup = (input: ReEvaluationSourceLookupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReEvaluationSourceDto>({
      method: 'POST',
      url: '/api/app/re-evaluation-sources/lookup',
      body: input,
    },
    { apiName: this.apiName,...config });
}