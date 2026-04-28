import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  SimpleChanges,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import type { AppointmentDocumentDto } from '../proxy/appointment-documents/models';

/**
 * W1-3 appointment-documents UI. Embedded inside appointment-view as a
 * standalone child block. Lists existing documents for the appointment and
 * exposes a simple upload form (file input + DocumentName text input).
 *
 * Cuts deferred to ledger:
 *   - Approve / Reject document workflow (no status surface yet).
 *   - Verification-code anonymous download.
 *   - Drag-and-drop upload (basic file input only).
 *   - Multi-file upload (one file at a time at MVP).
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
      .upload-form {
        display: flex;
        gap: 8px;
        align-items: end;
        flex-wrap: wrap;
        padding: 12px;
        background: #f8f9fa;
        border-radius: 4px;
      }
    `,
  ],
})
export class AppointmentDocumentsComponent implements OnChanges {
  @Input() appointmentId: string | null = null;

  private service = inject(AppointmentDocumentService);
  private toaster = inject(ToasterService);

  documents: AppointmentDocumentDto[] = [];
  isLoading = false;
  isUploading = false;
  documentName = '';
  selectedFile: File | null = null;

  readonly maxBytes = 25 * 1024 * 1024;

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
    this.service.upload(this.appointmentId, form).subscribe({
      next: () => {
        this.toaster.success('Document uploaded.');
        this.documentName = '';
        this.selectedFile = null;
        const input = document.getElementById('document-file-input') as HTMLInputElement | null;
        if (input) {
          input.value = '';
        }
        this.refresh();
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
    const url = this.service.buildDownloadUrl(this.appointmentId, doc.id);
    // window.open routes through the browser's session cookies + ABP auth
    // interceptor on the SPA shell. For SPA-only setups without cookie
    // auth, we'd need to fetch + Blob URL; deferred to ledger.
    window.open(url, '_blank');
  }

  delete(doc: AppointmentDocumentDto): void {
    if (!this.appointmentId) {
      return;
    }
    if (!confirm(`Delete "${doc.documentName}"?`)) {
      return;
    }
    this.service.delete(this.appointmentId, doc.id).subscribe({
      next: () => {
        this.toaster.success('Document deleted.');
        this.refresh();
      },
    });
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
