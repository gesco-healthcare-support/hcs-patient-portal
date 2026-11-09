import { ChangeDetectionStrategy, Component, inject, output, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfigStateService } from '@abp/ng.core';

import { ConfirmationNumberLookupsService } from '../../proxy/appointments/confirmation-number-lookups.service';

/** The four boxes of the form. */
export type NumberRequestField = 'firstName' | 'lastName' | 'dateOfBirth' | 'claimNumber';

/** Where the request stands. Every answer the server gives after the throttle is the same `sent`. */
export type NumberRequestState = 'idle' | 'sending' | 'sent' | 'throttled' | 'failed';

/** The state an HTTP error leaves the form in: the hourly limit has its own message. */
export function numberRequestErrorState(error: unknown): NumberRequestState {
  return error instanceof HttpErrorResponse && error.status === 429 ? 'throttled' : 'failed';
}

/**
 * 2026-11-06 (R3) -- "I don't know the confirmation number", the middle card of a re-evaluation.
 * The booker gives the patient's name, date of birth and a claim number; when they match one earlier
 * case in this office, the server emails its number to the booker's own login. Like forgot-password,
 * the screen says the same thing whatever happened, so it never tells anyone whether a patient, a case
 * or a claim exists. Once the number arrives the booker switches to the number card.
 */
@Component({
  selector: 'app-confirmation-number-request',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './confirmation-number-request.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmationNumberRequestComponent {
  private readonly lookups = inject(ConfirmationNumberLookupsService);
  private readonly config = inject(ConfigStateService);

  /** The booker has the number now and wants the number card. */
  readonly haveNumber = output<void>();

  /** The first exam was done in the old system, so there is nothing to look up: switch cards. */
  readonly beforePortal = output<void>();

  readonly form = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    dateOfBirth: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    claimNumber: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  readonly state = signal<NumberRequestState>('idle');
  readonly attempted = signal(false);

  /** The booker's own login email: the only place the number is ever sent. */
  get loginEmail(): string {
    return this.config.getOne('currentUser')?.email ?? '';
  }

  missing(name: NumberRequestField): boolean {
    const control = this.form.controls[name];
    return this.attempted() && !control.value.trim();
  }

  submit(): void {
    this.attempted.set(true);
    const v = this.form.getRawValue();
    const blank = [v.firstName, v.lastName, v.dateOfBirth, v.claimNumber].some((x) => !x.trim());
    if (blank || this.state() === 'sending') {
      return;
    }
    this.state.set('sending');
    this.lookups
      .request(
        {
          patientFirstName: v.firstName.trim(),
          patientLastName: v.lastName.trim(),
          patientDateOfBirth: v.dateOfBirth,
          claimNumber: v.claimNumber.trim(),
        },
        // The answers are shown here; ABP's full-screen error card would hide the form.
        { skipHandleError: true },
      )
      .subscribe({
        next: () => this.state.set('sent'),
        error: (error: unknown) => this.state.set(numberRequestErrorState(error)),
      });
  }
}
