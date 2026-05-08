import { EnvironmentService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

/**
 * URL helpers for appointment-document and appointment-packet downloads.
 *
 * Pre-regen, the typed proxy `AppointmentDocumentService` carried a
 * hand-edited `buildDownloadUrl(appointmentId, documentId)` instance method.
 * `abp generate-proxy` does not preserve hand-edits in `proxy/`, so the
 * helper is moved here -- outside `proxy/` -- for parity with the
 * pre-regen consumer behavior.
 *
 * <para>The download endpoint shape mirrors the OLD-app intent
 * (anchor-style file fetch). OLD used <c>DocumentDownloadController.
 * DownloadFile?filePath=...</c> and consumed the response as a Blob
 * (no URL helper existed in OLD). NEW exposes parameterised routes
 * (<c>GET /api/app/appointments/{appointmentId}/documents/{id}/download</c>
 * and the packet equivalent), and the consumer triggers a browser
 * download via <c>window.open</c> against the constructed URL. Auth
 * is carried by ABP's cookie session for the AuthServer-backed flow.</para>
 *
 * Tracked: docs/research/proxy-regen-doc-flow-fix.md (Q2).
 */
@Injectable({ providedIn: 'root' })
export class AppointmentDocumentUrls {
  private readonly environmentService = inject(EnvironmentService);

  /**
   * Returns the absolute URL for downloading a single document attached to
   * an appointment. Mirrors <c>AppointmentDocumentController.DownloadAsync</c>.
   */
  build(appointmentId: string, documentId: string): string {
    const base = this.environmentService.getApiUrl('Default') ?? '';
    return `${base}/api/app/appointments/${appointmentId}/documents/${documentId}/download`;
  }

  /**
   * Returns the absolute URL for downloading the merged packet (all
   * accepted package documents merged into a single PDF) for an
   * appointment. Mirrors <c>AppointmentPacketController.DownloadAsync</c>.
   */
  buildPacket(appointmentId: string): string {
    const base = this.environmentService.getApiUrl('Default') ?? '';
    return `${base}/api/app/appointments/${appointmentId}/packet/download`;
  }
}
