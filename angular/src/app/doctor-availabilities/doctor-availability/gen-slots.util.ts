import type {
  DoctorAvailabilityGenerateInputDto,
  DoctorAvailabilitySlotsPreviewDto,
} from '../../proxy/doctor-availabilities/models';
import { BookingStatus } from '../../proxy/enums/booking-status.enum';
import { formatTimeRange } from './avail-grid.util';

/**
 * Prompt 14 (2026-06-15) -- pure helpers for the generate-slots form: assemble
 * the generate input DTO from form state (range+weekday OR explicit calendar
 * dates), estimate the slot count client-side to warn before the server's 5000
 * cap, and shape the preview response into a per-day grid.
 *
 * Conflict semantics: CreateRangeAsync re-runs the preview server-side and
 * inserts only the non-conflicting slots (conflicts are auto-skipped, never
 * trusted from a client-trimmed preview). So the UI shows conflicts in red and
 * reports created-vs-skipped on submit rather than forcing a manual removal.
 */

export type GenMode = 'range' | 'pick';

export interface GenTimeRange {
  fromTime: string; // "HH:mm"
  toTime: string; // "HH:mm"
  durationOverride: number | null;
}

export interface GenFormState {
  locationId: string;
  mode: GenMode;
  fromDate: string; // "yyyy-mm-dd"
  toDate: string; // "yyyy-mm-dd"
  weekdays: boolean[]; // length 7, index 0=Sun .. 6=Sat
  selectedDates: string[]; // "yyyy-mm-dd"
  timeRanges: GenTimeRange[];
  capacity: number;
  durationMinutes: number;
  appointmentTypeIds: string[];
}

export interface PreviewSlot {
  key: string;
  timeLabel: string;
  conflict: boolean;
}

export interface PreviewDay {
  label: string; // "Mon 15"
  conflicts: number;
  slots: PreviewSlot[];
}

/** Mirrors the server's locked GenerationSlotLimit (2026-05-20 Q2). */
export const GENERATION_SLOT_LIMIT = 5000;

function toTimeOnly(hhmm: string): string {
  if (!hhmm) {
    return hhmm;
  }
  return hhmm.length === 5 ? `${hhmm}:00` : hhmm;
}

function minutesOf(hhmm: string): number | null {
  const [h, m] = (hhmm ?? '').split(':');
  const hh = Number(h);
  const mm = Number(m);
  if (Number.isNaN(hh) || Number.isNaN(mm)) {
    return null;
  }
  return hh * 60 + mm;
}

/** Weekday indices (0=Sun..6=Sat) that are toggled on. */
export function selectedWeekdayIndices(weekdays: boolean[]): number[] {
  const out: number[] = [];
  weekdays.forEach((on, i) => {
    if (on) {
      out.push(i);
    }
  });
  return out;
}

/** Build the generate input DTO for the chosen pattern. */
export function buildGenerateInput(state: GenFormState): DoctorAvailabilityGenerateInputDto {
  const timeRanges = state.timeRanges.map((r) => ({
    fromTime: toTimeOnly(r.fromTime),
    toTime: toTimeOnly(r.toTime),
    appointmentDurationMinutes:
      r.durationOverride && r.durationOverride > 0 ? r.durationOverride : null,
  }));

  const base: DoctorAvailabilityGenerateInputDto = {
    locationId: state.locationId,
    timeRanges,
    capacity: state.capacity,
    appointmentDurationMinutes: state.durationMinutes,
    appointmentTypeIds: state.appointmentTypeIds,
    bookingStatusId: BookingStatus.Available,
  };

  if (state.mode === 'pick') {
    return { ...base, selectedDates: [...state.selectedDates].sort() };
  }

  return {
    ...base,
    fromDate: `${state.fromDate}T00:00:00`,
    toDate: `${state.toDate}T00:00:00`,
    selectedDays: selectedWeekdayIndices(state.weekdays),
  };
}

function resolveDayCount(state: GenFormState): number {
  if (state.mode === 'pick') {
    return new Set(state.selectedDates).size;
  }
  if (!state.fromDate || !state.toDate) {
    return 0;
  }
  const start = new Date(`${state.fromDate}T00:00:00`);
  const end = new Date(`${state.toDate}T00:00:00`);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end < start) {
    return 0;
  }
  const allowed = selectedWeekdayIndices(state.weekdays);
  const useAll = allowed.length === 0;
  let count = 0;
  const cursor = new Date(start);
  while (cursor <= end) {
    if (useAll || allowed.includes(cursor.getDay())) {
      count++;
    }
    cursor.setDate(cursor.getDate() + 1);
  }
  return count;
}

function resolveSlotsPerDay(state: GenFormState): number {
  return state.timeRanges.reduce((sum, r) => {
    const duration =
      r.durationOverride && r.durationOverride > 0 ? r.durationOverride : state.durationMinutes;
    if (!duration || duration <= 0) {
      return sum;
    }
    const f = minutesOf(r.fromTime);
    const t = minutesOf(r.toTime);
    if (f === null || t === null || t <= f) {
      return sum;
    }
    return sum + Math.floor((t - f) / duration);
  }, 0);
}

/** Client-side mirror of the server's EstimateSlotCount (dayCount * slotsPerDay). */
export function estimateSlotCount(state: GenFormState): number {
  if (!state.timeRanges.length) {
    return 0;
  }
  return resolveDayCount(state) * resolveSlotsPerDay(state);
}

export function exceedsLimit(count: number): boolean {
  return count > GENERATION_SLOT_LIMIT;
}

export function countPreviewSlots(preview: DoctorAvailabilitySlotsPreviewDto[]): number {
  return preview.reduce((sum, d) => sum + (d.doctorAvailabilities?.length ?? 0), 0);
}

export function countPreviewConflicts(preview: DoctorAvailabilitySlotsPreviewDto[]): number {
  return preview.reduce(
    (sum, d) => sum + (d.doctorAvailabilities?.filter((x) => x.isConflict).length ?? 0),
    0,
  );
}

function dayLabel(dates: string | undefined, days: string | null | undefined): string {
  // `dates` is "MM-dd-yyyy" from the backend; `days` is the full weekday name.
  const parts = (dates ?? '').split('-');
  const dayNum = parts.length === 3 ? Number(parts[1]) : NaN;
  const dow = (days ?? '').slice(0, 3);
  if (dow && !Number.isNaN(dayNum)) {
    return `${dow} ${dayNum}`;
  }
  return days ?? dates ?? '';
}

/** Shape the grouped preview response into per-day columns for the grid. */
export function mapPreviewToDays(preview: DoctorAvailabilitySlotsPreviewDto[]): PreviewDay[] {
  return preview.map((d, di) => {
    const slots: PreviewSlot[] = (d.doctorAvailabilities ?? []).map((s, si) => ({
      key: `${di}-${s.timeId ?? si}`,
      timeLabel: formatTimeRange(s.fromTime, s.toTime),
      conflict: !!s.isConflict,
    }));
    return {
      label: dayLabel(d.dates, d.days),
      conflicts: slots.filter((s) => s.conflict).length,
      slots,
    };
  });
}
