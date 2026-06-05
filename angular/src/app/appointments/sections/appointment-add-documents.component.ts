import { Component, EventEmitter, Input, Output } from '@angular/core';

export type StagedDocumentStatus = 'staged' | 'uploading' | 'uploaded' | 'failed';

/** A document the booker has attached but not yet uploaded (uploaded post-create). */
export interface StagedDocumentUpload {
  file: File;
  status: StagedDocumentStatus;
  error?: string;
}

/**
 * AF7 (2026-06-05) -- pre-submit document upload card. Lets the booker attach
 * documents while filling the form; the parent uploads them after the
 * appointment is created (two-phase create-then-upload, since
 * AppointmentDocument.AppointmentId is non-nullable). Template-only: the staged
 * File[], client validation, and the post-create upload loop all live in
 * AppointmentAddComponent (the FormGroup-lives-here-only convention). This card
 * only emits picks + removals and renders per-file upload status during submit.
 */
// Default change detection (NOT OnPush): the parent mutates the
// stagedDocuments array in place (push / splice / per-file status during the
// upload loop), and the parent component uses default CD, so this card renders
// those mutations without immutable-update ceremony.
@Component({
  selector: 'app-appointment-add-documents',
  standalone: true,
  imports: [],
  templateUrl: './appointment-add-documents.component.html',
})
export class AppointmentAddDocumentsComponent {
  @Input() stagedDocuments: StagedDocumentUpload[] = [];
  /** Locks the picker + remove buttons while the post-create upload loop runs. */
  @Input() disabled = false;

  @Output() filesSelected = new EventEmitter<File[]>();
  @Output() removeDocument = new EventEmitter<number>();
  @Output() retryUploads = new EventEmitter<void>();

  get hasFailedUploads(): boolean {
    return this.stagedDocuments.some((d) => d.status === 'failed');
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) {
      return;
    }
    this.filesSelected.emit(Array.from(input.files));
    // Clear the native input so re-selecting the same file fires change again.
    input.value = '';
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
