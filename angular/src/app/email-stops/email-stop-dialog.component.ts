import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { EmailStopConsentWarningComponent } from './email-stop-consent-warning.component';
import { EmailStopScope } from '../proxy/email-stops';
import type { EmailStopConsentImpactDto } from '../proxy/email-stops';
import { EmailStopsService } from '../proxy/email-stops';

/**
 * "Stop emails" dialog on the case card: choose Only this case / All cases, an optional reason,
 * and see the consent warning for the chosen scope. Created only while open, so it loads in
 * ngOnInit.
 */
@Component({
  selector: 'app-email-stop-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, LocalizationPipe, EmailStopConsentWarningComponent],
  templateUrl: './email-stop-dialog.component.html',
  styleUrl: './email-stop-dialog.component.scss',
})
export class EmailStopDialogComponent implements OnInit {
  private readonly service = inject(EmailStopsService);

  readonly appointmentId = input.required<string>();
  readonly email = input.required<string>();
  readonly closed = output<void>();
  readonly stopped = output<void>();

  protected readonly scopes = EmailStopScope;
  protected readonly scope = signal<EmailStopScope>(EmailStopScope.Case);
  protected readonly note = signal('');
  protected readonly impact = signal<EmailStopConsentImpactDto | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    this.loadImpact();
  }

  protected chooseScope(scope: EmailStopScope): void {
    if (scope === this.scope()) {
      return;
    }
    this.scope.set(scope);
    this.loadImpact();
  }

  protected save(): void {
    this.busy.set(true);
    const note = this.note().trim();
    this.service
      .stopForAppointment(this.appointmentId(), {
        email: this.email(),
        scope: this.scope(),
        note: note ? note : null,
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.stopped.emit();
        },
        // The error itself is shown by ABP's HTTP error handler; only unlock the button here.
        error: () => this.busy.set(false),
      });
  }

  private loadImpact(): void {
    this.impact.set(null);
    const request = ++this.impactRequest;
    this.service
      .getConsentImpact(this.appointmentId(), this.email(), this.scope())
      .subscribe((impact) => {
        // Ignore an answer for a scope the person has already switched away from.
        if (request === this.impactRequest) {
          this.impact.set(impact);
        }
      });
  }

  private impactRequest = 0;
}
