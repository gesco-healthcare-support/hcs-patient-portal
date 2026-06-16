import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  HostListener,
  Input,
  Output,
  inject,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { firstValueFrom } from 'rxjs';
import { FLAGGABLE_FIELDS, FlaggableField } from '../send-back-fields';

/**
 * Staff "Request info" / Send Back modal (2026-06-14). Lets internal staff flag
 * specific fields + add a note on a Pending appointment, moving it to
 * InfoRequested. POSTs to the appointment-info-requests send-back endpoint via
 * RestService (no proxy regeneration needed). Bootstrap-styled to match the
 * current internal appointment view; the redesigned staff modal lands with the
 * internal detail page (#11).
 */
@Component({
  selector: 'app-request-info-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [FormsModule],
  template: `
    @if (visible) {
      <div class="modal fade show d-block" tabindex="-1" role="dialog" aria-modal="true">
        <div class="modal-dialog modal-lg modal-dialog-centered">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title fw-bold">Request more information</h5>
              <button type="button" class="btn-close" aria-label="Close" (click)="close()"></button>
            </div>
            <div class="modal-body">
              <p class="text-muted small mb-3">
                Select the fields the requester must fix and add a note. The appointment moves to
                <strong>Info Requested</strong>
                and the requester is asked to correct only the selected fields, then resubmit.
              </p>

              @for (g of groups; track g) {
                <div class="mb-2">
                  <div class="fw-semibold small text-uppercase text-muted mb-1">{{ g }}</div>
                  <div class="d-flex flex-wrap gap-3">
                    @for (f of fieldsIn(g); track f.key) {
                      <div class="form-check">
                        <input
                          class="form-check-input"
                          type="checkbox"
                          [id]="'rif-' + f.key"
                          [checked]="selected.has(f.key)"
                          (change)="toggle(f.key)"
                        />
                        <label class="form-check-label" [for]="'rif-' + f.key">{{ f.label }}</label>
                      </div>
                    }
                  </div>
                </div>
              }

              <div class="mt-3">
                <label class="form-label fw-semibold" for="rif-note">Note to requester *</label>
                <textarea
                  id="rif-note"
                  class="form-control"
                  rows="3"
                  maxlength="1000"
                  [(ngModel)]="note"
                  placeholder="Explain what's needed and why -- this is shown to the requester."
                ></textarea>
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-secondary" (click)="close()">Cancel</button>
              <button
                type="button"
                class="btn btn-primary"
                [disabled]="isBusy || !canSend"
                (click)="send()"
              >
                Send back ({{ selected.size }} field{{ selected.size === 1 ? '' : 's' }})
              </button>
            </div>
          </div>
        </div>
      </div>
      <div class="modal-backdrop fade show"></div>
    }
  `,
})
export class RequestInfoModalComponent {
  @Input() appointmentId: string | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<void>();

  private readonly restService = inject(RestService);
  private readonly toaster = inject(ToasterService);

  protected readonly fields = FLAGGABLE_FIELDS;
  protected readonly groups = [...new Set(FLAGGABLE_FIELDS.map((f) => f.group))];
  protected readonly selected = new Set<string>();
  protected readonly hints: Record<string, string> = {};
  protected note = '';
  protected isBusy = false;

  protected get canSend(): boolean {
    return this.note.trim().length > 0;
  }

  protected fieldsIn(group: string): FlaggableField[] {
    return this.fields.filter((f) => f.group === group);
  }

  protected toggle(key: string): void {
    if (this.selected.has(key)) {
      this.selected.delete(key);
    } else {
      this.selected.add(key);
    }
  }

  @HostListener('document:keydown.escape')
  protected close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
    this.selected.clear();
    this.note = '';
    this.isBusy = false;
  }

  protected async send(): Promise<void> {
    if (!this.appointmentId || !this.canSend || this.isBusy) {
      return;
    }
    this.isBusy = true;
    try {
      await firstValueFrom(
        this.restService.request<unknown, void>(
          {
            method: 'POST',
            url: `/api/app/appointment-info-requests/send-back/${this.appointmentId}`,
            body: {
              note: this.note.trim(),
              flaggedFields: [...this.selected].map((key) => ({
                key,
                hint: this.hints[key] || null,
              })),
            },
          },
          { apiName: 'Default' },
        ),
      );
      this.toaster.success('Sent back to the requester for more information.');
      this.succeeded.emit();
      this.close();
    } catch {
      this.isBusy = false;
    }
  }
}
