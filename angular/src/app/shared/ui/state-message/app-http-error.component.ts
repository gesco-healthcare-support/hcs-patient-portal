import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
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

/**
 * Status -> screen mapping. 401 (session-timeout) is wired in the next slice;
 * until then an unmapped status falls back to the generic error variant.
 */
const VARIANTS: Record<number, ErrorVariant> = {
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

  /** Set by ABP's wrapper with the HTTP status code. */
  readonly status = signal(0);

  /** Wrapper teardown Subject assigned by ABP; `next()` dismisses the overlay. */
  destroy$?: Subject<void>;

  protected readonly variant = computed<ErrorVariant>(
    () => VARIANTS[this.status()] ?? GENERIC_ERROR,
  );

  protected readonly actions = computed<StateMessageAction[]>(() => {
    const status = this.status();
    if (status === 403 || status === 404) {
      return [{ label: 'Back to home', icon: 'home', click: () => this.goHome() }];
    }
    return [{ label: 'Try again', icon: 'refresh', click: () => this.retry() }];
  });

  private goHome(): void {
    void this.router.navigateByUrl('/', { onSameUrlNavigation: 'reload' });
    this.destroy$?.next();
  }

  private retry(): void {
    void this.router.navigateByUrl(this.router.url, { onSameUrlNavigation: 'reload' });
    this.destroy$?.next();
  }
}
