import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToasterService } from '@abp/ng.theme.shared';
import { PermissionService } from '@abp/ng.core';
// Post-regen (G2.1, 2026-05-04): the AppointmentPacket types live under
// proxy/appointment-documents/ now -- the merged backend folded the packet
// service in alongside AppointmentDocumentService. Pre-regen the consumer
// imported from proxy/appointment-packets/, which the regenerator removed.
import { AppointmentPacketService } from '../proxy/appointment-documents/appointment-packet.service';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentPacketDto } from '../proxy/appointment-documents/models';
import { PacketGenerationStatus } from '../proxy/appointment-documents/packet-generation-status.enum';
import { PacketKind } from '../proxy/appointment-documents/packet-kind.enum';
import { AppointmentDocumentUrls } from '../appointment-documents/appointment-document-urls';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/**
 * Phase 1D.9 packet UI. Displays the per-kind packet status for an
 * appointment (Patient / Doctor / AttorneyClaimExaminer rows produced by
 * the multi-kind orchestrator at
 * <c>GenerateAppointmentPacketJob.GenerateInsideTenantAsync</c>) with
 * per-row Download actions plus one global Regenerate action that
 * re-enqueues the job for all kinds (current
 * <c>AppointmentDocumentsAppService.RegeneratePacketAsync</c> semantics).
 *
 * <para>Polls every 5 seconds while any packet is still <c>Generating</c>
 * so the office sees Failed/Generated transitions without manual refresh.
 * SignalR push is deferred to the post-MVP ledger.</para>
 */
@Component({
  selector: 'app-appointment-packet',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [CommonModule],
  templateUrl: './appointment-packet.component.html',
  styles: [
    `
      .packet-row {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 12px;
        background: #f8f9fa;
        border-radius: 4px;
      }
      .packet-row + .packet-row {
        margin-top: 8px;
      }
      .packet-meta {
        color: #6c757d;
        font-size: 0.85em;
      }
      .packet-error {
        color: #842029;
        font-size: 0.85em;
        margin-top: 4px;
      }
      .badge-status {
        font-size: 0.75em;
        padding: 4px 8px;
      }
    `,
  ],
})
export class AppointmentPacketComponent implements OnChanges, OnDestroy {
  @Input() appointmentId: string | null = null;

  private packetService = inject(AppointmentPacketService);
  private documentService = inject(AppointmentDocumentService);
  private toaster = inject(ToasterService);
  private permission = inject(PermissionService);
  // Pre-regen carried buildDownloadUrl as a hand-edited service method.
  // Post-regen we keep the same UX (window.open against an absolute URL)
  // by routing through the AppointmentDocumentUrls helper that lives
  // outside proxy/. See docs/research/proxy-regen-doc-flow-fix.md (Q2).
  private urls = inject(AppointmentDocumentUrls);
  // 2026-05-11 (Bug E fix): HttpClient (with ABP's auth interceptor)
  // for blob downloads. window.open opens a new tab with NO Bearer
  // token attached, so the API returns 500 (AbpAuthorizationException
  // mapped to 500 instead of 401). HttpClient.get with
  // responseType:'blob' goes through the interceptor + attaches the
  // Bearer transparently.
  private http = inject(HttpClient);

  packets: AppointmentPacketDto[] = [];
  isLoading = false;
  isRegenerating = false;
  private pollHandle: ReturnType<typeof setInterval> | null = null;

  readonly PacketGenerationStatus = PacketGenerationStatus;
  readonly PacketKind = PacketKind;

  get canRegenerate(): boolean {
    return this.permission.getGrantedPolicy('CaseEvaluation.AppointmentPackets.Regenerate');
  }

  get hasGenerating(): boolean {
    return this.packets.some((p) => p.status === PacketGenerationStatus.Generating);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['appointmentId']) {
      this.refresh();
    }
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  refresh(): void {
    if (!this.appointmentId) {
      this.packets = [];
      this.stopPolling();
      return;
    }
    this.isLoading = true;
    this.packetService.getListByAppointment(this.appointmentId).subscribe({
      next: (rows) => {
        // Sort by Kind enum value so display order is stable across re-fetches:
        // Patient (1) -> Doctor (2) -> AttorneyClaimExaminer (3). Sorting on
        // the client mirrors AppointmentPacketsAppService.GetListByAppointmentAsync
        // which has no ORDER BY clause guarantee.
        this.packets = (rows ?? []).slice().sort((a, b) => (a.kind ?? 0) - (b.kind ?? 0));
        this.isLoading = false;
        if (this.hasGenerating) {
          this.startPolling();
        } else {
          this.stopPolling();
        }
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  download(packet: AppointmentPacketDto): void {
    // 2026-05-11 (Bug E fix): replace the old `window.open(url, '_blank')`
    // with an authenticated HttpClient.get<Blob>. The new tab opened by
    // window.open does not inherit the SPA's Bearer token, so the API
    // rejected the request with AbpAuthorizationException (mapped to 500).
    // HttpClient runs through ABP's auth interceptor and attaches the
    // Bearer transparently, then we materialise the response as a blob
    // and trigger a synthetic anchor click for the browser file download.
    void this.downloadInternal(packet);
  }

  private async downloadInternal(packet: AppointmentPacketDto): Promise<void> {
    if (
      !this.appointmentId ||
      packet.status !== PacketGenerationStatus.Generated ||
      packet.kind == null
    ) {
      return;
    }
    const url = this.urls.buildPacket(this.appointmentId, packet.kind);
    try {
      const response = await firstValueFrom(
        this.http.get(url, {
          observe: 'response',
          responseType: 'blob',
        }),
      );
      const blob = response.body;
      if (!blob) {
        this.toaster.error('Empty packet response from server.');
        return;
      }
      // Honor Content-Disposition filename when the server provides it;
      // fall back to a synthesised name derived from the kind + confirmation.
      const disp = response.headers.get('content-disposition') || '';
      const match = /filename\*?=(?:UTF-8'')?\"?([^\";]+)/i.exec(disp);
      const fileName = match
        ? decodeURIComponent(match[1])
        : `${PacketKind[packet.kind] ?? 'Packet'}.pdf`;
      const objectUrl = URL.createObjectURL(blob);
      try {
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = fileName;
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
      } finally {
        // Small delay before revoke to let the browser kick off the download.
        setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
      }
    } catch (err) {
      // 401/403 land here when the SPA token has expired; the API now
      // returns 401 instead of 500 after Bug E backend fix.
      const status =
        (err as { status?: number; error?: { status?: number } } | null)?.status ?? '?';
      this.toaster.error(`Packet download failed (status ${status}).`);
    }
  }

  regenerate(): void {
    if (!this.appointmentId || !this.canRegenerate || this.isRegenerating) {
      return;
    }
    this.isRegenerating = true;
    // Backend semantics: one call re-enqueues GenerateAppointmentPacketArgs
    // which the orchestrator expands into all applicable kinds (Patient +
    // Doctor always; AttorneyClaimExaminer when the appointment type name
    // contains "PQME" or "AME"). Per-kind regenerate is deferred until a
    // dedicated backend endpoint lands.
    this.documentService.regeneratePacket(this.appointmentId).subscribe({
      next: () => {
        this.toaster.success('Packet regeneration queued.');
        this.isRegenerating = false;
        this.refresh();
      },
      error: () => {
        this.isRegenerating = false;
      },
    });
  }

  kindLabel(kind: PacketKind | undefined): string {
    switch (kind) {
      case PacketKind.Patient:
        return 'Patient Packet';
      case PacketKind.Doctor:
        return 'Doctor Packet';
      case PacketKind.AttorneyClaimExaminer:
        return 'Attorney / Claim Examiner Packet';
      default:
        return 'Packet';
    }
  }

  statusLabel(status: PacketGenerationStatus | undefined): string {
    switch (status) {
      case PacketGenerationStatus.Generated:
        return 'Generated';
      case PacketGenerationStatus.Failed:
        return 'Failed';
      default:
        return 'Generating';
    }
  }

  statusBadgeClass(status: PacketGenerationStatus | undefined): string {
    switch (status) {
      case PacketGenerationStatus.Generated:
        return 'bg-success';
      case PacketGenerationStatus.Failed:
        return 'bg-danger';
      default:
        return 'bg-info';
    }
  }

  trackByPacket(_index: number, packet: AppointmentPacketDto): string {
    return packet.id ?? `${packet.kind}`;
  }

  private startPolling(): void {
    this.stopPolling();
    this.pollHandle = setInterval(() => this.refresh(), 5000);
  }

  private stopPolling(): void {
    if (this.pollHandle !== null) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }
  }
}
