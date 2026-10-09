import { inject, Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';

/** Editable packet-letterhead fields (mirrors the server UpdateOfficeLetterheadInput). */
export interface OfficeLetterheadFields {
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

/** The stored letterhead plus what a blank field falls back to (mirrors OfficeLetterheadDto). */
export interface OfficeLetterheadDto extends OfficeLetterheadFields {
  defaultPhysicianName: string;
  defaultLetterheadName: string;
  defaultPracticeName: string;
}

/**
 * 2026-10-09 (walkthrough Q5): the current office's packet letterhead -- what generated
 * packets print as the office's identity. Hand-written rather than generated because the
 * endpoint is new and the generated proxy is only regenerated against a running API; it
 * follows the same RestService pattern as shared/branding/branding.service.ts.
 */
@Injectable({ providedIn: 'root' })
export class OfficeLetterheadService {
  private readonly rest = inject(RestService);

  get(): Observable<OfficeLetterheadDto> {
    return this.rest.request<void, OfficeLetterheadDto>(
      { method: 'GET', url: '/api/app/branding/letterhead' },
      { apiName: 'Default' },
    );
  }

  update(input: OfficeLetterheadFields): Observable<OfficeLetterheadDto> {
    return this.rest.request<OfficeLetterheadFields, OfficeLetterheadDto>(
      { method: 'PUT', url: '/api/app/branding/letterhead', body: input },
      { apiName: 'Default' },
    );
  }
}
