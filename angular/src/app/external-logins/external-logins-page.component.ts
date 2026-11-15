import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { roleDisplayLabel } from '../shared/auth/external-user-roles';
import { ExternalLoginsService } from './external-logins.service';
import type { ExternalLoginDto } from './external-logins.models';

const PAGE_SIZE = 20;

/** The status filter's three choices, as the select's string values. */
export type ExternalLoginStatusFilter = '' | 'active' | 'off';

/**
 * Users & Access > Outside logins (2026-11-13). A practice's staff supervisor lists the practice's
 * outside logins (patient, attorney, paralegal, claim examiner) and switches one off or back on.
 * Switching off signs the person out and blocks new sign-ins; it does NOT stop case emails (that is
 * Stopped emails), and the confirm dialog says so. Never lists staff.
 */
@Component({
  selector: 'app-external-logins-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, LocalizationPipe],
  templateUrl: './external-logins-page.component.html',
  styleUrl: './external-logins-page.component.scss',
})
export class ExternalLoginsPageComponent implements OnInit {
  private readonly service = inject(ExternalLoginsService);
  private readonly toaster = inject(ToasterService);
  private readonly localization = inject(LocalizationService);

  protected readonly items = signal<ExternalLoginDto[]>([]);
  protected readonly total = signal(0);
  protected readonly filter = signal('');
  protected readonly status = signal<ExternalLoginStatusFilter>('');
  protected readonly skip = signal(0);
  protected readonly busy = signal(false);
  /** The login the open confirm dialog is about, and the state it would be set to. */
  protected readonly target = signal<{ login: ExternalLoginDto; turnOn: boolean } | null>(null);
  protected readonly hasPrev = computed(() => this.skip() > 0);
  protected readonly hasNext = computed(() => this.skip() + PAGE_SIZE < this.total());

  ngOnInit(): void {
    this.load();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    // Same rule as the Cancel button: once a save is running the dialog stays open until it answers.
    if (this.target() && !this.busy()) {
      this.target.set(null);
    }
  }

  protected roleLabels(login: ExternalLoginDto): string {
    return login.roles.map((r) => roleDisplayLabel(r)).join(', ');
  }

  protected fullName(login: ExternalLoginDto): string {
    return [login.name, login.surname].filter((p) => !!p && p.trim()).join(' ');
  }

  protected setFilter(value: string): void {
    this.filter.set(value);
    this.skip.set(0);
    this.load();
  }

  protected setStatus(value: ExternalLoginStatusFilter): void {
    this.status.set(value);
    this.skip.set(0);
    this.load();
  }

  protected page(direction: 1 | -1): void {
    this.skip.set(Math.max(0, this.skip() + direction * PAGE_SIZE));
    this.load();
  }

  protected ask(login: ExternalLoginDto): void {
    this.target.set({ login, turnOn: !login.isActive });
  }

  protected cancel(): void {
    if (!this.busy()) {
      this.target.set(null);
    }
  }

  protected confirm(): void {
    const t = this.target();
    if (!t || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.service.setActive(t.login.id, { isActive: t.turnOn }).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.target.set(null);
        this.items.update((rows) => rows.map((r) => (r.id === updated.id ? updated : r)));
        const key = t.turnOn ? '::ExternalLogins:Toast:On' : '::ExternalLogins:Toast:Off';
        this.toaster.success(this.localization.instant(key, t.login.email ?? ''));
      },
      // ABP's HTTP error handler shows the message; keep the dialog open and only unlock it.
      error: () => this.busy.set(false),
    });
  }

  private load(): void {
    const status = this.status();
    this.service
      .getList({
        filterText: this.filter().trim() || undefined,
        isActive: status === '' ? null : status === 'active',
        skipCount: this.skip(),
        maxResultCount: PAGE_SIZE,
      })
      .subscribe((res) => {
        this.items.set(res.items);
        this.total.set(res.totalCount);
      });
  }
}
