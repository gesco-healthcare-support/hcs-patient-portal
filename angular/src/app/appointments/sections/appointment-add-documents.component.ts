import { Component, EventEmitter, Input, Output } from '@angular/core';

export type StagedDocumentStatus = 'staged' | 'uploading' | 'uploaded' | 'failed';

/**
 * Sentinel <option> value for the "Other" choice in the document-type picker.
 * Selecting it reveals a free-text input; the parent maps it to the backend's
 * OtherDocumentTypeName (mutually exclusive with a real AppointmentDocumentTypeId).
 */
export const OTHER_DOCUMENT_TYPE_VALUE = '__other__';

/** Mirrors backend AppointmentDocumentConsts.OtherDocumentTypeNameMaxLength. */
export const OTHER_DOCUMENT_TYPE_MAX_LENGTH = 100;

/** A document the booker has attached but not yet uploaded (uploaded post-create). */
export interface StagedDocumentUpload {
  file: File;
  status: StagedDocumentStatus;
  error?: string;
  /** AF6: true when the booker marked this staged doc as the PQME panel strike list. */
  isStrikeList: boolean;
  /** I15 (2026-06-08): the chosen document-category label id (null = unspecified). */
  documentTypeId?: string | null;
  /** True when the booker picked "Other" and will type a free-text label. */
  isOtherType?: boolean;
  /** Free-text label entered when isOtherType is true (max OTHER_DOCUMENT_TYPE_MAX_LENGTH). */
  otherDocumentTypeName?: string | null;
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
  /** AF6: PQME-only UI (the strike-list checkbox + per-row selector) shows only when true. */
  @Input() isPqme = false;
  /** AF6: bound to the parent's hasPanelStrikeList flag (opt-in strike-list requirement). */
  @Input() hasPanelStrikeList = false;
  /** I15 (2026-06-08): per-appointment-type document-category labels for the picker. */
  @Input() documentTypeOptions: { id: string; displayName: string }[] = [];

  @Output() filesSelected = new EventEmitter<File[]>();
  @Output() removeDocument = new EventEmitter<number>();
  @Output() retryUploads = new EventEmitter<void>();
  /** AF6: the PQME "I have the panel strike list" checkbox toggled. */
  @Output() hasPanelStrikeListChange = new EventEmitter<boolean>();
  /** I15 (2026-06-08): the booker chose a document-type label for staged doc [index]. */
  @Output() documentTypeChange = new EventEmitter<{ index: number; typeId: string | null }>();
  /** The free-text "Other" label was edited for staged doc [index]. */
  @Output() otherDocumentTypeNameChange = new EventEmitter<{ index: number; value: string }>();

  /** Exposed to the template: the "Other" sentinel option value + free-text cap. */
  protected readonly otherValue = OTHER_DOCUMENT_TYPE_VALUE;
  protected readonly otherMaxLength = OTHER_DOCUMENT_TYPE_MAX_LENGTH;

  onHasPanelStrikeListChange(event: Event): void {
    this.hasPanelStrikeListChange.emit((event.target as HTMLInputElement).checked);
  }

  onDocumentTypeChange(index: number, event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.documentTypeChange.emit({ index, typeId: value || null });
  }

  onOtherDocumentTypeNameChange(index: number, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.otherDocumentTypeNameChange.emit({ index, value });
  }

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
