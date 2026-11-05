import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  notifyParalegalErrors,
  type NotifyParalegalField,
  type NotifyParalegalState,
} from '../shared/notify-paralegal.util';

/**
 * "Send all notifications to my paralegal" -- shown under an attorney booker's own-side
 * attorney section. The parent owns the state (it is read at submit); this child only edits a
 * copy and emits the new value. The lawyer keeps receiving every email.
 */
@Component({
  selector: 'app-appointment-notify-paralegal',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card mb-4" [id]="idPrefix + '-card'">
      <div class="card-body">
        <div class="form-check">
          <input
            class="form-check-input"
            type="checkbox"
            [id]="idPrefix + '-enabled'"
            [ngModel]="state.enabled"
            (ngModelChange)="update({ enabled: $event })"
          />
          <label class="form-check-label fw-semibold" [for]="idPrefix + '-enabled'">
            Send all notifications to my paralegal instead of me
          </label>
        </div>
        <div class="text-secondary small mt-1">
          Your paralegal is added as an Authorized User and gets every email about this case. You
          get no emails for this case, but you can still sign in to see it.
        </div>
        @if (state.enabled) {
          <div class="row mt-3">
            <div class="col-md-4 mb-3">
              <label [for]="idPrefix + '-first'" class="form-label fw-semibold">First Name *</label>
              <input
                [id]="idPrefix + '-first'"
                class="form-control"
                maxlength="64"
                [class.is-invalid]="invalid('firstName')"
                [ngModel]="state.firstName"
                (ngModelChange)="update({ firstName: $event })"
              />
            </div>
            <div class="col-md-4 mb-3">
              <label [for]="idPrefix + '-last'" class="form-label fw-semibold">Last Name *</label>
              <input
                [id]="idPrefix + '-last'"
                class="form-control"
                maxlength="64"
                [class.is-invalid]="invalid('lastName')"
                [ngModel]="state.lastName"
                (ngModelChange)="update({ lastName: $event })"
              />
            </div>
            <div class="col-md-4 mb-3">
              <label [for]="idPrefix + '-email'" class="form-label fw-semibold">Email *</label>
              <input
                [id]="idPrefix + '-email'"
                type="email"
                class="form-control"
                maxlength="256"
                [class.is-invalid]="invalid('email')"
                [ngModel]="state.email"
                (ngModelChange)="update({ email: $event })"
              />
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class AppointmentNotifyParalegalComponent {
  @Input({ required: true }) state!: NotifyParalegalState;
  /** Unique DOM-id prefix, e.g. `applicant-paralegal`. */
  @Input() idPrefix = 'notify-paralegal';
  /** Turn on red borders after a blocked Continue. */
  @Input() showErrors = false;
  @Output() stateChange = new EventEmitter<NotifyParalegalState>();

  invalid(field: NotifyParalegalField): boolean {
    return this.showErrors && notifyParalegalErrors(this.state).includes(field);
  }

  update(patch: Partial<NotifyParalegalState>): void {
    this.stateChange.emit({ ...this.state, ...patch });
  }
}
