import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { LocationService } from '../../proxy/locations/location.service';
import type {
  LocationCreateDto,
  LocationUpdateDto,
  LocationWithNavigationPropertiesDto,
} from '../../proxy/locations/models';
import type { AppointmentTypeDto } from '../../proxy/appointment-types/models';
import { IconComponent } from '../../shared/ui/icon/icon.component';

/**
 * The list/nav-props rows carry the full appointment-type M2M as
 * `appointmentTypes` (plural) at runtime; the generated proxy type is stale and
 * only declares the singular `appointmentType`, so we widen it locally rather
 * than regenerating the proxy (the deferred date/time plan owns that refresh).
 */
type LocationRow = LocationWithNavigationPropertiesDto & {
  appointmentTypes?: AppointmentTypeDto[];
};

interface LookupOption {
  id: string;
  name: string;
}

interface LocFormState {
  id: string | null;
  name: string;
  address: string;
  city: string;
  zipCode: string;
  stateId: string;
  parkingFee: number;
  isActive: boolean;
  typeIds: string[];
  concurrencyStamp?: string;
}

/**
 * Internal Scheduling (Prompt 14) -- Locations CRUD re-skinned into the internal
 * shell (ia-table list + ra-modal form) over the existing LocationService. The
 * appointment-type M2M is sent as appointmentTypeIds[] (the proxy create/update
 * types are stale, so the body is built and cast). Delete relies on the server
 * in-use guard (ABP surfaces the LocationInUse message). Standalone + OnPush.
 *
 * Date/time seam: the deferred resolver plan adds a required TimeZoneId IANA
 * select to this form; the field order leaves room for it after State.
 */
@Component({
  selector: 'app-internal-locations',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './internal-locations.component.html',
})
export class InternalLocationsComponent implements OnInit {
  private readonly service = inject(LocationService);
  private readonly toaster = inject(ToasterService);

  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);
  protected readonly rows = signal<LocationRow[]>([]);
  protected readonly states = signal<LookupOption[]>([]);
  protected readonly allTypes = signal<LookupOption[]>([]);
  protected readonly form = signal<LocFormState | null>(null);
  protected readonly confirmDelete = signal<LocationRow | null>(null);

  ngOnInit(): void {
    this.service.getStateLookup({ maxResultCount: 100, skipCount: 0, filter: '' }).subscribe({
      next: (res) =>
        this.states.set(
          (res.items ?? []).map((s) => ({ id: s.id ?? '', name: s.displayName ?? '' })),
        ),
      error: () => undefined,
    });
    this.service
      .getAppointmentTypeLookup({ maxResultCount: 100, skipCount: 0, filter: '' })
      .subscribe({
        next: (res) =>
          this.allTypes.set(
            (res.items ?? []).map((t) => ({ id: t.id ?? '', name: t.displayName ?? '' })),
          ),
        error: () => undefined,
      });
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.service
      .getList({ maxResultCount: 200, skipCount: 0 })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.rows.set((res.items ?? []) as LocationRow[]),
        error: () => this.rows.set([]),
      });
  }

  protected typeNames(row: LocationRow): string[] {
    return (row.appointmentTypes ?? []).map((t) => t.name ?? '').filter((n) => n.length > 0);
  }
  protected stateName(row: LocationRow): string {
    return row.state?.name ?? '';
  }

  // ---- modal ----
  protected openNew(): void {
    this.form.set({
      id: null,
      name: '',
      address: '',
      city: '',
      zipCode: '',
      stateId: '',
      parkingFee: 0,
      isActive: true,
      typeIds: [],
    });
  }
  protected openEdit(row: LocationRow): void {
    const loc = row.location;
    this.form.set({
      id: loc?.id ?? null,
      name: loc?.name ?? '',
      address: loc?.address ?? '',
      city: loc?.city ?? '',
      zipCode: loc?.zipCode ?? '',
      stateId: loc?.stateId ?? '',
      parkingFee: loc?.parkingFee ?? 0,
      isActive: loc?.isActive ?? true,
      typeIds: (row.appointmentTypes ?? []).map((t) => t.id ?? ''),
      concurrencyStamp: loc?.concurrencyStamp,
    });
  }
  protected closeModal(): void {
    if (!this.isBusy()) {
      this.form.set(null);
    }
  }
  protected patch(partial: Partial<LocFormState>): void {
    const f = this.form();
    if (f) {
      this.form.set({ ...f, ...partial });
    }
  }
  protected toggleType(id: string): void {
    const f = this.form();
    if (!f) {
      return;
    }
    const has = f.typeIds.includes(id);
    this.patch({ typeIds: has ? f.typeIds.filter((x) => x !== id) : [...f.typeIds, id] });
  }
  protected isTypeOn(id: string): boolean {
    return this.form()?.typeIds.includes(id) ?? false;
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
    // The proxy create/update types still carry the stale singular
    // appointmentTypeId; send the real appointmentTypeIds[] and cast.
    const body = {
      name: f.name.trim(),
      address: f.address || null,
      city: f.city || null,
      zipCode: f.zipCode || null,
      stateId: f.stateId || null,
      parkingFee: Number(f.parkingFee) || 0,
      isActive: f.isActive,
      appointmentTypeIds: f.typeIds,
    };
    this.isBusy.set(true);
    const req$ = f.id
      ? this.service.update(f.id, {
          ...body,
          concurrencyStamp: f.concurrencyStamp,
        } as unknown as LocationUpdateDto)
      : this.service.create(body as unknown as LocationCreateDto);
    req$.pipe(finalize(() => this.isBusy.set(false))).subscribe({
      next: () => {
        this.toaster.success('Location saved.');
        this.form.set(null);
        this.load();
      },
      error: () => undefined,
    });
  }

  // ---- delete ----
  protected askDelete(row: LocationRow): void {
    this.confirmDelete.set(row);
  }
  protected cancelDelete(): void {
    if (!this.isBusy()) {
      this.confirmDelete.set(null);
    }
  }
  protected confirmDeleteRow(): void {
    const row = this.confirmDelete();
    const id = row?.location?.id;
    if (!id || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.service
      .delete(id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Location deleted.');
          this.confirmDelete.set(null);
          this.load();
        },
        // ABP surfaces the in-use guard message (LocationInUse) itself.
        error: () => this.confirmDelete.set(null),
      });
  }
}
