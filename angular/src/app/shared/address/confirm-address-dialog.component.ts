import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
} from '@angular/core';
import { CommonModule } from '@angular/common';

/** One address whose standardized form differs from what was entered. */
export interface AddressDiffItem {
  /** Stable key (the group, e.g. 'patient'); also the radio group name. */
  key: string;
  /** Human label, e.g. "Patient address". */
  label: string;
  /** Pre-formatted display lines for the entered address. */
  enteredLines: string[];
  /** Pre-formatted display lines for the provider-standardized address. */
  suggestedLines: string[];
}

export type AddressChoice = 'suggested' | 'mine';

/**
 * F2 / address validation (2026-05-29) -- consolidated pre-submit prompt. When
 * one or more booking-form addresses have a standardized form that differs from
 * what the user typed, the booking flow shows this dialog so the user picks
 * "Use suggested" or "Keep mine" per address before the appointment is created.
 * Emits the per-address choice map; the caller applies it and proceeds.
 *
 * Rendered inline by the booking component (no modal-service dependency) and
 * bridged to the async submit via a resolver, so a provider outage simply means
 * this dialog never opens and submission is never blocked.
 */
@Component({
  selector: 'app-confirm-address-dialog',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="modal fade show d-block"
      tabindex="-1"
      role="dialog"
      style="background: rgba(0, 0, 0, 0.5)"
    >
      <div
        class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable"
        role="document"
      >
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title">Confirm addresses</h5>
          </div>
          <div class="modal-body">
            <p class="text-muted">
              We found standardized (USPS-formatted) versions of the addresses below. Choose which
              to use for each.
            </p>
            @for (item of items; track item.key) {
              <div class="border rounded p-3 mb-3">
                <div class="fw-semibold mb-2">{{ item.label }}</div>
                <div class="row">
                  <div class="col-md-6 mb-2">
                    <div class="form-check">
                      <input
                        class="form-check-input"
                        type="radio"
                        [name]="item.key"
                        [id]="item.key + '-suggested'"
                        [checked]="choices[item.key] === 'suggested'"
                        (change)="choices[item.key] = 'suggested'"
                      />
                      <label class="form-check-label fw-semibold" [for]="item.key + '-suggested'">
                        Use suggested
                      </label>
                    </div>
                    <div class="small text-muted ms-4">
                      @for (line of item.suggestedLines; track $index) {
                        <div>{{ line }}</div>
                      }
                    </div>
                  </div>
                  <div class="col-md-6 mb-2">
                    <div class="form-check">
                      <input
                        class="form-check-input"
                        type="radio"
                        [name]="item.key"
                        [id]="item.key + '-mine'"
                        [checked]="choices[item.key] === 'mine'"
                        (change)="choices[item.key] = 'mine'"
                      />
                      <label class="form-check-label" [for]="item.key + '-mine'">Keep mine</label>
                    </div>
                    <div class="small text-muted ms-4">
                      @for (line of item.enteredLines; track $index) {
                        <div>{{ line }}</div>
                      }
                    </div>
                  </div>
                </div>
              </div>
            }
          </div>
          <div class="modal-footer">
            <button type="button" class="btn btn-outline-secondary" (click)="keepAllMine()">
              Keep all mine
            </button>
            <button type="button" class="btn btn-primary" (click)="confirm()">Continue</button>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class ConfirmAddressDialogComponent implements OnInit {
  @Input({ required: true }) items: AddressDiffItem[] = [];
  /** Emits the per-address choice map once the user resolves the dialog. */
  @Output() resolved = new EventEmitter<Record<string, AddressChoice>>();

  /** Per-address selection; defaults to the standardized suggestion. */
  choices: Record<string, AddressChoice> = {};

  ngOnInit(): void {
    for (const item of this.items) {
      this.choices[item.key] = 'suggested';
    }
  }

  confirm(): void {
    this.resolved.emit({ ...this.choices });
  }

  keepAllMine(): void {
    const allMine: Record<string, AddressChoice> = {};
    for (const item of this.items) {
      allMine[item.key] = 'mine';
    }
    this.resolved.emit(allMine);
  }
}
