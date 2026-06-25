import { ChangeDetectionStrategy, Component, Input, OnChanges, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ICON_PATHS, IconName } from './icon.registry';

/**
 * Inline line-icon renderer. Wraps a name from {@link ICON_PATHS} in the shared
 * <svg> shell (currentColor stroke, 1.8 weight) ported from the design handoff.
 *
 * Usage: `<app-icon name="bell" [size]="20" />` (decorative) or
 * `<app-icon name="logout" label="Sign out" />` (exposed to assistive tech).
 */
@Component({
  selector: 'app-icon',
  standalone: true,
  template: `
    <span class="app-icon" [innerHTML]="markup"></span>
  `,
  styles: `
    .app-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      line-height: 0;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IconComponent implements OnChanges {
  /** Icon name from the design-handoff set. */
  @Input({ required: true }) name!: IconName;

  /** Square size in px (width === height). Default 18, matching the prototype. */
  @Input() size = 18;

  /**
   * Accessible label. When provided the icon is exposed to assistive tech
   * (role="img" + aria-label); otherwise it is treated as decorative
   * (aria-hidden="true").
   */
  @Input() label?: string;

  protected markup: SafeHtml | string = '';

  private readonly sanitizer = inject(DomSanitizer);

  ngOnChanges(): void {
    const inner = ICON_PATHS[this.name] ?? '';
    const a11y = this.label
      ? ` role="img" aria-label="${this.escape(this.label)}"`
      : ' aria-hidden="true"';
    const svg =
      `<svg viewBox="0 0 24 24" width="${this.size}" height="${this.size}" ` +
      `fill="none" stroke="currentColor" stroke-width="1.8" ` +
      `stroke-linecap="round" stroke-linejoin="round"${a11y}>${inner}</svg>`;
    // The <svg> shell and every inner fragment are static, code-owned constants
    // (no user input), so trusting this markup is safe. `label` is the only
    // dynamic value and is HTML-escaped above before interpolation.
    this.markup = this.sanitizer.bypassSecurityTrustHtml(svg);
  }

  private escape(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }
}
