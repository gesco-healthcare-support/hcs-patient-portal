// Issue #117 (2026-05-13) -- HAND-PATCHED FILE. The ABP proxy generator
// (`abp generate-proxy -t ng -m app`) emits `body: form.file` which
// fails server-side because the controller expects `[FromForm]
// UploadAppointmentDocumentForm`. ABP has historically not supported
// generating multipart proxies for `IFormFile` properties. The
// hand-patched method body builds a `FormData` payload that the
// RestService passes through unchanged. If `abp generate-proxy` is
// re-run, uploadByVerificationCode will regress -- re-apply the same
// FormData pattern.
import type { AppointmentDocumentDto, UploadAppointmentDocumentForm } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PublicDocumentUploadService {
  private restService = inject(RestService);
  apiName = 'Default';

  // Issue #117 (2026-05-13) HAND-PATCHED: was `body: form.file`. Now
  // sends FormData so the [FromForm] controller binds correctly.
  uploadByVerificationCode = (id: string, verificationCode: string, form: UploadAppointmentDocumentForm, config?: Partial<Rest.Config>) => {
    const fd = new FormData();
    if (form.documentName !== undefined && form.documentName !== null && form.documentName !== '') {
      fd.append('DocumentName', form.documentName);
    }
    if (form.file) {
      fd.append('File', form.file as unknown as Blob);
    }
    return this.restService.request<any, AppointmentDocumentDto>({
      method: 'POST',
      url: `/api/public/appointment-documents/${id}/upload-by-code/${verificationCode}`,
      body: fd,
    },
    { apiName: this.apiName,...config });
  };
}
