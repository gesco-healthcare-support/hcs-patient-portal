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
import { SystemParametersService } from '../../proxy/system-parameters-controllers/system-parameters.service';
import type { DoctorAvailabilitySlotsPreviewDto } from '../../proxy/doctor-availabilities/models';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { isoDate } from './avail-grid.util';
import {
  GENERATION_SLOT_LIMIT,
  buildGenerateInput,
  countPreviewConflicts,
  countPreviewSlots,
  estimateSlotCount,
  exceedsLimit,
  mapPreviewToDays,
  type GenFormState,
  type GenMode,
  type GenTimeRange,
} from './gen-slots.util';

interface LookupOption {
  id: string;
  name: string;
}
interface CalCell {
  blank: boolean;
  iso: string;
  day: number;
  disabled: boolean;
  selected: boolean;
}

const WEEKDAY_LABELS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

/**
 * Internal Scheduling (Prompt 14) -- generate slots. Two patterns: date range +
 * weekdays, or pick irregular days on a calendar (backed by the SelectedDates
 * server add). Preview calls generatePreview; submit calls createRange, which
 * re-expands server-side and auto-skips conflicting slots, so the UI shows
 * conflicts in red and reports created-vs-skipped instead of forcing manual
 * removal. Standalone + OnPush + signals; form assembly + estimate in
 * gen-slots.util. Times are naive clinic-local (deferred date/time plan adds TZ).
 */
@Component({
  selector: 'app-internal-generate-slots',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './internal-generate-slots.component.html',
})
export class InternalGenerateSlotsComponent implements OnInit {
  private readonly service = inject(DoctorAvailabilityService);
  private readonly systemParams = inject(SystemParametersService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);

  protected readonly weekdayLabels = WEEKDAY_LABELS;
  protected readonly slotLimit = GENERATION_SLOT_LIMIT;

  private readonly today = new Date();
  private readonly todayIso = isoDate(this.today);

  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);
  protected readonly locations = signal<LookupOption[]>([]);
  protected readonly appointmentTypes = signal<LookupOption[]>([]);
  // 2026-06-23: tenant booking lead time (days). Slots dated within today+lead
  // can never be booked, so the form warns before they are generated. 0 = unknown
  // (fetch failed / no read permission) -> no warning rather than a wrong one.
  protected readonly leadTimeDays = signal(0);

  protected readonly locationId = signal('');
  protected readonly mode = signal<GenMode>('range');
  protected readonly fromDate = signal(this.todayIso);
  protected readonly toDate = signal(isoDate(this.addDays(this.today, 4)));
  protected readonly weekdays = signal<boolean[]>([false, true, true, true, true, true, false]);
  protected readonly selectedDates = signal<string[]>([]);
  protected readonly timeRanges = signal<GenTimeRange[]>([
    { fromTime: '08:30', toTime: '11:30', durationOverride: null },
  ]);
  protected readonly capacity = signal(3);
  protected readonly durationMinutes = signal(60);
  protected readonly selectedTypeIds = signal<string[]>([]);

  protected readonly monthCursor = signal(
    new Date(this.today.getFullYear(), this.today.getMonth(), 1),
  );
  protected readonly preview = signal<DoctorAvailabilitySlotsPreviewDto[] | null>(null);

  protected readonly genState = computed<GenFormState>(() => ({
    locationId: this.locationId(),
    mode: this.mode(),
    fromDate: this.fromDate(),
    toDate: this.toDate(),
    weekdays: this.weekdays(),
    selectedDates: this.selectedDates(),
    timeRanges: this.timeRanges(),
    capacity: this.capacity(),
    durationMinutes: this.durationMinutes(),
    appointmentTypeIds: this.selectedTypeIds(),
  }));

  protected readonly estimate = computed(() => estimateSlotCount(this.genState()));
  protected readonly overLimit = computed(() => exceedsLimit(this.estimate()));

  protected readonly previewDays = computed(() => {
    const p = this.preview();
    return p ? mapPreviewToDays(p) : [];
  });
  protected readonly totalSlots = computed(() => countPreviewSlots(this.preview() ?? []));
  protected readonly conflicts = computed(() => countPreviewConflicts(this.preview() ?? []));
  protected readonly creatable = computed(() => this.totalSlots() - this.conflicts());
  protected readonly canSubmit = computed(() => this.preview() !== null && this.creatable() > 0);
  protected readonly previewCols = computed(() =>
    Math.min(Math.max(this.previewDays().length, 1), 7),
  );

  // 2026-06-23: earliest bookable date = today + lead time. Empty until the lead
  // time is known (fetch may 403 / return 0).
  protected readonly earliestBookableIso = computed(() => {
    const lead = this.leadTimeDays();
    return lead > 0 ? isoDate(this.addDays(this.today, lead)) : '';
  });

  // Non-empty when any date this form would generate falls inside the lead-time
  // window (i.e. before earliestBookableIso). Such slots can never be booked.
  protected readonly leadTimeWarning = computed<string>(() => {
    const earliestBookable = this.earliestBookableIso();
    if (!earliestBookable) {
      return '';
    }
    const earliestGenerated = this.earliestGeneratedIso();
    if (!earliestGenerated || earliestGenerated >= earliestBookable) {
      return '';
    }
    const lead = this.leadTimeDays();
    return (
      `Some selected dates are within the ${lead}-day booking lead time (before ` +
      `${earliestBookable}) and will NOT be bookable -- a slot dated inside the lead ` +
      `time can never be booked. The earliest bookable date is ${earliestBookable}.`
    );
  });

  protected readonly monthLabel = computed(() =>
    this.monthCursor().toLocaleString('en-US', { month: 'long', year: 'numeric' }),
  );
  protected readonly monthCells = computed<CalCell[]>(() => {
    const cursor = this.monthCursor();
    const year = cursor.getFullYear();
    const month = cursor.getMonth();
    const firstDow = new Date(year, month, 1).getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const picked = new Set(this.selectedDates());
    const cells: CalCell[] = [];
    for (let i = 0; i < firstDow; i++) {
      cells.push({ blank: true, iso: '', day: 0, disabled: true, selected: false });
    }
    for (let d = 1; d <= daysInMonth; d++) {
      const iso = isoDate(new Date(year, month, d));
      cells.push({
        blank: false,
        iso,
        day: d,
        disabled: iso < this.todayIso,
        selected: picked.has(iso),
      });
    }
    return cells;
  });

  ngOnInit(): void {
    this.service.getLocationLookup({ maxResultCount: 100, skipCount: 0, filter: '' }).subscribe({
      next: (res) => {
        this.locations.set(
          (res.items ?? []).map((l) => ({ id: l.id ?? '', name: l.displayName ?? '' })),
        );
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.service
      .getAppointmentTypeLookup({ maxResultCount: 100, skipCount: 0, filter: '' })
      .subscribe({
        next: (res) =>
          this.appointmentTypes.set(
            (res.items ?? []).map((t) => ({ id: t.id ?? '', name: t.displayName ?? '' })),
          ),
        error: () => undefined,
      });
    // Booking lead time drives the in-window warning. Read access is gated by
    // SystemParameters.Default; on a miss we leave lead time at 0 (no warning).
    this.systemParams.get().subscribe({
      next: (p) => this.leadTimeDays.set(p.appointmentLeadTime ?? 0),
      error: () => undefined,
    });
  }

  private addDays(d: Date, n: number): Date {
    const r = new Date(d.getFullYear(), d.getMonth(), d.getDate());
    r.setDate(r.getDate() + n);
    return r;
  }

  // Earliest date this form would generate: the min picked day (pick mode) or the
  // first selected-weekday on/after fromDate within the range (range mode). Used
  // only to decide whether any generated date lands inside the lead-time window.
  private earliestGeneratedIso(): string | null {
    if (this.mode() === 'pick') {
      const ds = this.selectedDates();
      return ds.length ? ds.slice().sort()[0] : null;
    }
    const from = this.fromDate();
    const to = this.toDate();
    if (!from || !to || from > to) {
      return null;
    }
    const weekdays = this.weekdays();
    const end = new Date(to + 'T00:00:00');
    for (let d = new Date(from + 'T00:00:00'); d <= end; d.setDate(d.getDate() + 1)) {
      if (weekdays[d.getDay()]) {
        return isoDate(d);
      }
    }
    return null;
  }

  // ---- form mutations ----
  protected setMode(m: GenMode): void {
    this.mode.set(m);
    this.preview.set(null);
  }
  protected toggleWeekday(i: number): void {
    this.weekdays.set(this.weekdays().map((on, j) => (j === i ? !on : on)));
  }
  protected setRange(i: number, field: keyof GenTimeRange, value: string): void {
    this.timeRanges.set(
      this.timeRanges().map((r, j) =>
        j === i
          ? { ...r, [field]: field === 'durationOverride' ? (value ? Number(value) : null) : value }
          : r,
      ),
    );
  }
  protected addRange(): void {
    this.timeRanges.set([
      ...this.timeRanges(),
      { fromTime: '13:00', toTime: '16:00', durationOverride: null },
    ]);
  }
  protected removeRange(i: number): void {
    if (this.timeRanges().length <= 1) {
      return;
    }
    this.timeRanges.set(this.timeRanges().filter((_, j) => j !== i));
  }
  protected toggleType(id: string): void {
    const cur = this.selectedTypeIds();
    this.selectedTypeIds.set(cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id]);
  }
  protected isTypeOn(id: string): boolean {
    return this.selectedTypeIds().includes(id);
  }
  protected reset(): void {
    this.preview.set(null);
    this.timeRanges.set([{ fromTime: '08:30', toTime: '11:30', durationOverride: null }]);
    this.selectedTypeIds.set([]);
    this.selectedDates.set([]);
    this.toaster.info('Form reset.');
  }

  // ---- calendar (pick mode) ----
  protected shiftMonth(delta: number): void {
    const c = this.monthCursor();
    this.monthCursor.set(new Date(c.getFullYear(), c.getMonth() + delta, 1));
  }
  protected toggleDate(cell: CalCell): void {
    if (cell.blank || cell.disabled) {
      return;
    }
    const cur = this.selectedDates();
    this.selectedDates.set(
      cur.includes(cell.iso) ? cur.filter((x) => x !== cell.iso) : [...cur, cell.iso],
    );
  }
  protected clearDates(): void {
    this.selectedDates.set([]);
  }

  // ---- preview + submit ----
  protected genPreview(): void {
    if (!this.locationId()) {
      this.toaster.warn('Select a location first.');
      return;
    }
    if (this.mode() === 'pick' && this.selectedDates().length === 0) {
      this.toaster.warn('Pick at least one day on the calendar.');
      return;
    }
    if (this.overLimit()) {
      this.toaster.warn(`This would create over ${this.slotLimit} slots. Narrow the range.`);
      return;
    }
    this.isBusy.set(true);
    this.service
      .generatePreview(buildGenerateInput(this.genState()))
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: (res) => this.preview.set(res ?? []),
        error: () => undefined,
      });
  }

  protected cancelPreview(): void {
    this.preview.set(null);
  }

  protected submit(): void {
    if (!this.canSubmit() || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.service
      .createRange(buildGenerateInput(this.genState()))
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: (res) => {
          const inserted = res.insertedCount ?? 0;
          const skipped = res.skippedConflictCount ?? 0;
          this.toaster.success(
            skipped > 0
              ? `${inserted} slot(s) created; ${skipped} conflict(s) skipped.`
              : `${inserted} slot(s) created.`,
          );
          this.preview.set(null);
          void this.router.navigateByUrl('/doctor-management/doctor-availabilities');
        },
        error: () => undefined,
      });
  }
}
