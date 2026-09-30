import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import {
  PREFILL_SECTIONS,
  defaultPrefillSelection,
  type PrefillSection,
  type PrefillSelection,
} from './prefill-sections';

/**
 * Item 5 (2026-08-18) -- asks the booker which sections have changed since the appointment
 * this booking was prefilled from.
 *
 * Prefill is otherwise all-or-nothing and silent: a defense attorney who changed eight months
 * ago arrives looking exactly as correct as one that did not. The booker ticks what has moved
 * on, and those sections are cleared for re-entry.
 *
 * Deliberately NOT an `<abp-modal>`. That component takes an `[options]` object, and binding a
 * getter that CONSTRUCTS one loops change detection and hangs the tab with no error -- a trap
 * this codebase has already been caught by. A plain overlay has no such edge, and this dialog
 * needs none of abp-modal's machinery.
 */
@Component({
  selector: 'app-prefill-picker-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible) {
      <div class="modal fade show d-block" tabindex="-1" role="dialog">
        <div class="modal-dialog modal-dialog-centered" role="document">
          <div
            class="modal-content"
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="prefill-picker-title"
          >
            <div class="modal-header">
              <h5 class="modal-title" id="prefill-picker-title">What has changed?</h5>
            </div>
            <div class="modal-body">
              <p class="text-muted">
                We filled this in from {{ sourceConfirmationNumber || 'the earlier appointment' }}.
                Tick anything that has changed since then and we will clear it for you to re-enter.
                Leave the rest and we will keep what we have.
              </p>
              @for (section of sections; track section.key) {
                <div class="form-check mb-2">
                  <input
                    class="form-check-input"
                    type="checkbox"
                    [id]="'prefill-section-' + section.key"
                    [checked]="draft[section.key]"
                    (change)="toggle(section.key)"
                  />
                  <label class="form-check-label" [for]="'prefill-section-' + section.key">
                    {{ section.label }}
                  </label>
                </div>
              }
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" (click)="onKeepEverything()">
                Nothing has changed
              </button>
              <button type="button" class="btn btn-primary" (click)="onConfirm()">
                Clear what I ticked
              </button>
            </div>
          </div>
        </div>
      </div>
      <div class="modal-backdrop fade show"></div>
    }
  `,
})
export class PrefillPickerModalComponent {
  protected readonly sections = PREFILL_SECTIONS;

  @Input() visible = false;
  /** Shown so the booker knows WHICH appointment this was filled in from. */
  @Input() sourceConfirmationNumber: string | null = null;
  /** Seeds the checkboxes, so re-opening the picker shows the previous answer. */
  @Input() set selection(value: PrefillSelection | null) {
    this.draft = { ...(value ?? defaultPrefillSelection()) };
  }

  @Output() confirmed = new EventEmitter<PrefillSelection>();

  protected draft: PrefillSelection = defaultPrefillSelection();

  protected toggle(section: PrefillSection): void {
    this.draft = { ...this.draft, [section]: !this.draft[section] };
  }

  protected onConfirm(): void {
    this.confirmed.emit({ ...this.draft });
  }

  /**
   * The explicit "nothing changed" answer. Emits an all-false selection rather than simply
   * dismissing, because dismissing would leave the question unanswered and re-open the picker
   * on the next step -- and because "I looked and it is all still right" is a real answer worth
   * recording, not a cancellation.
   */
  protected onKeepEverything(): void {
    this.confirmed.emit(defaultPrefillSelection());
  }
}
