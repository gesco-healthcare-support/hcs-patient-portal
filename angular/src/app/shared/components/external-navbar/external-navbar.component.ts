import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  HostListener,
  Input,
  Output,
  inject,
} from '@angular/core';
import { IconComponent } from '../../ui/icon/icon.component';
import type { IconName } from '../../ui/icon/icon.registry';
import { BrandingService } from '../../branding/branding.service';
import { avatarColor } from '../../ui/avatar.util';

/** One row in the notifications dropdown. The feed (BACKEND-CHANGES §G25) is not
 *  built yet, so callers pass [] and the dropdown shows an empty state. */
export interface ExternalNotification {
  id: string;
  tone: 'approved' | 'pending' | 'info' | 'purple' | 'teal' | 'rejected';
  icon: IconName;
  title: string;
  body: string;
  time: string;
  unread: boolean;
}

/**
 * External top navbar (redesign shell). Ported from the prototype `ExtNav`
 * (design_handoff_appointment_portal/components/ext-after.jsx). Reused by every
 * external page. Logo + clinic name are runtime slots; until a BrandingAppService
 * exists they default to the static asset + the ABP tenant name.
 *
 * Usage:
 *   <app-external-navbar
 *     [clinicName]="clinicName" [userName]="name" [roleLabel]="role"
 *     [userEmail]="email" [orgName]="firm" [notifications]="notifs"
 *     (profileClick)="..." (documentsClick)="..." (helpClick)="..." (logoutClick)="..." />
 */
@Component({
  selector: 'app-external-navbar',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './external-navbar.component.html',
  styleUrl: './external-navbar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExternalNavbarComponent {
  /** Per-office branding (logo + display name); overrides the inputs when present. */
  protected readonly branding = inject(BrandingService);
  /** Tenant logo (runtime slot; static placeholder, used when no per-office logo). */
  @Input() logoUrl = 'assets/branding/falkinstein-logo.png';
  /** Clinic / tenant display name (fallback when no per-office display name). */
  @Input() clinicName = 'Appointment Portal';
  @Input() userName = '';
  @Input() roleLabel = '';
  @Input() userEmail = '';
  /** Firm/org name (attorneys) -- shown as a row in the account menu when set. */
  @Input() orgName: string | null = null;
  @Input() notifications: ExternalNotification[] = [];

  @Output() profileClick = new EventEmitter<void>();
  @Output() documentsClick = new EventEmitter<void>();
  @Output() helpClick = new EventEmitter<void>();
  @Output() logoutClick = new EventEmitter<void>();

  /** Which dropdown is open. */
  protected openMenu: 'notif' | 'acct' | null = null;

  /** Tone -> the after.css icon-tint utility class. */
  protected readonly toneTint: Record<ExternalNotification['tone'], string> = {
    approved: 'tint-green',
    pending: 'tint-amber',
    info: 'tint-blue',
    purple: 'tint-purple',
    teal: 'tint-teal',
    rejected: 'tint-red',
  };

  protected get unreadCount(): number {
    return this.notifications.filter((n) => n.unread).length;
  }

  protected get initials(): string {
    // Keep only word-bearing tokens so a firm name's connectors (e.g. the "&"
    // in "Stone & Perez") never become an initial. \p{L}/\p{N} stay correct for
    // non-Latin names; the leading-punctuation strip handles "(Stone".
    const tokens = this.userName
      .trim()
      .split(/\s+/)
      .map((t) => t.replace(/^[^\p{L}\p{N}]+/u, ''))
      .filter((t) => /[\p{L}\p{N}]/u.test(t));
    if (tokens.length === 0) return '?';
    const first = tokens[0][0];
    // A firm's last token is usually a suffix (LLP/Inc/Law), not a meaningful
    // initial -- so for a firm-name avatar use the first two words ("Stone &
    // Perez Defense LLP" -> "SP"). A person keeps first + last ("Marcus James
    // Bennett" -> "MB"). The display name equals orgName only in the firm case.
    const isFirm = !!this.orgName && this.userName.trim() === this.orgName.trim();
    const secondIndex = isFirm ? 1 : tokens.length - 1;
    const second = tokens.length > 1 ? tokens[secondIndex][0] : '';
    return (first + second).toUpperCase();
  }

  /**
   * Deterministic avatar color (ported from after-common.jsx avaColor).
   *
   * <p>#769: this was an inline copy of `ui/avatar.util.ts` -- same palette, same
   * `for...of` code-point loop, same `>>> 0` wrap. Measured identical on 5,000
   * seeds including astral characters, so delegating changes no rendered colour.
   * It is the only one of the six `avatarColor` implementations that could be
   * removed without a visual decision; the other four disagree with this one and
   * with each other, and consolidating them is still open on #769.</p>
   */
  protected get avatarColor(): string {
    return avatarColor(this.userName);
  }

  protected toggle(menu: 'notif' | 'acct'): void {
    this.openMenu = this.openMenu === menu ? null : menu;
  }

  protected close(): void {
    this.openMenu = null;
  }

  protected markAllRead(): void {
    this.notifications = this.notifications.map((n) => ({ ...n, unread: false }));
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.close();
  }
}
