import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { BrandingService, OfficeBrandingDto } from '../shared/branding/branding.service';

/**
 * Phase E (2026-06-25) -- host-side central per-office branding manager for IT Admin
 * and the host Staff Supervisor (gated CaseEvaluation.Branding). Lists every office
 * and edits each office's display name + logo BY ID, without switching into the
 * office. The rendered logo is verified on the office's own login/shell; this grid
 * shows the upload state (a gated by-id image serve cannot be previewed via <img>).
 */
@Component({
  selector: 'app-host-branding',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, IconComponent],
  template: `
    <section class="ho-assign">
      <header class="ho-assign__head">
        <h1>Office branding</h1>
        <p>Set each office's display name and logo. Changes apply to that office only.</p>
      </header>

      @if (loading()) {
        <p class="ho-assign__muted">Loading offices...</p>
      } @else if (rows().length === 0) {
        <p class="ho-assign__muted">No offices yet.</p>
      } @else {
        <table class="ho-assign__table">
          <thead>
            <tr>
              <th>Office</th>
              <th>Display name</th>
              <th>Logo</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.officeId) {
              <tr>
                <td>{{ row.officeName }}</td>
                <td>
                  <input
                    type="text"
                    [(ngModel)]="names[row.officeId]"
                    [disabled]="busy()"
                    maxlength="128"
                    placeholder="Office display name"
                  />
                </td>
                <td>
                  @if (row.hasLogo) {
                    <span class="ho-assign__muted">Logo set</span>
                  } @else {
                    <span class="ho-assign__muted">No logo</span>
                  }
                  <input
                    type="file"
                    accept="image/png,image/jpeg"
                    [disabled]="busy()"
                    (change)="onLogoSelected(row, $event)"
                  />
                </td>
                <td>
                  <button type="button" [disabled]="busy()" (click)="saveName(row)">Save name</button>
                  @if (row.hasLogo) {
                    <button
                      type="button"
                      class="ho-assign__unassign"
                      [disabled]="busy()"
                      (click)="removeLogo(row)"
                    >
                      <app-icon name="trash" />
                      Remove logo
                    </button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>
  `,
})
export class HostBrandingComponent {
  private readonly service = inject(BrandingService);
  private readonly toaster = inject(ToasterService);

  protected readonly rows = signal<OfficeBrandingDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);

  /** Editable display-name buffer keyed by office id. */
  protected names: Record<string, string> = {};

  constructor() {
    this.reload();
  }

  protected saveName(row: OfficeBrandingDto): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    const value = (this.names[row.officeId] ?? '').trim();
    this.service
      .setDisplayName(value.length ? value : null, row.officeId)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Display name saved.');
          this.reload();
        },
        error: () => undefined,
      });
  }

  protected onLogoSelected(row: OfficeBrandingDto, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.service
      .uploadLogo(file, row.officeId)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Logo uploaded.');
          input.value = '';
          this.reload();
        },
        error: () => {
          input.value = '';
        },
      });
  }

  protected removeLogo(row: OfficeBrandingDto): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.service
      .removeLogo(row.officeId)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Logo removed.');
          this.reload();
        },
        error: () => undefined,
      });
  }

  private reload(): void {
    this.loading.set(true);
    this.service
      .getOffices()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const items = res.items ?? [];
          this.rows.set(items);
          this.names = {};
          for (const item of items) {
            this.names[item.officeId] = item.displayName ?? '';
          }
        },
        error: () => this.rows.set([]),
      });
  }
}
