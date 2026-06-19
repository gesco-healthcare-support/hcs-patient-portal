import type { DoctorAvailabilityWithNavigationPropertiesDto } from '../../proxy/doctor-availabilities/models';
import { BookingStatus } from '../../proxy/enums/booking-status.enum';

/**
 * Prompt 14 (2026-06-15) -- pure helpers for the internal availabilities week
 * grid. Each persisted DoctorAvailability is ONE slot with a BookingStatus and
 * a capacity; the grid colours slots by status and shows a per-day utilisation
 * bar (non-available slots / total). All dates are treated as naive clinic-local
 * here; the deferred date/time plan adds the clinic-timezone label later.
 */

export type SlotStatusKey = 'available' | 'booked' | 'reserved';

export interface GridSlot {
  id: string;
  statusKey: SlotStatusKey;
  capacity: number;
  fromTime: string;
  toTime: string;
  timeLabel: string;
  /** #2 -- booked/reserved patient names on this slot (empty for available). */
  patientNames: string[];
}

export interface WeekDayColumn {
  /** Local yyyy-mm-dd for this column. */
  iso: string;
  /** Short weekday label, e.g. "Mon". */
  dow: string;
  /** Day-of-month number. */
  dayNum: number;
  slots: GridSlot[];
  /** Total slots that day. */
  total: number;
  /** Slots in a non-available (booked/reserved) state. */
  busy: number;
}

/** Map the slot's BookingStatus to its grid colour class. */
export function bookingStatusToKey(status: BookingStatus | undefined | null): SlotStatusKey {
  switch (status) {
    case BookingStatus.Booked:
      return 'booked';
    case BookingStatus.Reserved:
      return 'reserved';
    case BookingStatus.Available:
    default:
      return 'available';
  }
}

/** Human label for a slot status. */
export function bookingStatusLabel(status: BookingStatus | undefined | null): string {
  const key = bookingStatusToKey(status);
  return key.charAt(0).toUpperCase() + key.slice(1);
}

interface ParsedTime {
  h: number;
  m: number;
}

function parseTimeOnly(value: string | undefined | null): ParsedTime | null {
  if (!value) {
    return null;
  }
  const [hh, mm] = value.split(':');
  const h = Number(hh);
  const m = Number(mm);
  if (Number.isNaN(h) || Number.isNaN(m)) {
    return null;
  }
  return { h, m };
}

function formatOne(t: ParsedTime, withMeridiem: boolean): string {
  const meridiem = t.h >= 12 ? 'PM' : 'AM';
  const hour12 = t.h % 12 === 0 ? 12 : t.h % 12;
  const mm = String(t.m).padStart(2, '0');
  return withMeridiem ? `${hour12}:${mm} ${meridiem}` : `${hour12}:${mm}`;
}

/**
 * Format a slot's time band from two "HH:mm:ss" TimeOnly strings, e.g.
 * "8:30 - 9:30 AM" (shared meridiem) or "11:30 AM - 1:00 PM" (crossing noon).
 */
export function formatTimeRange(
  fromTime: string | undefined | null,
  toTime: string | undefined | null,
): string {
  const f = parseTimeOnly(fromTime);
  const t = parseTimeOnly(toTime);
  if (!f && !t) {
    return '';
  }
  if (f && !t) {
    return formatOne(f, true);
  }
  if (!f && t) {
    return formatOne(t, true);
  }
  const sameMeridiem = f!.h >= 12 === t!.h >= 12;
  return `${formatOne(f!, !sameMeridiem)} - ${formatOne(t!, true)}`;
}

/** Local yyyy-mm-dd for a Date, without any UTC conversion. */
export function isoDate(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/** Monday 00:00 of the week containing the anchor date (local). */
export function startOfWeekMonday(anchor: Date): Date {
  const d = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate());
  const dow = d.getDay(); // 0=Sun .. 6=Sat
  const toMonday = dow === 0 ? -6 : 1 - dow;
  d.setDate(d.getDate() + toMonday);
  return d;
}

/** The seven dates (Mon..Sun) for the week at `weekOffset` relative to anchor. */
export function weekDatesFor(anchor: Date, weekOffset: number): Date[] {
  const start = startOfWeekMonday(anchor);
  start.setDate(start.getDate() + weekOffset * 7);
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(start.getFullYear(), start.getMonth(), start.getDate());
    d.setDate(d.getDate() + i);
    return d;
  });
}

/** Compact label for the visible week, e.g. "Jun 15 - 21, 2026". */
export function formatWeekRange(weekDates: Date[]): string {
  if (weekDates.length === 0) {
    return '';
  }
  const a = weekDates[0];
  const b = weekDates[weekDates.length - 1];
  const mon = (d: Date) => d.toLocaleString('en-US', { month: 'short' });
  if (a.getMonth() === b.getMonth() && a.getFullYear() === b.getFullYear()) {
    return `${mon(a)} ${a.getDate()} - ${b.getDate()}, ${b.getFullYear()}`;
  }
  return `${mon(a)} ${a.getDate()} - ${mon(b)} ${b.getDate()}, ${b.getFullYear()}`;
}

/**
 * Bucket flat availability rows into the seven week columns by their
 * availableDate (date portion only, so no timezone shift). Slots are ordered by
 * start time; total/busy counts cover every slot that day regardless of any
 * status filter applied for display.
 */
export function buildWeekColumns(
  items: DoctorAvailabilityWithNavigationPropertiesDto[],
  weekDates: Date[],
  patientNames: Record<string, string[]> = {},
): WeekDayColumn[] {
  return weekDates.map((d) => {
    const iso = isoDate(d);
    const slots: GridSlot[] = items
      .filter((it) => (it.doctorAvailability?.availableDate ?? '').slice(0, 10) === iso)
      .map((it) => {
        const da = it.doctorAvailability!;
        return {
          id: da.id ?? '',
          statusKey: bookingStatusToKey(da.bookingStatusId),
          capacity: da.capacity ?? 0,
          fromTime: da.fromTime ?? '',
          toTime: da.toTime ?? '',
          timeLabel: formatTimeRange(da.fromTime, da.toTime),
          patientNames: patientNames[da.id ?? ''] ?? [],
        };
      })
      .sort((a, b) => a.fromTime.localeCompare(b.fromTime));
    const busy = slots.filter((s) => s.statusKey !== 'available').length;
    return {
      iso,
      dow: d.toLocaleString('en-US', { weekday: 'short' }),
      dayNum: d.getDate(),
      slots,
      total: slots.length,
      busy,
    };
  });
}
