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
import { ToasterService } from '@abp/ng.theme.shared';
import { firstValueFrom } from 'rxjs';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { AppointmentInfoRequestService } from '../../../proxy/appointment-info-requests/appointment-info-request.service';
import { FLAGGABLE_FIELDS, FlaggableField } from '../send-back-fields';
import { buildSendBackInput, canSendBack } from './request-info-modal.util';

/** Only requester-provided fields are sent back; staff/scheduling fields are excluded. */
const SEND_BACK_FIELDS = FLAGGABLE_FIELDS.filter((f) => f.sendBackFlaggable);

/**
 * Staff "Request info" / Send Back modal (Prompt 17 redesign). Lets internal
 * staff flag specific requester-provided fields -- each with an optional hint --
 * plus a required note on a Pending appointment, moving it to InfoRequested and
 * emailing the requester a fix-it link. Built on the redesign's ra-modal shell
 * (design_handoff_appointment_portal/components/sb-after.jsx, StaffSendBack);
 * submits through the generated AppointmentInfoRequestService proxy. Default CD
 * because the host (internal appointment detail) extends the default-CD view.
 */
@Component({
  selector: 'app-request-info-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [FormsModule, IconComponent],
  template: `
    @if (visible) {
      <div class="ra-scrim" (click)="close()">
        <div
          class="ra-modal ra-modal--lg"
          role="dialog"
          aria-modal="true"
          (click)="$event.stopPropagation()"
        >
          <div class="ra-modal__head">
            <span class="ic tint-purple"><app-icon name="help" [size]="19" /></span>
            <h3>Request more information</h3>
            <button type="button" class="x" aria-label="Close" (click)="close()">
              <app-icon name="x" [size]="17" />
            </button>
          </div>

          <div class="ra-modal__body">
            <div class="ra-field col-12" style="margin-bottom: 14px">
              <label>
                Fields that need to be fixed
                <span class="req">*</span>
              </label>
              <div class="sb-tree">
                @for (g of groups; track g) {
                  <div class="sb-tree__group">
                    <div class="sb-tree__ghead">
                      {{ g }}
                      @if (groupCount(g) > 0) {
                        <span class="cnt">{{ groupCount(g) }}</span>
                      }
                    </div>
                    @for (f of fieldsIn(g); track f.key) {
                      <div class="sb-tree__row">
                        <label class="main">
                          <input
                            type="checkbox"
                            [checked]="selected.has(f.key)"
                            (change)="toggle(f.key)"
                          />
                          <b>{{ f.label }}</b>
                        </label>
                        @if (selected.has(f.key)) {
                          <div class="hint-in">
                            <input
                              #hi
                              maxlength="150"
                              [value]="hints[f.key] || ''"
                              [placeholder]="hintPlaceholder"
                              (input)="setHint(f.key, hi.value)"
                            />
                          </div>
                        }
                      </div>
                    }
                  </div>
                }
              </div>
            </div>

            @if (selected.size > 0) {
              <div class="ra-field col-12" style="margin-bottom: 14px">
                <label>Selected ({{ selected.size }})</label>
                <div class="sb-chips">
                  @for (k of selectedKeys(); track k) {
                    <span class="sb-chip">
                      {{ labelOf(k) }}
                      <button type="button" aria-label="Remove" (click)="toggle(k)">
                        <app-icon name="x" [size]="12" />
                      </button>
                    </span>
                  }
                </div>
              </div>
            }

            <div class="ra-field col-12">
              <label>
                Note to the requester
                <span class="req">*</span>
              </label>
              <textarea
                class="ra-input"
                rows="4"
                maxlength="500"
                [(ngModel)]="note"
                placeholder="Explain what's needed and why -- this goes in the email and on their fix-it page."
              ></textarea>
              <div class="ra-hint" style="text-align: right">{{ note.length }}/500</div>
            </div>

            <div class="ra-note" style="margin-top: 4px">
              <span class="i"><app-icon name="bell" [size]="15" /></span>
              <span>
                The requester gets an
                <b>email with your note and a direct link</b>
                ; the appointment shows as
                <b>Info Requested</b>
                until they resubmit.
              </span>
            </div>
          </div>

          <div class="ra-modal__foot">
            <button type="button" class="af-btn af-btn--ghost" (click)="close()">Cancel</button>
            <button
              type="button"
              class="af-btn af-btn--primary"
              [disabled]="isBusy || !canSend"
              (click)="send()"
            >
              <app-icon name="arrowUp" [size]="15" />
              Send back ({{ selected.size }} field{{ selected.size === 1 ? '' : 's' }})
            </button>
          </div>
        </div>
      </div>
    }
  `,
})
export class RequestInfoModalComponent {
  @Input() appointmentId: string | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<void>();

  private readonly infoRequests = inject(AppointmentInfoRequestService);
  private readonly toaster = inject(ToasterService);

  protected readonly fields = SEND_BACK_FIELDS;
  protected readonly groups = [...new Set(SEND_BACK_FIELDS.map((f) => f.group))];
  protected readonly selected = new Set<string>();
  protected readonly hints: Record<string, string> = {};
  protected note = '';
  protected isBusy = false;

  protected readonly hintPlaceholder =
    'Optional hint shown next to this field (e.g. "doesn\'t match the panel on file")';

  protected get canSend(): boolean {
    return canSendBack(this.selected.size, this.note);
  }

  protected fieldsIn(group: string): FlaggableField[] {
    return this.fields.filter((f) => f.group === group);
  }

  protected groupCount(group: string): number {
    return this.fieldsIn(group).filter((f) => this.selected.has(f.key)).length;
  }

  protected selectedKeys(): string[] {
    return this.fields.filter((f) => this.selected.has(f.key)).map((f) => f.key);
  }

  protected labelOf(key: string): string {
    return this.fields.find((f) => f.key === key)?.label ?? key;
  }

  protected toggle(key: string): void {
    if (this.selected.has(key)) {
      this.selected.delete(key);
      delete this.hints[key];
    } else {
      this.selected.add(key);
    }
  }

  protected setHint(key: string, value: string): void {
    this.hints[key] = value;
  }

  @HostListener('document:keydown.escape')
  protected close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
    this.reset();
  }

  protected async send(): Promise<void> {
    if (!this.appointmentId || !this.canSend || this.isBusy) {
      return;
    }
    this.isBusy = true;
    try {
      await firstValueFrom(
        this.infoRequests.sendBack(
          this.appointmentId,
          buildSendBackInput(this.selectedKeys(), this.hints, this.note),
        ),
      );
      this.toaster.success('Sent back to the requester - email queued with your note.');
      this.succeeded.emit();
      this.close();
    } catch {
      this.isBusy = false;
    }
  }

  private reset(): void {
    this.selected.clear();
    for (const k of Object.keys(this.hints)) {
      delete this.hints[k];
    }
    this.note = '';
    this.isBusy = false;
  }
}
