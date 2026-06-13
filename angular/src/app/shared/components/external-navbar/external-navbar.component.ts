import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  HostListener,
  Input,
  Output,
} from '@angular/core';
import { IconComponent } from '../../ui/icon/icon.component';
import type { IconName } from '../../ui/icon/icon.registry';

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
  /** Tenant logo (runtime slot; static placeholder until BrandingAppService lands). */
  @Input() logoUrl = 'assets/images/header-logo.png';
  /** Clinic / tenant display name (from configState currentTenant.name). */
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
    const parts = this.userName.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    const first = parts[0][0] ?? '';
    const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : '';
    return (first + last).toUpperCase();
  }

  /** Deterministic avatar color (ported from after-common.jsx avaColor). */
  protected get avatarColor(): string {
    const palette = [
      '#055495',
      '#075ca1',
      '#0a4778',
      '#2f7cbf',
      '#1f6e6e',
      '#5b3ea6',
      '#82a52a',
      '#a35a26',
    ];
    let h = 0;
    for (const ch of this.userName) {
      h = (h * 31 + ch.charCodeAt(0)) >>> 0;
    }
    return palette[h % palette.length];
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
