import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { Subject } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { OfficeNamePipe } from '../shared/pipes/office-name.pipe';
import { ManagedTableComponent } from '../shared/components/managed-table/managed-table.component';
import {
  ManagedTableCellDirective,
  ManagedTableRowActionsDirective,
} from '../shared/components/managed-table/managed-table-cell.directive';
import type {
  ManagedTableColumn,
  ManagedTableDataSource,
} from '../shared/components/managed-table/managed-table.models';
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
  imports: [
    CommonModule,
    FormsModule,
    IconComponent,
    OfficeNamePipe,
    ManagedTableComponent,
    ManagedTableCellDirective,
    ManagedTableRowActionsDirective,
  ],
  // The display-name cell used to be styled by .ho-assign__table input[type='text'];
  // inside app-managed-table that selector no longer matches, so the input rule is
  // re-declared component-scoped (the projected template belongs to this view).
  styles: `
    input[type='text'] {
      height: 40px;
      padding: 0 12px;
      border: 1px solid var(--border);
      border-radius: 9px;
      background: #fff;
      font-size: 14px;
      font-family: var(--font);
      color: var(--n-800, #1f2c3d);
    }
    input[type='text']:focus {
      outline: none;
      border-color: var(--blue-300, #93c5fd);
      box-shadow: 0 0 0 3px var(--blue-50);
    }
  `,
  template: `
    <section class="ho-assign">
      <header class="ho-assign__head">
        <!-- UI label: 'Practice Branding' (code: office branding) -->
        <h1>Practice Branding</h1>
        <!-- UI label: 'practice' (code: office) -->
        <p>Set each practice's display name and logo. Changes apply to that practice only.</p>
      </header>

      <!-- UI labels: search 'practice or display name', empty 'No practices yet.' (code: office) -->
      <app-managed-table
        [dataSource]="dataSource"
        [columns]="columns"
        [busy]="busy()"
        [reload$]="reload$"
        [pageSize]="20"
        trackByKey="officeId"
        searchPlaceholder="Search by practice or display name..."
        emptyText="No practices yet."
      >
        <span *managedTableCell="'officeName'; let row">{{ row.officeName | officeName }}</span>
        <!-- UI label: placeholder 'Practice display name' (code: office) -->
        <input
          *managedTableCell="'displayName'; let row"
          type="text"
          [ngModel]="displayNameFor(row)"
          (ngModelChange)="names[row.officeId] = $event"
          [disabled]="busy()"
          maxlength="128"
          placeholder="Practice display name"
        />
        <ng-container *managedTableCell="'hasLogo'; let row">
          @if (row.hasLogo) {
            <span class="ho-assign__muted">Logo set</span>
          } @else {
            <span class="ho-assign__muted">No logo</span>
          }
          <label class="ho-assign__upload" [class.is-disabled]="busy()">
            <app-icon name="upload" [size]="14" />
            {{ row.hasLogo ? 'Replace logo' : 'Upload logo' }}
            <input
              type="file"
              accept="image/png,image/jpeg"
              hidden
              [disabled]="busy()"
              (change)="onLogoSelected(row, $event)"
            />
          </label>
        </ng-container>
        <ng-container *managedTableRowActions="let row">
          <button type="button" class="ho-assign__btn" [disabled]="busy()" (click)="saveName(row)">
            Save name
          </button>
          @if (row.hasLogo) {
            <button
              type="button"
              class="ho-assign__btn ho-assign__unassign"
              [disabled]="busy()"
              (click)="removeLogo(row)"
            >
              <app-icon name="trash" />
              Remove logo
            </button>
          }
        </ng-container>
      </app-managed-table>
    </section>
  `,
})
export class HostBrandingComponent {
  private readonly service = inject(BrandingService);
  private readonly toaster = inject(ToasterService);

  protected readonly busy = signal(false);

  /** Forces the offices table to refetch after a save / upload / remove. */
  protected readonly reload$ = new Subject<void>();

  /** Editable display-name buffer keyed by office id (only holds user edits). */
  protected names: Record<string, string> = {};

  protected readonly columns: ManagedTableColumn[] = [
    // UI label: header 'Practice' (code key: officeName)
    { key: 'officeName', header: 'Practice', sortable: true, sortKey: 'officeName' },
    { key: 'displayName', header: 'Display name', sortable: true, sortKey: 'displayName' },
    { key: 'hasLogo', header: 'Logo' },
  ];

  protected readonly dataSource: ManagedTableDataSource<OfficeBrandingDto> = (query) =>
    this.service.getOfficesPaged(query);

  /**
   * Display name shown in the editable cell: the user's in-progress edit if any,
   * otherwise the office's current saved value. The managed table owns the rows, so
   * the buffer is no longer seeded on load -- it only captures edits.
   */
  protected displayNameFor(row: OfficeBrandingDto): string {
    return this.names[row.officeId] ?? row.displayName ?? '';
  }

  protected saveName(row: OfficeBrandingDto): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    const value = (this.names[row.officeId] ?? row.displayName ?? '').trim();
    this.service
      .setDisplayName(value.length ? value : null, row.officeId)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Display name saved.');
          this.reload$.next();
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
          this.reload$.next();
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
          this.reload$.next();
        },
        error: () => undefined,
      });
  }
}
