import { EnvironmentService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import { PacketKind } from '../proxy/appointment-documents/packet-kind.enum';

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
   * Returns the absolute URL for downloading one rendered packet (DOCX for
   * Phase 1, PDF once Phase 2's DOCX->PDF conversion lands). Mirrors the
   * per-kind route added in Phase 1D.9
   * (<c>AppointmentPacketsAppService.DownloadByKindAsync</c>): one of the
   * three <see cref="PacketKind"/> values produced by the multi-kind
   * orchestrator at <c>GenerateAppointmentPacketJob.GenerateInsideTenantAsync</c>.
   */
  buildPacket(appointmentId: string, kind: PacketKind): string {
    const base = this.environmentService.getApiUrl('Default') ?? '';
    return `${base}/api/app/appointments/${appointmentId}/packet/download/${kind}`;
  }
}
