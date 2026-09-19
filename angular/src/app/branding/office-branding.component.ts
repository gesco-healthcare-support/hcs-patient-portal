import { ChangeDetectionStrategy, Component, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { EnvironmentService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { BrandingDto, BrandingService } from '../shared/branding/branding.service';

/**
 * Phase E (2026-06-25) -- in-office branding editor: a Supervisor / IT Admin (or
 * the office admin) edits THIS office's display name + logo (gated
 * CaseEvaluation.Branding.Edit). Targets the current office (no office id), so it
 * works while a host operator is impersonating the office. After a save it refreshes
 * the boot branding so the shell navbar + tab title update live. The logo preview is
 * fetched via HttpClient (ABP attaches the bearer) so the serve resolves the office
 * from the request tenant -- an <img> tag sends no token, so the anonymous
 * by-subdomain serve 404s when impersonating an office from admin.localhost.
 */
@Component({
  selector: 'app-office-branding',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, IconComponent],
  styles: `
    /* QA #15 obs 8 (2026-07-07): rebuilt on the app design system. Only the
       layout not covered by the global ra/af classes is scoped here. */
    .ra-card {
      max-width: 680px;
      margin: 0 auto;
    }
    .ob-actions {
      margin-top: 14px;
    }
    .ob-logo {
      margin-top: 20px;
      padding-top: 18px;
      border-top: 1px solid var(--border, #e6ebf2);
      display: flex;
      align-items: center;
      gap: 18px;
      flex-wrap: wrap;
    }
    .ob-logo__preview {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 120px;
      min-height: 72px;
      padding: 10px 14px;
      border: 1px solid var(--border, #e6ebf2);
      border-radius: var(--r-md, 11px);
      background: var(--n-25, #fafbfd);
    }
    .ob-logo__preview img {
      max-height: 64px;
      max-width: 220px;
    }
    .ob-logo__empty {
      color: var(--n-400, #9aa6b8);
      font-size: 13px;
    }
    .ob-logo__controls {
      display: inline-flex;
      gap: 10px;
      flex-wrap: wrap;
    }
    .ob-upload {
      cursor: pointer;
    }
    .ob-upload.is-disabled {
      opacity: 0.5;
      pointer-events: none;
    }
  `,
  template: `
    <div class="ia-head">
      <div>
        <h1>Office branding</h1>
        <p>Set your office's display name and logo, shown in the portal and on the sign-in page.</p>
      </div>
    </div>

    @if (loading()) {
      <div class="ia-empty">Loading...</div>
    } @else {
      <div class="ra-card">
        <div class="ra-card__head">
          <span class="ic tint-blue"><app-icon name="edit" [size]="18" /></span>
          <div>
            <h3>Branding</h3>
            <p>Shown in the portal navbar and on the sign-in page.</p>
          </div>
        </div>
        <div class="ra-card__body">
          <div class="ra-field">
            <label>Display name</label>
            <input
              class="ra-input"
              type="text"
              [(ngModel)]="displayName"
              [disabled]="busy()"
              maxlength="128"
              placeholder="Office display name"
            />
          </div>
          <div class="ob-actions">
            <button
              type="button"
              class="af-btn af-btn--primary"
              [disabled]="busy()"
              (click)="saveName()"
            >
              <app-icon name="check" [size]="15" />
              Save name
            </button>
          </div>

          <div class="ob-logo">
            <div class="ob-logo__preview">
              @if (logoPreview(); as url) {
                <img [src]="url" alt="Office logo" />
              } @else {
                <span class="ob-logo__empty">No logo uploaded</span>
              }
            </div>
            <div class="ob-logo__controls">
              <label class="af-btn af-btn--ghost ob-upload" [class.is-disabled]="busy()">
                <app-icon name="upload" [size]="15" />
                {{ hasLogo() ? 'Replace logo' : 'Upload logo' }}
                <input
                  type="file"
                  accept="image/png,image/jpeg"
                  hidden
                  [disabled]="busy()"
                  (change)="onLogoSelected($event)"
                />
              </label>
              @if (hasLogo()) {
                <button
                  type="button"
                  class="af-btn af-btn--ghost"
                  [disabled]="busy()"
                  (click)="removeLogo()"
                >
                  <app-icon name="trash" [size]="15" />
                  Remove logo
                </button>
              }
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class OfficeBrandingComponent implements OnDestroy {
  protected readonly branding = inject(BrandingService);
  private readonly toaster = inject(ToasterService);
  private readonly http = inject(HttpClient);
  private readonly environment = inject(EnvironmentService);

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly hasLogo = signal(false);
  // Authenticated object-URL preview (see the class doc): fetch the logo with the
  // bearer attached so it resolves the impersonated office, then show the blob.
  protected readonly logoPreview = signal<string | null>(null);
  private objectUrl: string | null = null;
  protected displayName = '';

  constructor() {
    this.branding
      .getCurrent()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (dto) => {
          this.displayName = dto?.displayName ?? '';
          this.setLogoState(dto);
        },
        error: () => undefined,
      });
  }

  ngOnDestroy(): void {
    this.revokePreview();
  }

  /** Refresh hasLogo + the authenticated blob preview from a branding dto. */
  private setLogoState(dto: BrandingDto | null): void {
    this.hasLogo.set(!!dto?.hasLogo);
    this.revokePreview();
    this.logoPreview.set(null);
    if (!dto?.hasLogo || !dto.logoUrl) {
      return;
    }
    const base = (this.environment.getApiUrl('default') ?? '').replace(/\/$/, '');
    const url = /^https?:\/\//i.test(dto.logoUrl)
      ? dto.logoUrl
      : `${base}/${dto.logoUrl.replace(/^\//, '')}`;
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        this.objectUrl = URL.createObjectURL(blob);
        this.logoPreview.set(this.objectUrl);
      },
      error: () => this.logoPreview.set(null),
    });
  }

  private revokePreview(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
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
        next: (dto) => {
          this.toaster.success('Logo uploaded.');
          input.value = '';
          this.setLogoState(dto);
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
          this.hasLogo.set(false);
          this.revokePreview();
          this.logoPreview.set(null);
          this.branding.load();
        },
        error: () => undefined,
      });
  }
}
