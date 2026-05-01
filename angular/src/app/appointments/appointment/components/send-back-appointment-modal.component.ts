import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import {
  ModalComponent,
  ModalCloseDirective,
  ButtonComponent,
  ToasterService,
} from '@abp/ng.theme.shared';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import type { AppointmentDto, SendBackAppointmentInput } from '../../../proxy/appointments/models';
import {
  FLAGGABLE_SECTIONS,
  TOTAL_FLAGGABLE_FIELD_COUNT,
  type FlaggableSection,
} from '../send-back-fields';

/**
 * W1-1 Send-back-for-info modal. Office picks any of 92 fields across 9
 * sections + writes a note. Submitting routes to
 * /api/app/appointments/{id}/send-back which transitions Pending ->
 * AwaitingMoreInfo and persists an AppointmentSendBackInfo row.
 *
 * Field source: send-back-fields.ts (single source of truth).
 * Sections 6-9 (Defense Attorney, Patient Injury, Insurance, Claim Adjuster)
 * carry a "ships in a later wave" hint -- office can flag now; booker sees
 * the flag but cannot edit until those caps land.
 *
 * Submit enabled when (>=1 field flagged) OR (note non-empty).
 */
@Component({
  selector: 'app-send-back-appointment-modal',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    ModalComponent,
    ModalCloseDirective,
    ButtonComponent,
  ],
  templateUrl: './send-back-appointment-modal.component.html',
  styles: [
    `
      .section-panel {
        border: 1px solid #dee2e6;
        border-radius: 4px;
        margin-bottom: 8px;
      }
      .section-header {
        padding: 10px 14px;
        cursor: pointer;
        background: #f8f9fa;
        user-select: none;
        display: flex;
        justify-content: space-between;
        align-items: center;
      }
      .section-header:hover {
        background: #e9ecef;
      }
      .section-header .selected-count {
        color: #0d6efd;
        font-weight: 600;
        font-size: 0.9em;
      }
      .section-deferred {
        color: #6c757d;
        font-style: italic;
        font-size: 0.85em;
      }
      .field-grid {
        padding: 12px 16px;
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 6px 16px;
      }
      .field-checkbox label {
        font-weight: 400;
        cursor: pointer;
      }
      .total-counter {
        font-weight: 600;
        padding: 8px 0;
        border-bottom: 1px solid #dee2e6;
        margin-bottom: 12px;
      }
    `,
  ],
})
export class SendBackAppointmentModalComponent {
  @Input() appointmentId: string | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<AppointmentDto>();

  private appointmentService = inject(AppointmentService);
  private toaster = inject(ToasterService);

  readonly sections: readonly FlaggableSection[] = FLAGGABLE_SECTIONS;
  readonly totalFieldCount = TOTAL_FLAGGABLE_FIELD_COUNT;
  readonly maxNoteLength = 2000;

  /** Map of fieldKey -> boolean checked state. */
  selectedFields: Record<string, boolean> = {};
  /** Map of sectionId -> open/closed accordion state. */
  expandedSections: Record<string, boolean> = {};

  note = '';
  isBusy = false;

  get selectedCount(): number {
    return Object.values(this.selectedFields).filter(Boolean).length;
  }

  get canSubmit(): boolean {
    return !this.isBusy && (this.selectedCount > 0 || this.note.trim().length > 0);
  }

  countSelectedInSection(section: FlaggableSection): number {
    return section.fields.filter((f) => this.selectedFields[f.key]).length;
  }

  toggleSection(sectionId: string): void {
    this.expandedSections[sectionId] = !this.expandedSections[sectionId];
  }

  isSectionDeferred(section: FlaggableSection): boolean {
    return section.wave !== 'W1';
  }

  setVisible(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.selectedFields = {};
      this.expandedSections = {};
      this.note = '';
      this.isBusy = false;
    }
  }

  submit(): void {
    if (!this.appointmentId || !this.canSubmit) {
      return;
    }
    this.isBusy = true;
    const flaggedFields = Object.entries(this.selectedFields)
      .filter(([, checked]) => checked)
      .map(([key]) => key);
    const input: SendBackAppointmentInput = {
      flaggedFields,
      note: this.note.trim() || undefined,
    };
    this.appointmentService.sendBack(this.appointmentId, input).subscribe({
      next: (dto) => {
        this.toaster.success('::Appointment:Toast:SentBack');
        this.succeeded.emit(dto);
        this.setVisible(false);
      },
      error: () => {
        this.isBusy = false;
      },
    });
  }
}
