import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  Input,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { IconComponent } from '../../ui/icon/icon.component';

/** What the staff "New appointment" button can open: `?type=` is the wizard's mode key. */
export type NewAppointmentKind = 'new' | 'reeval';

/** Query params for the booking route. The mode is read once when the wizard opens, so it is chosen here first. */
export function newAppointmentQueryParams(kind: NewAppointmentKind): { type: number } {
  return { type: kind === 'reeval' ? 2 : 1 };
}

/**
 * Staff "New appointment" button with a two-way choice, "New evaluation" or "Re-evaluation".
 *
 * The choice is made BEFORE the booking form opens: the form reads its mode once in its constructor
 * and Angular reuses the component on a query-param change, so an in-page toggle would leave the
 * mode and draft state stale. Escape and a click outside close the menu.
 *
 * 2026-11-06: a disclosure (a button that shows two more buttons), not an ARIA menu. `role="menu"`
 * promises arrow-key navigation, which two plain buttons do not need; Tab reaches them in order.
 * Escape also puts focus back on the trigger, so a keyboard user is not left on a removed button.
 */
@Component({
  selector: 'app-new-appointment-menu',
  standalone: true,
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="nam">
      <button
        #trigger
        type="button"
        [class]="buttonClass"
        aria-controls="nam-choices"
        [attr.aria-expanded]="open()"
        (click)="toggle()"
      >
        <app-icon name="plus" [size]="15" />
        New appointment
      </button>
      @if (open()) {
        <div class="nam__menu" id="nam-choices">
          <button type="button" id="nam-new" (click)="go('new')">
            <b>New evaluation</b>
            <span>The first evaluation of a patient.</span>
          </button>
          <button type="button" id="nam-reeval" (click)="go('reeval')">
            <b>Re-evaluation</b>
            <span>A follow-up to an earlier evaluation.</span>
          </button>
        </div>
      }
    </span>
  `,
  styles: [
    `
      .nam {
        position: relative;
        display: inline-block;
      }
      .nam__menu {
        position: absolute;
        right: 0;
        top: calc(100% + 6px);
        z-index: 50;
        min-width: 250px;
        display: flex;
        flex-direction: column;
        background: #fff;
        border: 1px solid #d0d7de;
        border-radius: 10px;
        box-shadow: 0 8px 24px rgba(0, 0, 0, 0.15);
        padding: 6px;
      }
      .nam__menu button {
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        text-align: left;
        background: none;
        border: 0;
        border-radius: 8px;
        padding: 9px 12px;
        cursor: pointer;
        color: #1f2933;
      }
      .nam__menu button:hover,
      .nam__menu button:focus-visible {
        background: #eef4fb;
      }
      .nam__menu span {
        font-size: 12px;
        color: #52606d;
      }
    `,
  ],
})
export class NewAppointmentMenuComponent {
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);

  @ViewChild('trigger') private trigger?: ElementRef<HTMLButtonElement>;

  /** CSS classes for the trigger, so each page keeps its own button look. */
  @Input() buttonClass = 'ap-btn ap-btn--primary';

  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((v) => !v);
  }

  protected go(kind: NewAppointmentKind): void {
    this.open.set(false);
    void this.router.navigate(['/appointments/request'], {
      queryParams: newAppointmentQueryParams(kind),
    });
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (!this.open()) {
      return;
    }
    this.open.set(false);
    this.trigger?.nativeElement.focus();
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: Event): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }
}
