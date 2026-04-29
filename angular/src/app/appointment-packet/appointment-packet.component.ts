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
import { AppointmentPacketService } from '../proxy/appointment-packets/appointment-packet.service';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentPacketDto, PacketGenerationStatus } from '../proxy/appointment-packets/models';

/**
 * W2-11 packet UI. Displays the merged-PDF packet status for an
 * appointment + Download / Regenerate actions. Polls every 5 seconds
 * while Generating so the office sees Failed/Generated transitions
 * without manual refresh. SignalR push deferred to ledger.
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

  packet: AppointmentPacketDto | null = null;
  isLoading = false;
  isRegenerating = false;
  private pollHandle: ReturnType<typeof setInterval> | null = null;

  readonly PacketGenerationStatus = PacketGenerationStatus;

  get canRegenerate(): boolean {
    return this.permission.getGrantedPolicy('CaseEvaluation.AppointmentPackets.Regenerate');
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
      this.packet = null;
      this.stopPolling();
      return;
    }
    this.isLoading = true;
    this.packetService.getByAppointment(this.appointmentId).subscribe({
      next: (row) => {
        this.packet = row ?? null;
        this.isLoading = false;
        if (this.packet?.status === PacketGenerationStatus.Generating) {
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

  download(): void {
    if (!this.appointmentId || this.packet?.status !== PacketGenerationStatus.Generated) {
      return;
    }
    const url = this.packetService.buildDownloadUrl(this.appointmentId);
    window.open(url, '_blank');
  }

  regenerate(): void {
    if (!this.appointmentId || !this.canRegenerate || this.isRegenerating) {
      return;
    }
    this.isRegenerating = true;
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

  statusLabel(status: PacketGenerationStatus): string {
    switch (status) {
      case PacketGenerationStatus.Generated:
        return 'Generated';
      case PacketGenerationStatus.Failed:
        return 'Failed';
      default:
        return 'Generating';
    }
  }

  statusBadgeClass(status: PacketGenerationStatus): string {
    switch (status) {
      case PacketGenerationStatus.Generated:
        return 'bg-success';
      case PacketGenerationStatus.Failed:
        return 'bg-danger';
      default:
        return 'bg-info';
    }
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
