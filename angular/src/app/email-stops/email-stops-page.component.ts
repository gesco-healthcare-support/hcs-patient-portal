import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { PacificDatePipe } from '../shared/pipes/pacific-date.pipe';
import { EmailStopConsentWarningComponent } from './email-stop-consent-warning.component';
import type { EmailStopConsentImpactDto, GlobalEmailStopDto } from './email-stops.models';
import { EmailStopsService } from './email-stops.service';

const PAGE_SIZE = 20;

/**
 * Users & Access > Stopped emails. Lists the addresses that get no emails from any practice
 * (except their own password reset), lets staff add one without a case, and turn one back on.
 * Works from the staff site and from inside a practice; the open-request check only sees the
 * current practice, so the staff site says it cannot check.
 */
@Component({
  selector: 'app-email-stops-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, LocalizationPipe, PacificDatePipe, EmailStopConsentWarningComponent],
  templateUrl: './email-stops-page.component.html',
  styleUrl: './email-stops-page.component.scss',
})
export class EmailStopsPageComponent implements OnInit {
  private readonly service = inject(EmailStopsService);
  private readonly configState = inject(ConfigStateService);

  protected readonly onStaffSite = !this.configState.getDeep('currentTenant.id');
  protected readonly items = signal<GlobalEmailStopDto[]>([]);
  protected readonly total = signal(0);
  protected readonly filter = signal('');
  protected readonly skip = signal(0);
  protected readonly busy = signal(false);
  protected readonly newEmail = signal('');
  protected readonly newNote = signal('');
  protected readonly impact = signal<EmailStopConsentImpactDto | null>(null);
  protected readonly resumeTarget = signal<GlobalEmailStopDto | null>(null);
  protected readonly hasPrev = computed(() => this.skip() > 0);
  protected readonly hasNext = computed(() => this.skip() + PAGE_SIZE < this.total());

  ngOnInit(): void {
    this.load();
  }

  protected setFilter(value: string): void {
    this.filter.set(value);
    this.skip.set(0);
    this.load();
  }

  protected page(direction: 1 | -1): void {
    this.skip.set(Math.max(0, this.skip() + direction * PAGE_SIZE));
    this.load();
  }

  protected setEmail(value: string): void {
    this.newEmail.set(value);
    this.impact.set(null);
  }

  /** First click checks for open requests; a warning needs a second, deliberate click. */
  protected stopAddress(): void {
    const email = this.newEmail().trim();
    if (!email || this.busy()) {
      return;
    }
    if (this.impact()?.hasImpact) {
      this.create(email);
      return;
    }
    this.busy.set(true);
    this.service.getGlobalConsentImpact(email).subscribe({
      next: (imp) => {
        if (imp.hasImpact) {
          this.impact.set(imp);
          this.busy.set(false);
        } else {
          this.create(email);
        }
      },
      error: () => this.busy.set(false),
    });
  }

  protected confirmResume(): void {
    const target = this.resumeTarget();
    if (!target) {
      return;
    }
    this.resumeTarget.set(null);
    this.service.delete(target.id).subscribe({ complete: () => this.load() });
  }

  private create(email: string): void {
    this.busy.set(true);
    const note = this.newNote().trim();
    this.service.create({ email, note: note ? note : null }).subscribe({
      next: () => {
        this.busy.set(false);
        this.newEmail.set('');
        this.newNote.set('');
        this.impact.set(null);
        this.load();
      },
      // ABP's HTTP error handler shows the message; only unlock the button here.
      error: () => this.busy.set(false),
    });
  }

  private load(): void {
    this.service
      .getList({
        filter: this.filter().trim() || undefined,
        sorting: 'stoppedAt desc',
        skipCount: this.skip(),
        maxResultCount: PAGE_SIZE,
      })
      .subscribe((res) => {
        this.items.set(res.items);
        this.total.set(res.totalCount);
      });
  }
}
