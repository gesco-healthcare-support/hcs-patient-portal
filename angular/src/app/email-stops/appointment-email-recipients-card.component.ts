import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { LocalizationPipe, PermissionDirective, PermissionService } from '@abp/ng.core';
import { Observable, concat } from 'rxjs';
import { PacificDatePipe } from '../shared/pipes/pacific-date.pipe';
import { EmailStopDialogComponent } from './email-stop-dialog.component';
import { EMAIL_STOPS_POLICY } from './email-stops.models';
import type { AppointmentEmailRecipientDto } from '../proxy/email-stops';
import { EmailStopsService } from '../proxy/email-stops';
import { kindKey, pillFor, routingNoteKey } from './email-stops.util';

/**
 * "Who gets emails for this case" -- one row per outside address on the case, with a stop /
 * turn-back-on button. Staff-only: hidden and never loaded without CaseEvaluation.EmailStops.
 * Emits the loaded rows so the case page can tag stopped people elsewhere on the page.
 */
@Component({
  selector: 'app-appointment-email-recipients-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe, PermissionDirective, PacificDatePipe, EmailStopDialogComponent],
  templateUrl: './appointment-email-recipients-card.component.html',
  styleUrl: './appointment-email-recipients-card.component.scss',
})
export class AppointmentEmailRecipientsCardComponent {
  private readonly service = inject(EmailStopsService);
  private readonly permission = inject(PermissionService);

  readonly appointmentId = input<string | null>(null);
  readonly recipientsChange = output<AppointmentEmailRecipientDto[]>();

  protected readonly policy = EMAIL_STOPS_POLICY;
  protected readonly items = signal<AppointmentEmailRecipientDto[]>([]);
  protected readonly loaded = signal(false);
  protected readonly stopTarget = signal<string | null>(null);
  protected readonly confirmAll = signal<AppointmentEmailRecipientDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly pillFor = pillFor;
  protected readonly kindKey = kindKey;
  protected readonly noteKey = routingNoteKey;

  constructor() {
    effect(() => {
      const id = this.appointmentId();
      if (id) {
        untracked(() => this.load(id));
      }
    });
  }

  protected stop(r: AppointmentEmailRecipientDto): void {
    this.stopTarget.set(r.email);
  }

  protected turnOn(r: AppointmentEmailRecipientDto): void {
    if (r.allCasesStop) {
      this.confirmAll.set(r);
      return;
    }
    this.resume(r);
  }

  protected resume(r: AppointmentEmailRecipientDto): void {
    const id = this.appointmentId();
    if (!id) {
      return;
    }
    this.confirmAll.set(null);
    const calls: Observable<void>[] = [];
    if (r.caseStop) {
      calls.push(this.service.resumeForAppointment(id, r.email));
    }
    if (r.allCasesStop) {
      calls.push(this.service.delete(r.allCasesStop.id));
    }
    this.busy.set(true);
    concat(...calls).subscribe({
      complete: () => {
        this.busy.set(false);
        this.load(id);
      },
      // ABP's HTTP error handler shows the message; only unlock the buttons here.
      error: () => this.busy.set(false),
    });
  }

  protected onStopped(): void {
    this.stopTarget.set(null);
    const id = this.appointmentId();
    if (id) {
      this.load(id);
    }
  }

  private load(id: string): void {
    if (!this.permission.getGrantedPolicy(EMAIL_STOPS_POLICY)) {
      return;
    }
    this.service.getAppointmentRecipients(id).subscribe((res) => {
      this.items.set(res.items);
      this.loaded.set(true);
      this.recipientsChange.emit(res.items);
    });
  }
}
