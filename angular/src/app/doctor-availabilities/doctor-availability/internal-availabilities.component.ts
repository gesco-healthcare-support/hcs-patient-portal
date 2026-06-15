import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import type { DoctorAvailabilityWithNavigationPropertiesDto } from '../../proxy/doctor-availabilities/models';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import {
  buildWeekColumns,
  formatWeekRange,
  weekDatesFor,
  type SlotStatusKey,
  type WeekDayColumn,
} from './avail-grid.util';

type StatusFilter = 'all' | SlotStatusKey;
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
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());
  protected readonly confirmDay = signal<WeekDayColumn | null>(null);

  protected readonly weekDates = computed(() => weekDatesFor(this.anchor, this.weekOffset()));
  protected readonly weekRange = computed(() => formatWeekRange(this.weekDates()));
  protected readonly columns = computed(() => buildWeekColumns(this.rows(), this.weekDates()));

  /** Columns with slots narrowed to the status filter (counts stay complete). */
  protected readonly displayColumns = computed(() => {
    const filter = this.statusFilter();
    const cols = this.columns();
    if (filter === 'all') {
      return cols;
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
        if (opts.length > 0) {
          this.locationId.set(opts[0].id);
          this.loadWeek();
        } else {
          this.loading.set(false);
        }
      },
      error: () => this.loading.set(false),
    });
  }

  private loadWeek(): void {
    const locationId = this.locationId();
    if (!locationId) {
      return;
    }
    this.loading.set(true);
    const week = this.weekDates();
    const min = `${this.toIso(week[0])}T00:00:00`;
    const max = `${this.toIso(week[week.length - 1])}T23:59:59`;
    this.service
      .getList({
        locationId,
        availableDateMin: min,
        availableDateMax: max,
        maxResultCount: 500,
        skipCount: 0,
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.rows.set(res.items ?? []),
        error: () => this.rows.set([]),
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
  protected cancelDeleteDay(): void {
    if (!this.isBusy()) {
      this.confirmDay.set(null);
    }
  }
  protected confirmDeleteDay(): void {
    const col = this.confirmDay();
    if (!col || this.isBusy()) {
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
  protected utilPct(col: WeekDayColumn): number {
    return col.total === 0 ? 0 : Math.round((col.busy / col.total) * 100);
  }
  protected count(col: WeekDayColumn, key: SlotStatusKey): number {
    return col.slots.filter((s) => s.statusKey === key).length;
  }
  protected isExpanded(iso: string): boolean {
    return this.expanded().has(iso);
  }
}
