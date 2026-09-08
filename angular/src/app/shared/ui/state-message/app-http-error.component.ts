import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { performFullLogout } from '../../auth/full-logout';
import { IconName } from '../icon/icon.registry';
import {
  StateMessageAction,
  StateMessageComponent,
  StateMessageTone,
} from '../state-message/state-message.component';

interface ErrorVariant {
  tone: StateMessageTone;
  icon: IconName;
  title: string;
  lead: string;
}

const GENERIC_ERROR: ErrorVariant = {
  tone: 'red',
  icon: 'alert',
  title: 'Something went wrong',
  lead: "We couldn't load this page. This is usually temporary - please try again in a moment.",
};

/** Status -> screen mapping. An unmapped status falls back to the generic error. */
const VARIANTS: Record<number, ErrorVariant> = {
  401: {
    tone: 'amber',
    icon: 'clock',
    title: 'Your session has expired',
    lead: "For your security, you've been signed out after a period of inactivity. Please sign in again to continue.",
  },
  403: {
    tone: 'red',
    icon: 'lock',
    title: "You don't have access",
    lead: "You don't have permission to view this page. If you think this is a mistake, contact your clinic.",
  },
  404: {
    tone: 'blue',
    icon: 'search',
    title: 'Page not found',
    lead: "The page you're looking for doesn't exist or may have moved.",
  },
  500: GENERIC_ERROR,
};

/**
 * Branded HTTP error screen, registered as ABP's `errorScreen.component`
 * (app.config.ts withHttpErrorConfig). On a matched HTTP error, ABP's
 * HttpErrorWrapperComponent creates this and, per its ngAfterViewInit:
 *   - calls `status.set(httpStatus)` on the signal below (it also assigns a
 *     deprecated `errorStatus` plain property, which we ignore),
 *   - assigns `destroy$` (its teardown Subject),
 *   - then runs change detection.
 * The overlay is a fixed, full-screen container that route changes do NOT
 * tear down, so each action navigates AND calls `destroy$.next()` to dismiss.
 */
@Component({
  selector: 'app-http-error',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StateMessageComponent],
  template: `
    <app-state-message
      [tone]="variant().tone"
      [icon]="variant().icon"
      [title]="variant().title"
      [lead]="variant().lead"
      [actions]="actions()"
    />
  `,
})
export class AppHttpErrorComponent {
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);

  /** Set by ABP's wrapper with the HTTP status code. */
  readonly status = signal(0);

  /** Wrapper teardown Subject assigned by ABP; `next()` dismisses the overlay. */
  destroy$?: Subject<void>;

  protected readonly variant = computed<ErrorVariant>(
    () => VARIANTS[this.status()] ?? GENERIC_ERROR,
  );

  protected readonly actions = computed<StateMessageAction[]>(() => {
    const status = this.status();
    if (status === 401) {
      return [{ label: 'Sign in again', icon: 'logout', click: () => this.signIn() }];
    }
    if (status === 403 || status === 404) {
      return [{ label: 'Back to home', icon: 'home', click: () => this.goHome() }];
    }
    return [{ label: 'Try again', icon: 'refresh', click: () => this.retry() }];
  });

  /**
   * Session-timeout CTA. The post-login redirect guard already sends anonymous
   * users to AuthServer before any API call, so a 401 reaching this screen is
   * an expired mid-session token.
   *
   * 1.8b (2026-09-01) -- this is the LIVE TRIGGER for the silent sign-out defect: a 401
   * here means the stored token is gone, which is exactly the state in which
   * `revokeTokenAndLogout()` used to early-return without redirecting. The old code
   * compensated with a chained `AuthService.navigateToLogin()`, which sent the browser to
   * the AuthServer while its SSO cookie was still live -- so the user was silently signed
   * back in. `performFullLogout` now drives the end-session redirect itself, and that flow
   * already lands on `/Account/Login`, so the chained call is gone: it would be a second
   * navigation racing the first.
   */
  private signIn(): void {
    void performFullLogout(this.injector);
  }

  private goHome(): void {
    void this.router.navigateByUrl('/', { onSameUrlNavigation: 'reload' });
    this.destroy$?.next();
  }

  private retry(): void {
    void this.router.navigateByUrl(this.router.url, { onSameUrlNavigation: 'reload' });
    this.destroy$?.next();
  }
}
