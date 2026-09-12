import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { OfflineDetectionService } from '../../services/offline-detection.service';
import {
  StateMessageAction,
  StateMessageComponent,
} from '../state-message/state-message.component';

/**
 * Full-screen offline overlay. AppComponent renders this while
 * OfflineDetectionService reports no connectivity; it auto-dismisses when the
 * signal flips back online. Retry re-reads connectivity in case the `online`
 * event was missed.
 */
@Component({
  selector: 'app-offline-overlay',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StateMessageComponent],
  template: `
    <app-state-message
      tone="amber"
      icon="alert"
      title="You're offline"
      lead="We can't reach the clinic portal right now. Check your internet connection and try again."
      [actions]="actions"
    />
  `,
  styles: `
    :host {
      position: fixed;
      inset: 0;
      z-index: 1080;
    }
  `,
})
export class OfflineOverlayComponent {
  private readonly offlineDetection = inject(OfflineDetectionService);

  protected readonly actions: StateMessageAction[] = [
    { label: 'Retry', icon: 'refresh', click: () => this.offlineDetection.refresh() },
  ];
}
