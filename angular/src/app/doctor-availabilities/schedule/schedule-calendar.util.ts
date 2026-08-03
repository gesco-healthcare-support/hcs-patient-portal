import type { EventInput } from 'fullcalendar';
import type {
  ScheduleAppointmentDto,
  ScheduleSlotDto,
} from '../../proxy/doctor-availabilities/models';
import { AppointmentStatusType } from '../../proxy/enums/appointment-status-type.enum';

/**
 * Phase 3 (2026-07-31) -- pure mapping from the view-agnostic schedule DTO to
 * FullCalendar events, kept out of the component so the classification that
 * decides what staff believe is bookable is unit tested without DI.
 *
 * The server deliberately sends no colours, labels or FullCalendar vocabulary,
 * so every visual decision is made here. Occupancy comes from the real active
 * appointment count, NOT from `BookingStatusId` -- that column is never set to
 * `Booked` by any code, so it is not ground truth.
 *
 * `EventInput` is a type-only import, so nothing from the FullCalendar bundle
 * is pulled into this module at runtime.
 */

/** Occupancy colour class for a slot's background band. */
export type SlotOccupancyClass = 'slot-free' | 'slot-partial' | 'slot-full';

/**
 * Statuses that mean a patient has asked for something but staff have not yet
 * settled it. Everything else non-terminal (approved, checked in/out, billed)
 * is a committed booking. Terminal statuses never reach here -- the server
 * excludes them from the schedule entirely.
 */
const REQUESTED_STATUSES: readonly AppointmentStatusType[] = [
  AppointmentStatusType.Pending,
  AppointmentStatusType.RescheduleRequested,
  AppointmentStatusType.CancellationRequested,
  AppointmentStatusType.InfoRequested,
];

/** Whether an appointment is an unsettled request rather than a committed booking. */
export function isRequestedStatus(status: AppointmentStatusType | undefined | null): boolean {
  return status != null && REQUESTED_STATUSES.includes(status);
}

/**
 * Occupancy class from real counts. A slot at or over capacity is full: the
 * calendar must never paint an over-subscribed slot as still bookable.
 */
export function slotOccupancyClass(slot: ScheduleSlotDto): SlotOccupancyClass {
  const active = slot.activeCount ?? 0;
  const remaining = slot.remainingCapacity ?? Math.max(0, (slot.capacity ?? 0) - active);
  if (remaining <= 0) {
    return 'slot-full';
  }
  return active > 0 ? 'slot-partial' : 'slot-free';
}

/**
 * Normalizes a serialized TimeOnly to "HH:mm:ss". The server may send two-part
 * ("09:00") or fractional ("09:00:00.0000000") values depending on the value's
 * precision, and FullCalendar needs a stable local ISO string.
 */
function normalizeTime(value: string | undefined | null): string | null {
  if (!value) {
    return null;
  }
  const parts = value.split(':');
  if (parts.length < 2) {
    return null;
  }
  const hh = parts[0].padStart(2, '0');
  const mm = parts[1].padStart(2, '0');
  const ss = (parts[2] ?? '00').split('.')[0].padStart(2, '0');
  return `${hh}:${mm}:${ss}`;
}

/**
 * The slot's calendar date. Takes the date portion only -- the same guard the
 * availabilities grid uses -- so a serialized DateTime never shifts a slot into
 * the neighbouring day.
 */
function slotDate(slot: ScheduleSlotDto): string | null {
  const date = (slot.availableDate ?? '').slice(0, 10);
  return /^\d{4}-\d{2}-\d{2}$/.test(date) ? date : null;
}

/** Local ISO start/end for a slot, or null when the server sent an unusable row. */
function slotRange(slot: ScheduleSlotDto): { start: string; end?: string } | null {
  const date = slotDate(slot);
  const from = normalizeTime(slot.fromTime);
  if (!date || !from) {
    return null;
  }
  const to = normalizeTime(slot.toTime);
  return to ? { start: `${date}T${from}`, end: `${date}T${to}` } : { start: `${date}T${from}` };
}

/**
 * One non-interactive background band per slot, carrying its occupancy colour.
 * Full slots are included: unlike the booking picker, staff need to SEE that a
 * slot is full rather than have it disappear.
 */
export function toBackgroundEvents(slots: ScheduleSlotDto[]): EventInput[] {
  const events: EventInput[] = [];
  for (const slot of slots ?? []) {
    const range = slotRange(slot);
    if (!range) {
      continue;
    }
    events.push({
      // Prefixed so a slot id can never collide with an appointment event id,
      // which eventClick uses verbatim to route.
      id: `slot-${slot.slotId ?? ''}`,
      ...range,
      display: 'background',
      classNames: [slotOccupancyClass(slot)],
      extendedProps: {
        kind: 'slot',
        slotId: slot.slotId,
        capacity: slot.capacity ?? 0,
        activeCount: slot.activeCount ?? 0,
        remainingCapacity: slot.remainingCapacity ?? 0,
      },
    });
  }
  return events;
}

/** Chip title: confirmation number and patient name, skipping whichever is absent. */
function chipTitle(appointment: ScheduleAppointmentDto): string {
  return [appointment.requestConfirmationNumber, appointment.patientName]
    .filter((part) => !!part)
    .join(' - ');
}

/**
 * One clickable chip per appointment, positioned at its slot's time because an
 * appointment carries no time of its own. The event id IS the appointment id so
 * `eventClick` can route straight to the detail page.
 */
export function toAppointmentEvents(slots: ScheduleSlotDto[]): EventInput[] {
  const events: EventInput[] = [];
  for (const slot of slots ?? []) {
    const range = slotRange(slot);
    if (!range) {
      continue;
    }
    for (const appointment of slot.appointments ?? []) {
      events.push({
        id: appointment.appointmentId ?? '',
        ...range,
        title: chipTitle(appointment),
        classNames: [isRequestedStatus(appointment.status) ? 'appt-requested' : 'appt-booked'],
        extendedProps: {
          kind: 'appointment',
          appointmentId: appointment.appointmentId,
          status: appointment.status,
        },
      });
    }
  }
  return events;
}
