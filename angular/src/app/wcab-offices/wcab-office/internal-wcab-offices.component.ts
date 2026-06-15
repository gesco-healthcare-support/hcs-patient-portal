import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { WcabOfficeService } from '../../proxy/wcab-offices/wcab-office.service';
import type { WcabOfficeWithNavigationPropertiesDto } from '../../proxy/wcab-offices/models';
import { IconComponent } from '../../shared/ui/icon/icon.component';

interface WcabFormState {
  id: string | null;
  name: string;
  abbreviation: string;
  // Preserved from the loaded record so the simplified modal does not drop them.
  address: string | null;
  city: string | null;
  zipCode: string | null;
  stateId: string | null;
  isActive: boolean;
  concurrencyStamp?: string;
}

/**
 * Internal Scheduling (Prompt 14) -- WCAB offices CRUD re-skinned into the
 * internal shell (ia-table + ra-modal) over the existing WcabOfficeService. The
 * design simplifies the modal to Name + Code (= Abbreviation); the other
 * persisted fields (address/city/zip/state/isActive) are carried through
 * unchanged on update. Standalone + OnPush.
 */
@Component({
  selector: 'app-internal-wcab-offices',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './internal-wcab-offices.component.html',
})
export class InternalWcabOfficesComponent implements OnInit {
  private readonly service = inject(WcabOfficeService);
  private readonly toaster = inject(ToasterService);

  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);
  protected readonly rows = signal<WcabOfficeWithNavigationPropertiesDto[]>([]);
  protected readonly form = signal<WcabFormState | null>(null);
  protected readonly confirmDelete = signal<WcabOfficeWithNavigationPropertiesDto | null>(null);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.service
      .getList({ maxResultCount: 200, skipCount: 0 })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.rows.set(res.items ?? []),
        error: () => this.rows.set([]),
      });
  }

  // ---- modal ----
  protected openNew(): void {
    this.form.set({
      id: null,
      name: '',
      abbreviation: '',
      address: null,
      city: null,
      zipCode: null,
      stateId: null,
      isActive: true,
    });
  }
  protected openEdit(row: WcabOfficeWithNavigationPropertiesDto): void {
    const o = row.wcabOffice;
    this.form.set({
      id: o?.id ?? null,
      name: o?.name ?? '',
      abbreviation: o?.abbreviation ?? '',
      address: o?.address ?? null,
      city: o?.city ?? null,
      zipCode: o?.zipCode ?? null,
      stateId: o?.stateId ?? null,
      isActive: o?.isActive ?? true,
      concurrencyStamp: o?.concurrencyStamp,
    });
  }
  protected closeModal(): void {
    if (!this.isBusy()) {
      this.form.set(null);
    }
  }
  protected patch(partial: Partial<WcabFormState>): void {
    const f = this.form();
    if (f) {
      this.form.set({ ...f, ...partial });
    }
  }

  protected save(): void {
    const f = this.form();
    if (!f || this.isBusy()) {
      return;
    }
    if (!f.name.trim()) {
      this.toaster.warn('Name is required.');
      return;
    }
    if (!f.abbreviation.trim()) {
      this.toaster.warn('Code is required.');
      return;
    }
    const body = {
      name: f.name.trim(),
      abbreviation: f.abbreviation.trim(),
      address: f.address,
      city: f.city,
      zipCode: f.zipCode,
      stateId: f.stateId,
      isActive: f.isActive,
    };
    this.isBusy.set(true);
    const req$ = f.id
      ? this.service.update(f.id, { ...body, concurrencyStamp: f.concurrencyStamp })
      : this.service.create(body);
    req$.pipe(finalize(() => this.isBusy.set(false))).subscribe({
      next: () => {
        this.toaster.success('WCAB office saved.');
        this.form.set(null);
        this.load();
      },
      error: () => undefined,
    });
  }

  // ---- delete ----
  protected askDelete(row: WcabOfficeWithNavigationPropertiesDto): void {
    this.confirmDelete.set(row);
  }
  protected cancelDelete(): void {
    if (!this.isBusy()) {
      this.confirmDelete.set(null);
    }
  }
  protected confirmDeleteRow(): void {
    const id = this.confirmDelete()?.wcabOffice?.id;
    if (!id || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.service
      .delete(id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Office deleted.');
          this.confirmDelete.set(null);
          this.load();
        },
        error: () => this.confirmDelete.set(null),
      });
  }
}
