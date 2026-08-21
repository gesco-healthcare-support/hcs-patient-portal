import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

/**
 * Shimmer placeholder primitive (the `.sk` block from the design handoff).
 * Compose several to build a page's loading skeleton. The shimmer animation is
 * disabled under prefers-reduced-motion via the global `.sk` styles. The host
 * uses display:contents so the span participates directly in the parent layout
 * (e.g. a flex skeleton row).
 */
@Component({
  selector: 'app-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="sk"
      [class.sk--circle]="circle"
      [style.width]="width"
      [style.height]="height"
      [style.border-radius]="radius"
    ></span>
  `,
  styles: `
    :host {
      display: contents;
    }
  `,
})
export class SkeletonComponent {
  /** CSS width (e.g. '100%', '90px'). */
  @Input() width = '100%';
  /** CSS height (e.g. '14px'). */
  @Input() height = '14px';
  /** Optional explicit border-radius; falls back to the default or circle. */
  @Input() radius?: string;
  /** Render as a circle (overrides radius). */
  @Input() circle = false;
}
