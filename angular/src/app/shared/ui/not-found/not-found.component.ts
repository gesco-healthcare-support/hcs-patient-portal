import { ChangeDetectionStrategy, Component } from '@angular/core';
import {
  StateMessageAction,
  StateMessageComponent,
} from '../state-message/state-message.component';

/**
 * Catch-all 404 page for unknown client-side routes (the wildcard `**` route).
 * API 404s are handled separately by AppHttpErrorComponent. Renders the shared
 * StateMessageComponent, so it looks identical for external and internal users.
 * "Back to home" targets `/`, which the post-login redirect guard resolves to
 * the external home or the internal dashboard per role.
 */
@Component({
  selector: 'app-not-found',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StateMessageComponent],
  template: `
    <app-state-message
      tone="blue"
      icon="search"
      title="Page not found"
      lead="The page you're looking for doesn't exist or may have moved. Check the link, or head back to your home page."
      [actions]="actions"
    />
  `,
})
export class NotFoundComponent {
  protected readonly actions: StateMessageAction[] = [
    { label: 'Back to home', icon: 'home', routerLink: '/' },
  ];
}
