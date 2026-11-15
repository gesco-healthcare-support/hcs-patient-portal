import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';
import type {
  ExternalLoginDto,
  ExternalLoginPage,
  ExternalLoginSetActiveDto,
  GetExternalLoginsInput,
} from './external-logins.models';

/**
 * Hand-written client for `api/app/external-logins` (the proxy folder is generated and never edited).
 * The next `abp generate-proxy` adds a generated twin; switch to it then and delete this file.
 */
@Injectable({ providedIn: 'root' })
export class ExternalLoginsService {
  private readonly rest = inject(RestService);

  getList(input: GetExternalLoginsInput): Observable<ExternalLoginPage> {
    return this.rest.request<void, ExternalLoginPage>(
      {
        method: 'GET',
        url: '/api/app/external-logins',
        params: {
          filterText: input.filterText || undefined,
          isActive: input.isActive ?? undefined,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: 'Default' },
    );
  }

  setActive(id: string, body: ExternalLoginSetActiveDto): Observable<ExternalLoginDto> {
    return this.rest.request<ExternalLoginSetActiveDto, ExternalLoginDto>(
      { method: 'PUT', url: `/api/app/external-logins/${id}/active`, body },
      { apiName: 'Default' },
    );
  }
}
