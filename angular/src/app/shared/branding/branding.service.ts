import { inject, Injectable, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { EnvironmentService, RestService } from '@abp/ng.core';

/** Public per-office branding (mirrors the server BrandingDto). */
export interface BrandingDto {
  displayName?: string | null;
  hasLogo: boolean;
  logoUrl?: string | null;
}

/**
 * Phase E (2026-06-25): fetches the current office's branding (display name + logo)
 * from the AllowAnonymous endpoint at boot -- the office is resolved by subdomain, so
 * this works pre-auth. Exposes the result as signals the shell navbars read (OnPush-
 * safe), and sets the browser tab title from the display name. The fetch never blocks
 * boot: an error leaves the signals null and the navbars fall back to the Gesco
 * defaults / the ABP tenant name. The server returns a relative logo path; this
 * resolves it against the runtime API base (do NOT hardcode the host).
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly rest = inject(RestService);
  private readonly environment = inject(EnvironmentService);
  private readonly title = inject(Title);

  private readonly _displayName = signal<string | null>(null);
  private readonly _logoUrl = signal<string | null>(null);

  /** Office display name, or null when unset / host scope. */
  readonly displayName = this._displayName.asReadonly();

  /** Absolute logo URL, or null when the office has no custom logo. */
  readonly logoUrl = this._logoUrl.asReadonly();

  /** Fires the anonymous branding fetch; resolves immediately so boot never waits on it. */
  load(): void {
    this.rest
      .request<void, BrandingDto>({ method: 'GET', url: '/api/app/branding' })
      .subscribe({
        next: (dto) => this.apply(dto),
        error: () => {
          /* leave defaults; branding is best-effort */
        },
      });
  }

  private apply(dto: BrandingDto | null): void {
    const displayName = dto?.displayName?.trim() || null;
    this._displayName.set(displayName);
    this._logoUrl.set(this.toAbsolute(dto?.logoUrl));
    if (displayName) {
      this.title.setTitle(displayName);
    }
  }

  private toAbsolute(relative?: string | null): string | null {
    if (!relative) {
      return null;
    }
    if (/^https?:\/\//i.test(relative)) {
      return relative;
    }
    const base = (this.environment.getApiUrl('default') ?? '').replace(/\/$/, '');
    return `${base}/${relative.replace(/^\//, '')}`;
  }
}
