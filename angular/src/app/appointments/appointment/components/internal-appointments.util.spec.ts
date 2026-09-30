import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import type { AppointmentStatusCountDto } from '../../../proxy/appointments/models';
import {
  avatarInitials,
  bucketChipCounts,
  decideByInfo,
  DECISION_DUE_DAYS,
  isRowActionable,
  pillOfStatus,
  segmentStatuses,
  toCsvContent,
} from './internal-appointments.util';

describe('internal-appointments.util', () => {
  describe('isRowActionable', () => {
    it('allows reschedule/cancel for the open pills', () => {
      expect(isRowActionable('Pending')).toBe(true);
      expect(isRowActionable('InfoRequested')).toBe(true);
      expect(isRowActionable('Approved')).toBe(true);
      expect(isRowActionable('Rescheduled')).toBe(true);
    });

    it('blocks the terminal pills', () => {
      expect(isRowActionable('Cancelled')).toBe(false);
      expect(isRowActionable('Rejected')).toBe(false);
    });
  });

  describe('segmentStatuses', () => {
    it('returns an empty set for the "all" chip (no status filter)', () => {
      expect(segmentStatuses('all')).toEqual([]);
    });

    it('maps the Cancelled chip to every cancelled-bucket raw status', () => {
      const cancelled = segmentStatuses('cancelled');
      expect(cancelled).toContain(AppointmentStatusType.CancelledNoBill);
      expect(cancelled).toContain(AppointmentStatusType.CancelledLate);
      expect(cancelled).toContain(AppointmentStatusType.CancellationRequested);
      expect(cancelled).toContain(AppointmentStatusType.NoShow);
    });

    it('maps the Approved chip to the post-approval raw statuses', () => {
      const approved = segmentStatuses('approved');
      expect(approved).toContain(AppointmentStatusType.Approved);
      expect(approved).toContain(AppointmentStatusType.CheckedIn);
      expect(approved).toContain(AppointmentStatusType.CheckedOut);
      expect(approved).toContain(AppointmentStatusType.Billed);
    });
  });

  describe('bucketChipCounts', () => {
    it('sums raw per-status counts into chip totals + an all total', () => {
      const raw: AppointmentStatusCountDto[] = [
        { status: AppointmentStatusType.Pending, count: 3 },
        { status: AppointmentStatusType.Approved, count: 2 },
        { status: AppointmentStatusType.CheckedIn, count: 1 },
        { status: AppointmentStatusType.CancelledLate, count: 4 },
      ];
      const counts = bucketChipCounts(raw);
      expect(counts.all).toBe(10);
      expect(counts.pending).toBe(3);
      expect(counts.approved).toBe(3); // Approved + CheckedIn
      expect(counts.cancelled).toBe(4);
      expect(counts.rejected).toBe(0);
    });

    it('ignores rows with no status', () => {
      const counts = bucketChipCounts([{ count: 5 } as AppointmentStatusCountDto]);
      expect(counts.all).toBe(0);
    });
  });

  describe('pillOfStatus', () => {
    it('falls back to Pending for an absent status', () => {
      expect(pillOfStatus(undefined)).toBe('Pending');
      expect(pillOfStatus(AppointmentStatusType.Rejected)).toBe('Rejected');
    });
  });

  describe('decideByInfo', () => {
    const now = new Date(2026, 5, 14); // 2026-06-14 local midnight

    it('returns null with no creation timestamp', () => {
      expect(decideByInfo(null, now)).toBeNull();
      expect(decideByInfo('not-a-date', now)).toBeNull();
    });

    it('is ok (green) when the deadline is the full window away', () => {
      // created today -> due today + DECISION_DUE_DAYS
      const created = new Date(2026, 5, 14, 9, 0, 0).toISOString();
      const info = decideByInfo(created, now);
      expect(info).not.toBeNull();
      expect(info!.daysLeft).toBe(DECISION_DUE_DAYS);
      expect(info!.level).toBe('ok');
      expect(info!.label).toBe('3d');
    });

    it('is warn at two days out', () => {
      const created = new Date(2026, 5, 13, 9, 0, 0).toISOString(); // due in 2
      const info = decideByInfo(created, now);
      expect(info!.daysLeft).toBe(2);
      expect(info!.level).toBe('warn');
      expect(info!.label).toBe('2d');
    });

    it('is crit at one day out and today', () => {
      const dueTomorrow = decideByInfo(new Date(2026, 5, 12, 9, 0, 0).toISOString(), now);
      expect(dueTomorrow!.daysLeft).toBe(1);
      expect(dueTomorrow!.level).toBe('crit');
      expect(dueTomorrow!.label).toBe('1d');

      const dueToday = decideByInfo(new Date(2026, 5, 11, 9, 0, 0).toISOString(), now);
      expect(dueToday!.daysLeft).toBe(0);
      expect(dueToday!.level).toBe('crit');
      expect(dueToday!.label).toBe('today');
    });

    it('is crit + past when overdue', () => {
      const overdue = decideByInfo(new Date(2026, 5, 1, 9, 0, 0).toISOString(), now);
      expect(overdue!.daysLeft).toBeLessThan(0);
      expect(overdue!.level).toBe('crit');
      expect(overdue!.label).toBe('past');
    });
  });

  describe('avatarInitials', () => {
    it('builds two-letter uppercase initials', () => {
      expect(avatarInitials('Ada', 'Lovelace')).toBe('AL');
    });

    it('degrades gracefully when names are missing', () => {
      expect(avatarInitials(null, null)).toBe('?');
      expect(avatarInitials('Madonna', '')).toBe('M');
    });
  });

  describe('toCsvContent', () => {
    it('quotes fields and doubles embedded quotes', () => {
      const csv = toCsvContent(['Confirmation', 'Patient'], [['A00001', 'Smith, "Sam"']]);
      expect(csv).toBe('"Confirmation","Patient"\r\n"A00001","Smith, ""Sam"""');
    });
  });
});
