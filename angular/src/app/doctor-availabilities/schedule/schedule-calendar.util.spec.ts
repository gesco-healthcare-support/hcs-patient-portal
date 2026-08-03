import type { ScheduleSlotDto } from '../../proxy/doctor-availabilities/models';
import { AppointmentStatusType } from '../../proxy/enums/appointment-status-type.enum';
import {
  isRequestedStatus,
  slotOccupancyClass,
  toAppointmentEvents,
  toBackgroundEvents,
} from './schedule-calendar.util';

/**
 * Builds a schedule slot the way the server sends one: `availableDate` is a
 * serialized DateTime, `fromTime`/`toTime` are serialized TimeOnly values.
 */
function slot(overrides: Partial<ScheduleSlotDto> = {}): ScheduleSlotDto {
  return {
    slotId: '11111111-1111-1111-1111-111111111111',
    availableDate: '2026-08-03T00:00:00',
    fromTime: '09:00:00',
    toTime: '10:00:00',
    capacity: 3,
    activeCount: 0,
    remainingCapacity: 3,
    appointments: [],
    ...overrides,
  };
}

describe('schedule-calendar.util', () => {
  describe('slotOccupancyClass', () => {
    it('classes a slot with no active appointments as free', () => {
      expect(slotOccupancyClass(slot({ activeCount: 0, remainingCapacity: 3 }))).toBe('slot-free');
    });

    it('classes a partly booked slot as partial', () => {
      expect(slotOccupancyClass(slot({ activeCount: 1, remainingCapacity: 2 }))).toBe(
        'slot-partial',
      );
    });

    it('classes a slot with no remaining capacity as full', () => {
      expect(slotOccupancyClass(slot({ activeCount: 3, remainingCapacity: 0 }))).toBe('slot-full');
    });

    it('classes an over-subscribed slot as full rather than partial', () => {
      // The server clamps at 0, but the calendar must not show a negative slot as bookable.
      expect(slotOccupancyClass(slot({ activeCount: 4, remainingCapacity: 0 }))).toBe('slot-full');
    });
  });

  describe('toBackgroundEvents', () => {
    it('renders one background event per slot at its real date and time', () => {
      const events = toBackgroundEvents([slot()]);

      expect(events.length).toBe(1);
      expect(events[0].start).toBe('2026-08-03T09:00:00');
      expect(events[0].end).toBe('2026-08-03T10:00:00');
      expect(events[0].display).toBe('background');
    });

    it('carries the occupancy class so staff can see free versus full', () => {
      const events = toBackgroundEvents([
        slot({ slotId: 'a', activeCount: 0, remainingCapacity: 3 }),
        slot({ slotId: 'b', activeCount: 3, remainingCapacity: 0 }),
      ]);

      expect(events[0].classNames).toEqual(['slot-free']);
      expect(events[1].classNames).toEqual(['slot-full']);
    });

    it('keeps a full slot in the result instead of hiding it', () => {
      // The booking picker drops full slots; the staff calendar must not.
      const events = toBackgroundEvents([slot({ activeCount: 3, remainingCapacity: 0 })]);

      expect(events.length).toBe(1);
    });

    it('does not collide with appointment event ids', () => {
      const events = toBackgroundEvents([slot({ slotId: 'shared-id' })]);

      expect(events[0].id).not.toBe('shared-id');
    });

    it('skips a slot missing its date or start time rather than inventing one', () => {
      const events = toBackgroundEvents([
        slot({ availableDate: undefined }),
        slot({ fromTime: undefined }),
      ]);

      expect(events.length).toBe(0);
    });
  });

  describe('isRequestedStatus', () => {
    it('treats the four pending-ish statuses as requested', () => {
      expect(isRequestedStatus(AppointmentStatusType.Pending)).toBeTrue();
      expect(isRequestedStatus(AppointmentStatusType.RescheduleRequested)).toBeTrue();
      expect(isRequestedStatus(AppointmentStatusType.CancellationRequested)).toBeTrue();
      expect(isRequestedStatus(AppointmentStatusType.InfoRequested)).toBeTrue();
    });

    it('treats an approved appointment as booked, not requested', () => {
      expect(isRequestedStatus(AppointmentStatusType.Approved)).toBeFalse();
    });

    it('treats post-visit statuses as booked', () => {
      expect(isRequestedStatus(AppointmentStatusType.CheckedIn)).toBeFalse();
      expect(isRequestedStatus(AppointmentStatusType.CheckedOut)).toBeFalse();
      expect(isRequestedStatus(AppointmentStatusType.Billed)).toBeFalse();
    });
  });

  describe('toAppointmentEvents', () => {
    const booked = slot({
      activeCount: 2,
      remainingCapacity: 1,
      appointments: [
        {
          appointmentId: 'aaaaaaaa-0000-0000-0000-000000000001',
          requestConfirmationNumber: 'A90001',
          patientName: 'Jordan Rivera',
          status: AppointmentStatusType.Approved,
        },
        {
          appointmentId: 'aaaaaaaa-0000-0000-0000-000000000002',
          requestConfirmationNumber: 'A90002',
          patientName: 'Sam Okafor',
          status: AppointmentStatusType.Pending,
        },
      ],
    });

    it('sets each event id to the appointment id so a click can route to it', () => {
      const events = toAppointmentEvents([booked]);

      expect(events.map((e) => e.id)).toEqual([
        'aaaaaaaa-0000-0000-0000-000000000001',
        'aaaaaaaa-0000-0000-0000-000000000002',
      ]);
    });

    it('puts both the confirmation number and the patient name in the title', () => {
      const events = toAppointmentEvents([booked]);

      expect(events[0].title).toContain('A90001');
      expect(events[0].title).toContain('Jordan Rivera');
    });

    it('classes a requested appointment apart from a booked one', () => {
      const events = toAppointmentEvents([booked]);

      expect(events[0].classNames).toEqual(['appt-booked']);
      expect(events[1].classNames).toEqual(['appt-requested']);
    });

    it('places each chip at its slot time, since an appointment carries no time of its own', () => {
      const events = toAppointmentEvents([booked]);

      expect(events[0].start).toBe('2026-08-03T09:00:00');
      expect(events[0].end).toBe('2026-08-03T10:00:00');
    });

    it('returns nothing for a slot with no appointments', () => {
      expect(toAppointmentEvents([slot()]).length).toBe(0);
    });

    it('flattens appointments across every slot', () => {
      const other = slot({
        slotId: '22222222-2222-2222-2222-222222222222',
        fromTime: '11:00:00',
        toTime: '12:00:00',
        appointments: [
          {
            appointmentId: 'bbbbbbbb-0000-0000-0000-000000000003',
            requestConfirmationNumber: 'A90003',
            patientName: 'Chris Vale',
            status: AppointmentStatusType.Approved,
          },
        ],
      });

      const events = toAppointmentEvents([booked, other]);

      expect(events.length).toBe(3);
      expect(events[2].start).toBe('2026-08-03T11:00:00');
    });

    it('tolerates a two-part time value from the server', () => {
      const events = toAppointmentEvents([
        slot({
          fromTime: '09:00',
          toTime: '10:00',
          appointments: [
            {
              appointmentId: 'cccccccc-0000-0000-0000-000000000004',
              requestConfirmationNumber: 'A90004',
              patientName: 'Pat Lin',
              status: AppointmentStatusType.Approved,
            },
          ],
        }),
      ]);

      expect(events[0].start).toBe('2026-08-03T09:00:00');
    });
  });
});
