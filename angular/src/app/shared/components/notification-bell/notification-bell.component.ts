import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { Subscription, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { AppNotificationService } from '../../../proxy/notifications/app-notification.service';
import { AppNotificationType } from '../../../proxy/notifications/app-notification-type.enum';
import type { AppNotificationDto } from '../../../proxy/notifications/models';
import { IconComponent } from '../../ui/icon/icon.component';
import type { IconName } from '../../ui/icon/icon.registry';

/**
 * QA item 7: the internal-shell notification bell. Shows an unread badge polled
 * on an interval (reusing the InternalNavBadgeService shape), and a dropdown of
 * the recent notifications fetched on open. Clicking a row marks it read and
 * deep-links to the case; "Mark all read" clears the badge. Self-contained so the
 * shared shell only needs a single tag. Renders only inside the internal shell
 * (office scope), so no permission gate is needed -- the app service still filters
 * to the caller's own rows.
 */
@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="nb">
      @if (open()) {
        <div class="nb__clickaway" (click)="close()"></div>
      }
      <button
        class="nb__btn"
        type="button"
        aria-label="Notifications"
        [attr.aria-expanded]="open()"
        (click)="toggle()"
      >
        <app-icon name="bell" [size]="18" />
        @if (unread() > 0) {
          <span class="nb__badge">{{ unread() > 99 ? '99+' : unread() }}</span>
        }
      </button>

      @if (open()) {
        <div class="nb__pop">
          <div class="nb__head">
            <b>Notifications</b>
            @if (unread() > 0) {
              <button type="button" class="nb__all" (click)="markAll()">Mark all read</button>
            }
          </div>
          <div class="nb__list">
            @if (loading()) {
              <div class="nb__empty">Loading...</div>
            } @else {
              @for (n of items(); track n.id) {
                <button
                  type="button"
                  class="nb__row"
                  [class.unread]="!n.isRead"
                  (click)="onClick(n)"
                >
                  <span class="nb__ic {{ tint(n.notificationType) }}">
                    <app-icon [name]="icon(n.notificationType)" [size]="15" />
                  </span>
                  <span class="nb__tx">
                    <b>{{ n.title }}</b>
                    <span class="nb__body">{{ n.body }}</span>
                    <span class="nb__time">{{ timeAgo(n.creationTime) }}</span>
                  </span>
                  @if (!n.isRead) {
                    <span class="nb__dot" aria-hidden="true"></span>
                  }
                </button>
              } @empty {
                <div class="nb__empty">
                  <app-icon name="inbox" [size]="20" />
                  <span>No notifications yet.</span>
                </div>
              }
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .nb {
      position: relative;
    }
    .nb__clickaway {
      position: fixed;
      inset: 0;
      z-index: 40;
    }
    .nb__btn {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 38px;
      height: 38px;
      border-radius: 10px;
      border: 1px solid var(--border);
      background: #fff;
      color: var(--n-700);
      cursor: pointer;
    }
    .nb__btn:hover {
      background: var(--n-50);
    }
    .nb__badge {
      position: absolute;
      top: -6px;
      right: -6px;
      min-width: 18px;
      height: 18px;
      padding: 0 5px;
      border-radius: 9px;
      background: var(--st-rejected-dot, #dc2626);
      color: #fff;
      font-size: 11px;
      font-weight: 700;
      line-height: 18px;
      text-align: center;
    }
    .nb__pop {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      width: 360px;
      max-width: 90vw;
      background: #fff;
      border: 1px solid var(--border);
      border-radius: var(--r-lg, 12px);
      box-shadow: 0 12px 32px rgba(0, 0, 0, 0.14);
      z-index: 50;
      overflow: hidden;
    }
    .nb__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 14px;
      border-bottom: 1px solid var(--border);
    }
    .nb__all {
      border: 0;
      background: none;
      color: var(--blue-700, #1d4ed8);
      font-size: 12px;
      font-weight: 600;
      cursor: pointer;
    }
    .nb__list {
      max-height: 380px;
      overflow-y: auto;
    }
    .nb__row {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      width: 100%;
      padding: 11px 14px;
      border: 0;
      border-bottom: 1px solid var(--n-50, #f3f4f6);
      background: #fff;
      text-align: left;
      cursor: pointer;
    }
    .nb__row:hover {
      background: var(--n-50, #f9fafb);
    }
    .nb__row.unread {
      background: #f5f8ff;
    }
    .nb__ic {
      flex: none;
      width: 30px;
      height: 30px;
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .nb__tx {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
      flex: 1;
    }
    .nb__tx b {
      font-size: 13px;
      color: var(--n-900);
    }
    .nb__body {
      font-size: 12px;
      color: var(--n-600);
    }
    .nb__time {
      font-size: 11px;
      color: var(--n-400);
    }
    .nb__dot {
      flex: none;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--blue-500, #3b82f6);
      margin-top: 6px;
    }
    .nb__empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      padding: 28px 14px;
      color: var(--n-400);
      font-size: 13px;
    }
    .tint-blue {
      background: #e6efff;
      color: #1d4ed8;
    }
    .tint-amber {
      background: #fef3e2;
      color: #b45309;
    }
    .tint-purple {
      background: #f0e9fb;
      color: #7c3aed;
    }
  `,
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  private static readonly PollIntervalMs = 60_000;
  private static readonly ListSize = 10;

  private readonly service = inject(AppNotificationService);
  private readonly router = inject(Router);

  protected readonly open = signal(false);
  protected readonly unread = signal(0);
  protected readonly items = signal<AppNotificationDto[]>([]);
  protected readonly loading = signal(false);

  private pollSubscription: Subscription | null = null;

  ngOnInit(): void {
    // Poll the unread count on an interval (mirrors InternalNavBadgeService).
    // A failed tick keeps the last value; the next tick retries.
    this.pollSubscription = timer(0, NotificationBellComponent.PollIntervalMs)
      .pipe(switchMap(() => this.service.getMyUnreadCount()))
      .subscribe({
        next: (count) => this.unread.set(Math.max(0, Math.floor(Number(count) || 0))),
        error: () => undefined,
      });
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
    this.pollSubscription = null;
  }

  protected toggle(): void {
    const next = !this.open();
    this.open.set(next);
    if (next) {
      this.loadList();
    }
  }

  protected close(): void {
    this.open.set(false);
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.close();
  }

  private loadList(): void {
    this.loading.set(true);
    this.service
      .getMyNotifications({ maxResultCount: NotificationBellComponent.ListSize, skipCount: 0 })
      .subscribe({
        next: (result) => {
          this.items.set(result.items ?? []);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  protected onClick(n: AppNotificationDto): void {
    if (!n.isRead && n.id) {
      this.service.markRead(n.id).subscribe({ next: () => undefined, error: () => undefined });
      this.items.update((list) => list.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
      this.unread.update((c) => Math.max(0, c - 1));
    }
    this.close();
    if (n.url) {
      void this.router.navigateByUrl(n.url);
    }
  }

  protected markAll(): void {
    this.service.markAllRead().subscribe({ next: () => undefined, error: () => undefined });
    this.items.update((list) => list.map((x) => ({ ...x, isRead: true })));
    this.unread.set(0);
  }

  protected icon(type: AppNotificationType): IconName {
    switch (type) {
      case AppNotificationType.AppointmentRequested:
        return 'calendar';
      case AppNotificationType.ChangeRequestSubmitted:
      case AppNotificationType.InfoRequestResubmitted:
        return 'refresh';
      case AppNotificationType.QuerySubmitted:
        return 'help';
      case AppNotificationType.DocumentUploaded:
        return 'doc';
      default:
        return 'bell';
    }
  }

  protected tint(type: AppNotificationType): string {
    switch (type) {
      case AppNotificationType.AppointmentRequested:
      case AppNotificationType.DocumentUploaded:
        return 'tint-blue';
      case AppNotificationType.ChangeRequestSubmitted:
      case AppNotificationType.InfoRequestResubmitted:
        return 'tint-amber';
      case AppNotificationType.QuerySubmitted:
        return 'tint-purple';
      default:
        return 'tint-blue';
    }
  }

  protected timeAgo(iso?: string): string {
    if (!iso) {
      return '';
    }
    const minutes = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 60000));
    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  }
}
