import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import type { DoctorAvailabilityWithNavigationPropertiesDto } from '../../proxy/doctor-availabilities/models';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import {
  buildWeekColumns,
  capacityLabel,
  formatWeekRange,
  weekDatesFor,
  type GridSlot,
  type SlotStatusKey,
  type WeekDayColumn,
} from './avail-grid.util';

/** The most a slot may admit from this editor; mirrors DoctorAvailabilityConsts.MaxCapacity. */
export const MAX_SLOT_CAPACITY = 50;

/** The capacity dialog: one slot, or every slot one location has on one day. */
interface CapacityEdit {
  kind: 'slot' | 'day';
  title: string;
  detail: string;
  locationId: string | null;
  slots: { id: string; concurrencyStamp: string }[];
}

type StatusFilter = 'all' | 'busy' | SlotStatusKey;
interface LocationOption {
  id: string;
  name: string;
}

/**
 * Internal Scheduling (Prompt 14) -- doctor availabilities, re-skinned into the
 * internal shell as a Week-grid <-> Table view over the existing
 * DoctorAvailabilityService. Per-slot delete uses the FK-protected single
 * delete; per-day delete uses the partial delete-by-date (booked/reserved kept)
 * and surfaces how many were skipped. Standalone + OnPush + signals; the grid
 * math lives in avail-grid.util. All times are naive clinic-local for now (the
 * deferred date/time plan adds the clinic-timezone label).
 */
@Component({
  selector: 'app-internal-availabilities',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './internal-availabilities.component.html',
})
export class InternalAvailabilitiesComponent implements OnInit {
  private readonly service = inject(DoctorAvailabilityService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly permissions = inject(PermissionService);

  /** 2026-10-30 -- the capacity controls need the same permission the server demands. */
  protected readonly canEditCapacity = this.permissions.getGrantedPolicy(
    'CaseEvaluation.DoctorAvailabilities.Edit',
  );
  protected readonly maxCapacity = MAX_SLOT_CAPACITY;

  // Anchor "today" captured once so week math + labels stay stable per visit.
  private readonly anchor = new Date();

  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);
  protected readonly locations = signal<LocationOption[]>([]);
  protected readonly locationId = signal<string>('');
  protected readonly statusFilter = signal<StatusFilter>('all');
  protected readonly view = signal<'grid' | 'table'>('grid');
  protected readonly weekOffset = signal(0);
  protected readonly rows = signal<DoctorAvailabilityWithNavigationPropertiesDto[]>([]);
  /** #2 -- booked/reserved patient names keyed by slot id (for the grid chips). */
  protected readonly patientNames = signal<Record<string, string[]>>({});
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());
  protected readonly confirmDay = signal<WeekDayColumn | null>(null);
  protected readonly capacityEdit = signal<CapacityEdit | null>(null);
  protected readonly capacityValue = signal<number | null>(null);
  /** The server's refusal (which slot, how many active appointments), shown in the dialog. */
  protected readonly capacityError = signal('');

  protected readonly weekDates = computed(() => weekDatesFor(this.anchor, this.weekOffset()));
  protected readonly weekRange = computed(() => formatWeekRange(this.weekDates()));
  protected readonly columns = computed(() =>
    buildWeekColumns(this.rows(), this.weekDates(), this.patientNames()),
  );

  /** Columns with slots narrowed to the status filter (counts stay complete). */
  protected readonly displayColumns = computed(() => {
    const filter = this.statusFilter();
    const cols = this.columns();
    if (filter === 'all') {
      return cols;
    }
    if (filter === 'busy') {
      // R2-3: everything not open -- hides the available flood that makes a
      // full work day of 15-20min slots overflow the week grid.
      return cols.map((c) => ({
        ...c,
        slots: c.slots.filter((s) => s.statusKey !== 'available'),
      }));
    }
    return cols.map((c) => ({ ...c, slots: c.slots.filter((s) => s.statusKey === filter) }));
  });

  /** Table view shows only days that have slots. */
  protected readonly tableColumns = computed(() => this.columns().filter((c) => c.total > 0));

  protected readonly selectedLocationName = computed(
    () => this.locations().find((l) => l.id === this.locationId())?.name ?? '',
  );

  ngOnInit(): void {
    this.service.getLocationLookup({ maxResultCount: 100, skipCount: 0, filter: '' }).subscribe({
      next: (res) => {
        const opts = (res.items ?? []).map((l) => ({ id: l.id ?? '', name: l.displayName ?? '' }));
        this.locations.set(opts);
        // Default to "All locations" (empty sentinel, already the initial value) so
        // staff see the doctor's whole schedule at once instead of one location.
        this.locationId.set('');
        if (opts.length > 0) {
          this.loadWeek();
        } else {
          this.loading.set(false);
        }
      },
      error: () => this.loading.set(false),
    });
  }

  private loadWeek(): void {
    // Empty locationId means "All locations": omit it so the backend (which
    // WhereIfs on a non-empty locationId) returns every location's slots.
    const locationId = this.locationId();
    this.loading.set(true);
    const week = this.weekDates();
    const min = `${this.toIso(week[0])}T00:00:00`;
    const max = `${this.toIso(week.at(-1)!)}T23:59:59`;
    this.service
      .getList({
        locationId: locationId || undefined,
        availableDateMin: min,
        availableDateMax: max,
        maxResultCount: 500,
        skipCount: 0,
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const items = res.items ?? [];
          this.rows.set(items);
          this.loadPatientNames(items);
        },
        error: () => {
          this.rows.set([]);
          this.patientNames.set({});
        },
      });
  }

  /**
   * #2 (2026-06-19) -- fetch the booked/reserved patient names for the loaded
   * week's slots in one bulk call, keyed by slot id for the grid chips. Runs
   * after getList so the grid paints immediately and chips fill in when the names
   * arrive; only slots with a non-terminal appointment come back.
   */
  private loadPatientNames(items: DoctorAvailabilityWithNavigationPropertiesDto[]): void {
    this.patientNames.set({});
    const slotIds = items.map((it) => it.doctorAvailability?.id).filter((id): id is string => !!id);
    if (slotIds.length === 0) {
      return;
    }
    this.service.getSlotPatientNames(slotIds).subscribe({
      next: (names) => {
        const map: Record<string, string[]> = {};
        for (const row of names) {
          if (row.slotId) {
            map[row.slotId] = row.names ?? [];
          }
        }
        this.patientNames.set(map);
      },
      error: () => this.patientNames.set({}),
    });
  }

  private toIso(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  // ---- toolbar ----
  protected onLocationChange(id: string): void {
    this.locationId.set(id);
    this.loadWeek();
  }
  protected changeWeek(delta: number): void {
    this.weekOffset.set(this.weekOffset() + delta);
    this.loadWeek();
  }
  protected setView(v: 'grid' | 'table'): void {
    this.view.set(v);
  }
  protected toggleExpand(iso: string): void {
    const next = new Set(this.expanded());
    if (next.has(iso)) {
      next.delete(iso);
    } else {
      next.add(iso);
    }
    this.expanded.set(next);
  }
  protected goGenerate(): void {
    void this.router.navigateByUrl('/doctor-management/doctor-availabilities/generate');
  }

  // ---- deletes ----
  protected deleteSlot(slotId: string): void {
    if (this.isBusy() || !slotId) {
      return;
    }
    this.isBusy.set(true);
    this.service
      .delete(slotId)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Slot deleted.');
          this.loadWeek();
        },
        // ABP surfaces the FK-protection message (booked/reserved slot) itself.
        error: () => undefined,
      });
  }

  protected askDeleteDay(col: WeekDayColumn): void {
    this.confirmDay.set(col);
  }
  /**
   * Sweep #641: the delete-day confirmation could only be dismissed with the mouse. Routed
   * through cancelDeleteDay so the isBusy guard is inherited rather than reimplemented -- a
   * delete in flight must not be dismissable.
   */
  @HostListener('document:keydown.escape')
  protected onEscapeKey(): void {
    if (this.confirmDay()) {
      this.cancelDeleteDay();
    }
    if (this.capacityEdit()) {
      this.cancelCapacity();
    }
  }

  @ViewChild('scrim') private readonly scrim?: ElementRef<HTMLElement>;

  /**
   * Closes the modal when a click lands on its backdrop. This replaced the backdrop's own (click)
   * binding, which was mouse-only; Escape is the keyboard path (onEscapeKey above). A click
   * inside the dialog targets the dialog or its content, never the backdrop element, so it leaves
   * the modal open.
   */
  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (event.target === this.scrim?.nativeElement) {
      this.cancelDeleteDay();
    }
    if (event.target === this.capacityScrim?.nativeElement) {
      this.cancelCapacity();
    }
  }

  @ViewChild('capacityScrim') private readonly capacityScrim?: ElementRef<HTMLElement>;

  // ---- capacity (2026-10-30) ----
  /** Change one slot's capacity. 0 closes it to new requests. */
  protected openSlotCapacity(slot: GridSlot, col: WeekDayColumn): void {
    this.openCapacity(
      {
        kind: 'slot',
        title: `Change capacity: ${slot.timeLabel}`,
        detail: `${this.dayLabel(col)}. Currently ${capacityLabel(slot)}.`,
        locationId: slot.locationId || null,
        slots: [{ id: slot.id, concurrencyStamp: slot.concurrencyStamp }],
      },
      slot.capacity,
    );
  }

  /**
   * One capacity for every slot the SELECTED location has that day. The slots come from the full
   * column (not the status-filtered one) and are narrowed to the selected location, and the
   * location travels with the request, so another location's slots on the same day are never
   * changed. Hidden in the All-locations view, and guarded here as well.
   */
  protected openDayCapacity(col: WeekDayColumn): void {
    const locationId = this.locationId();
    const full = this.columns().find((c) => c.iso === col.iso);
    const slots = (full?.slots ?? []).filter((s) => s.locationId === locationId);
    if (!locationId || slots.length === 0) {
      return;
    }
    this.openCapacity(
      {
        kind: 'day',
        title: `Set capacity for ${this.dayLabel(col)}`,
        detail: `All ${slots.length} slot(s) at ${this.selectedLocationName()} that day get the same capacity.`,
        locationId,
        slots: slots.map((s) => ({ id: s.id, concurrencyStamp: s.concurrencyStamp })),
      },
      slots[0].capacity,
    );
  }

  private openCapacity(edit: CapacityEdit, current: number): void {
    this.capacityError.set('');
    this.capacityValue.set(current);
    this.capacityEdit.set(edit);
  }

  protected cancelCapacity(): void {
    if (!this.isBusy()) {
      this.capacityEdit.set(null);
    }
  }

  protected capacityValid(): boolean {
    const v = this.capacityValue();
    return v != null && Number.isInteger(v) && v >= 0 && v <= MAX_SLOT_CAPACITY;
  }

  protected saveCapacity(): void {
    const edit = this.capacityEdit();
    const capacity = this.capacityValue();
    if (!edit || this.isBusy() || !this.capacityValid() || capacity == null) {
      return;
    }
    this.isBusy.set(true);
    this.capacityError.set('');
    this.service
      .setCapacity(
        { slots: edit.slots, capacity, locationId: edit.locationId ?? undefined },
        // The dialog shows the refusal itself (which slot, how many booked), so ABP's generic
        // error modal is skipped.
        { skipHandleError: true },
      )
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: (res) => {
          const n = res.updatedCount ?? edit.slots.length;
          this.toaster.success(
            capacity === 0
              ? `${n} slot(s) closed to new requests.`
              : `Capacity set to ${capacity} for ${n} slot(s).`,
          );
          this.capacityEdit.set(null);
          this.loadWeek();
        },
        error: (err: { status?: number; error?: { error?: { message?: string } } }) => {
          this.capacityError.set(
            err?.status === 409
              ? 'Someone changed these slots after the schedule loaded. Close this, reload the week and try again.'
              : (err?.error?.error?.message ??
                  'The capacity could not be changed. Nothing was saved.'),
          );
        },
      });
  }

  private dayLabel(col: WeekDayColumn): string {
    const [y, m, d] = col.iso.split('-').map(Number);
    return new Date(y, m - 1, d).toLocaleDateString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  }

  protected cancelDeleteDay(): void {
    if (!this.isBusy()) {
      this.confirmDay.set(null);
    }
  }
  protected confirmDeleteDay(): void {
    const col = this.confirmDay();
    // Per-day delete targets ONE location's slots; the button is hidden in the
    // "All locations" view (empty locationId), so guard the handler too.
    if (!col || this.isBusy() || !this.locationId()) {
      return;
    }
    this.isBusy.set(true);
    this.service
      .deleteByDate({ locationId: this.locationId(), availableDate: `${col.iso}T00:00:00` })
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: (res) => {
          const deleted = res.deletedCount ?? 0;
          const skipped = res.skippedSlotIds?.length ?? 0;
          this.toaster.success(
            skipped > 0
              ? `${deleted} slot(s) deleted; ${skipped} kept (booked or reserved).`
              : `${deleted} slot(s) deleted.`,
          );
          this.confirmDay.set(null);
          this.loadWeek();
        },
        error: () => this.confirmDay.set(null),
      });
  }

  // ---- presentation ----
  protected readonly capacityLabel = capacityLabel;
  protected utilPct(col: WeekDayColumn): number {
    return col.total === 0 ? 0 : Math.round((col.busy / col.total) * 100);
  }
  protected count(col: WeekDayColumn, key: SlotStatusKey): number {
    return col.slots.filter((s) => s.statusKey === key).length;
  }
  protected isExpanded(iso: string): boolean {
    return this.expanded().has(iso);
  }

  // R2-3: cap how many slots a day column renders so a full work day of
  // 15-20min slots no longer overflows the grid; the per-day expander
  // (toggleExpand) reveals the rest.
  protected readonly slotPreview = 8;
  protected previewSlots(col: WeekDayColumn) {
    return this.isExpanded(col.iso) ? col.slots : col.slots.slice(0, this.slotPreview);
  }
}
