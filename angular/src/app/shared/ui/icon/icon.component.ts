import { ChangeDetectionStrategy, Component, Input, OnChanges, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ICON_PATHS, IconName } from './icon.registry';

/** Square px size used when none is supplied, or when the supplied one is unusable. */
const DEFAULT_SIZE = 18;

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
  @Input() size: number = DEFAULT_SIZE;

  /**
   * Accessible label. When provided the icon is exposed to assistive tech
   * (role="img" + aria-label); otherwise it is treated as decorative
   * (aria-hidden="true").
   */
  @Input() label?: string;

  protected markup: SafeHtml | string = '';

  private readonly sanitizer = inject(DomSanitizer);

  ngOnChanges(): void {
    // `Object.hasOwn` rather than `ICON_PATHS[name] ?? ''`: the registry is a plain
    // object literal, so it inherits from Object.prototype and a lookup keyed on an
    // inherited member ('constructor', 'toString', '__proto__') returns a function or
    // object instead of undefined. `??` cannot catch those -- they are not nullish --
    // so they were interpolated into the trusted markup as native-code text. `name`
    // is not merely a template literal in practice: it arrives from server data via
    // DashboardActivityItemDto.icon, cast to IconName unchecked at
    // internal-dashboard.component.ts:291.
    const inner = Object.hasOwn(ICON_PATHS, this.name) ? ICON_PATHS[this.name] : '';

    // `size` is interpolated into two attributes with no escaping, and the `number`
    // annotation is erased at runtime, so a caller binding it from data could close
    // the attribute and add its own (proven in the spec: a string size injected an
    // `onload`). Coerce to a usable positive length instead of trusting the type.
    // Rejects NaN, Infinity, zero and negatives, all of which are meaningless here.
    const parsed = Number(this.size);
    const px = Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_SIZE;

    const a11y = this.label
      ? ` role="img" aria-label="${this.escape(this.label)}"`
      : ' aria-hidden="true"';
    const svg =
      `<svg viewBox="0 0 24 24" width="${px}" height="${px}" ` +
      `fill="none" stroke="currentColor" stroke-width="1.8" ` +
      `stroke-linecap="round" stroke-linejoin="round"${a11y}>${inner}</svg>`;
    // Safe to trust: the shell is a code-owned constant, `inner` can only be an own
    // value of the registry, `px` is provably a finite positive number, and `label`
    // is escaped for a double-quoted attribute below.
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
