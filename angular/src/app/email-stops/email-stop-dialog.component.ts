import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
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
  /** False from the moment a consent check starts until it answers or fails for the CURRENT scope. */
  protected readonly impactReady = signal(false);

  ngOnInit(): void {
    this.loadImpact();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    // Same rule as the Cancel button: once a save is running the dialog stays open until it answers.
    if (!this.busy()) {
      this.closed.emit();
    }
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
    this.impactReady.set(false);
    const request = ++this.impactRequest;
    this.service.getConsentImpact(this.appointmentId(), this.email(), this.scope()).subscribe({
      next: (impact) => {
        // Ignore an answer for a scope the person has already switched away from.
        if (request === this.impactRequest) {
          this.impact.set(impact);
          this.impactReady.set(true);
        }
      },
      // ABP shows the error; leave the warning empty and let the person decide.
      error: () => {
        if (request === this.impactRequest) {
          this.impactReady.set(true);
        }
      },
    });
  }

  private impactRequest = 0;
}
