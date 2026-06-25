import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { BrandingService } from '../shared/branding/branding.service';

/**
 * Phase E (2026-06-25) -- in-office branding editor: a Supervisor / IT Admin (or
 * the office admin) edits THIS office's display name + logo (gated
 * CaseEvaluation.Branding.Edit). Targets the current office (no office id), so it
 * works while a host operator is impersonating the office. After a save it refreshes
 * the boot branding so the shell navbar + tab title update live. The logo preview
 * uses the AllowAnonymous by-subdomain serve, so it renders without an auth header.
 */
@Component({
  selector: 'app-office-branding',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, IconComponent],
  template: `
    <section class="ho-assign">
      <header class="ho-assign__head">
        <h1>Office branding</h1>
        <p>Set your office's display name and logo, shown in the portal and on the sign-in page.</p>
      </header>

      @if (loading()) {
        <p class="ho-assign__muted">Loading...</p>
      } @else {
        <label>
          Display name
          <input
            type="text"
            [(ngModel)]="displayName"
            [disabled]="busy()"
            maxlength="128"
            placeholder="Office display name"
          />
        </label>
        <div>
          <button type="button" [disabled]="busy()" (click)="saveName()">Save name</button>
        </div>

        <div class="ho-assign__logo">
          @if (branding.logoUrl(); as url) {
            <img [src]="url" alt="Office logo" style="max-height: 64px" />
          } @else {
            <span class="ho-assign__muted">No logo uploaded.</span>
          }
          <input
            type="file"
            accept="image/png,image/jpeg"
            [disabled]="busy()"
            (change)="onLogoSelected($event)"
          />
          @if (branding.logoUrl()) {
            <button
              type="button"
              class="ho-assign__unassign"
              [disabled]="busy()"
              (click)="removeLogo()"
            >
              <app-icon name="trash" />
              Remove logo
            </button>
          }
        </div>
      }
    </section>
  `,
})
export class OfficeBrandingComponent {
  protected readonly branding = inject(BrandingService);
  private readonly toaster = inject(ToasterService);

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected displayName = '';

  constructor() {
    this.branding
      .getCurrent()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (dto) => (this.displayName = dto?.displayName ?? ''),
        error: () => undefined,
      });
  }

  protected saveName(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    const value = this.displayName.trim();
    this.branding
      .setDisplayName(value.length ? value : null)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Display name saved.');
          this.branding.load();
        },
        error: () => undefined,
      });
  }

  protected onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.branding
      .uploadLogo(file)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Logo uploaded.');
          input.value = '';
          this.branding.load();
        },
        error: () => {
          input.value = '';
        },
      });
  }

  protected removeLogo(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.branding
      .removeLogo()
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Logo removed.');
          this.branding.load();
        },
        error: () => undefined,
      });
  }
}
