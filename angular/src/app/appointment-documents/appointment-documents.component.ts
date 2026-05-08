import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { PermissionService, RestService } from '@abp/ng.core';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentDocumentDto } from '../proxy/appointment-documents/models';
import { DocumentStatus } from '../proxy/appointment-documents/document-status.enum';
import { AppointmentDocumentUrls } from './appointment-document-urls';

/**
 * W1-3 + W2-11 appointment-documents UI. Embedded inside appointment-view
 * as a standalone child block. Lists existing documents with status badges,
 * exposes upload + Approve/Reject for internal users (gated on the
 * AppointmentDocuments.Approve permission), and emits documentsChanged so
 * the parent can refresh the packet panel after an approve/reject action.
 */
@Component({
  selector: 'app-appointment-documents',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [CommonModule, FormsModule],
  templateUrl: './appointment-documents.component.html',
  styles: [
    `
      .doc-row {
        padding: 8px 12px;
        border-bottom: 1px solid #e9ecef;
        display: flex;
        align-items: center;
        gap: 12px;
      }
      .doc-row:last-child {
        border-bottom: none;
      }
      .doc-meta {
        color: #6c757d;
        font-size: 0.85em;
      }
      .doc-rejection {
        color: #842029;
        font-size: 0.85em;
        margin-top: 2px;
      }
      .upload-form {
        display: flex;
        gap: 8px;
        align-items: end;
        flex-wrap: wrap;
        padding: 12px;
        background: #f8f9fa;
        border-radius: 4px;
      }
      .badge-status {
        font-size: 0.75em;
        padding: 4px 8px;
      }
      .reject-modal-backdrop {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.5);
        z-index: 1050;
        display: flex;
        align-items: center;
        justify-content: center;
      }
      .reject-modal {
        background: #fff;
        border-radius: 8px;
        max-width: 480px;
        width: 90%;
        padding: 20px;
      }
    `,
  ],
})
export class AppointmentDocumentsComponent implements OnChanges {
  @Input() appointmentId: string | null = null;
  @Output() documentsChanged = new EventEmitter<void>();

  private service = inject(AppointmentDocumentService);
  private toaster = inject(ToasterService);
  private permission = inject(PermissionService);
  // Direct REST + URL helper bypass the auto-generated upload() / download
  // helpers on AppointmentDocumentService. The proxy generator emits a
  // typed multipart wrapper (UploadAppointmentDocumentForm with IFormFile)
  // that does not produce a valid browser FormData request, and the
  // hand-edited buildDownloadUrl helper does not survive regeneration.
  // See docs/research/proxy-regen-doc-flow-fix.md (Q2).
  private restService = inject(RestService);
  private urls = inject(AppointmentDocumentUrls);

  documents: AppointmentDocumentDto[] = [];
  isLoading = false;
  isUploading = false;
  documentName = '';
  selectedFile: File | null = null;

  rejectingDoc: AppointmentDocumentDto | null = null;
  rejectionReason = '';
  isSubmittingReject = false;

  readonly maxBytes = 25 * 1024 * 1024;
  readonly DocumentStatus = DocumentStatus;

  get canApprove(): boolean {
    return this.permission.getGrantedPolicy('CaseEvaluation.AppointmentDocuments.Approve');
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['appointmentId'] && this.appointmentId) {
      this.refresh();
    }
  }

  refresh(): void {
    if (!this.appointmentId) {
      return;
    }
    this.isLoading = true;
    this.service.getList(this.appointmentId).subscribe({
      next: (rows) => {
        this.documents = rows ?? [];
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    if (this.selectedFile && !this.documentName.trim()) {
      this.documentName = this.selectedFile.name.replace(/\.[^.]+$/, '');
    }
  }

  upload(): void {
    if (!this.appointmentId || !this.selectedFile || this.isUploading) {
      return;
    }
    if (this.selectedFile.size > this.maxBytes) {
      this.toaster.error(`File exceeds the ${this.maxBytes / (1024 * 1024)} MB upload cap.`);
      return;
    }
    const form = new FormData();
    form.append('file', this.selectedFile, this.selectedFile.name);
    form.append('documentName', this.documentName.trim() || this.selectedFile.name);

    this.isUploading = true;
    this.restService
      .request<FormData, AppointmentDocumentDto>(
        {
          method: 'POST',
          url: `/api/app/appointments/${this.appointmentId}/documents`,
          body: form,
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: () => {
          this.toaster.success('Document uploaded.');
          this.documentName = '';
          this.selectedFile = null;
          const input = document.getElementById('document-file-input') as HTMLInputElement | null;
          if (input) {
            input.value = '';
          }
          this.refresh();
          this.documentsChanged.emit();
          this.isUploading = false;
        },
        error: () => {
          this.isUploading = false;
        },
      });
  }

  download(doc: AppointmentDocumentDto): void {
    if (!this.appointmentId) {
      return;
    }
    if (!doc.id) {
      return;
    }
    const url = this.urls.build(this.appointmentId, doc.id);
    window.open(url, '_blank');
  }

  delete(doc: AppointmentDocumentDto): void {
    if (!this.appointmentId) {
      return;
    }
    if (!confirm(`Delete "${doc.documentName}"?`)) {
      return;
    }
    if (!doc.id) {
      return;
    }
    this.service.delete(this.appointmentId, doc.id).subscribe({
      next: () => {
        this.toaster.success('Document deleted.');
        this.refresh();
        this.documentsChanged.emit();
      },
    });
  }

  approve(doc: AppointmentDocumentDto): void {
    if (!this.appointmentId || !this.canApprove) {
      return;
    }
    if (!doc.id) {
      return;
    }
    this.service.approve(this.appointmentId, doc.id).subscribe({
      next: () => {
        this.toaster.success('Document approved.');
        this.refresh();
        this.documentsChanged.emit();
      },
    });
  }

  openRejectModal(doc: AppointmentDocumentDto): void {
    if (!this.canApprove) {
      return;
    }
    this.rejectingDoc = doc;
    this.rejectionReason = doc.rejectionReason ?? '';
  }

  closeRejectModal(): void {
    this.rejectingDoc = null;
    this.rejectionReason = '';
    this.isSubmittingReject = false;
  }

  submitReject(): void {
    if (!this.appointmentId || !this.rejectingDoc) {
      return;
    }
    const reason = this.rejectionReason.trim();
    if (!reason) {
      this.toaster.error('Rejection reason is required.');
      return;
    }
    if (reason.length > 500) {
      this.toaster.error('Rejection reason exceeds 500 characters.');
      return;
    }
    if (!this.rejectingDoc.id) {
      return;
    }
    this.isSubmittingReject = true;
    this.service.reject(this.appointmentId, this.rejectingDoc.id, { reason }).subscribe({
      next: () => {
        this.toaster.success('Document rejected.');
        this.closeRejectModal();
        this.refresh();
        this.documentsChanged.emit();
      },
      error: () => {
        this.isSubmittingReject = false;
      },
    });
  }

  statusLabel(status: DocumentStatus): string {
    switch (status) {
      case DocumentStatus.Accepted:
        return 'Approved';
      case DocumentStatus.Rejected:
        return 'Rejected';
      default:
        return 'Uploaded';
    }
  }

  statusBadgeClass(status: DocumentStatus): string {
    switch (status) {
      case DocumentStatus.Accepted:
        return 'bg-success';
      case DocumentStatus.Rejected:
        return 'bg-danger';
      default:
        return 'bg-secondary';
    }
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
