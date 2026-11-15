import type { AppointmentChangeLogDto } from '../proxy/appointment-change-logs/models';
import { changeTypeMeta, groupChangeLogEntries, whoHint, whoIsMuted } from './clg-log.util';
import type { ChangeLogRow } from './clg-log.util';

describe('clg-log.util', () => {
  describe('changeTypeMeta', () => {
    it('maps created/updated/deleted to icon + tint + verb', () => {
      expect(changeTypeMeta('Created')).toEqual({
        icon: 'plus',
        tint: 'tint-green',
        verb: 'added',
      });
      expect(changeTypeMeta('Updated')).toEqual({
        icon: 'refresh',
        tint: 'tint-blue',
        verb: 'updated',
      });
      expect(changeTypeMeta('Deleted')).toEqual({ icon: 'x', tint: 'tint-red', verb: 'removed' });
    });
    it('defaults unknown change types to updated', () => {
      expect(changeTypeMeta(null).verb).toBe('updated');
      expect(changeTypeMeta('Whatever').tint).toBe('tint-blue');
    });
  });

  describe('groupChangeLogEntries', () => {
    const rows: AppointmentChangeLogDto[] = [
      {
        appointmentId: 'a1',
        entityType: 'Appointment',
        changeType: 'Updated',
        changeTime: '2026-06-10T14:48:00Z',
        propertyName: 'Status',
        oldValue: 'Pending',
        newValue: 'Approved',
        valueRedacted: false,
      },
      {
        appointmentId: 'a1',
        entityType: 'Appointment',
        changeType: 'Updated',
        changeTime: '2026-06-10T14:48:00Z',
        propertyName: 'PanelNumber',
        oldValue: '',
        newValue: 'P-22',
        valueRedacted: false,
      },
      {
        appointmentId: 'a1',
        entityType: 'Patient',
        changeType: 'Updated',
        changeTime: '2026-06-09T11:21:00Z',
        propertyName: 'SocialSecurityNumber',
        valueRedacted: true,
      },
    ];

    it('groups same appointment+entity+type+time rows into one entry with all diffs', () => {
      const entries = groupChangeLogEntries(rows);
      expect(entries).toHaveSize(2);
      expect(entries[0].diffs).toHaveSize(2);
      expect(entries[0].entityType).toBe('Appointment');
      expect(entries[1].diffs).toHaveSize(1);
      expect(entries[1].diffs[0].valueRedacted).toBe(true);
    });

    it('preserves first-seen (newest-first) order', () => {
      const entries = groupChangeLogEntries(rows);
      expect(entries[0].changeTime).toBe('2026-06-10T14:48:00Z');
      expect(entries[1].changeTime).toBe('2026-06-09T11:21:00Z');
    });

    it('returns an empty list for no rows', () => {
      expect(groupChangeLogEntries([])).toEqual([]);
    });
  });

  describe('who and action', () => {
    const row = (over: Partial<ChangeLogRow>): ChangeLogRow => ({
      appointmentId: 'a1',
      entityType: 'Appointment',
      changeType: 'Updated',
      changeTime: '2026-06-10T14:48:00Z',
      propertyName: 'AppointmentStatus',
      ...over,
    });

    it('carries the action and the author onto the grouped entry', () => {
      const entries = groupChangeLogEntries([
        row({ actionLabel: 'Approved', changedByName: 'Sam Supervisor', changedByKind: 'Staff' }),
      ]);
      expect(entries[0].actionLabel).toBe('Approved');
      expect(entries[0].changedByName).toBe('Sam Supervisor');
      expect(entries[0].changedByKind).toBe('Staff');
      expect(entries[0].fieldCount).toBe(1);
    });

    it('does not merge two different actions that share a timestamp', () => {
      const entries = groupChangeLogEntries([
        row({ entityType: 'Document', actionLabel: 'Document accepted' }),
        row({ entityType: 'Document', actionLabel: 'Document rejected' }),
      ]);
      expect(entries).toHaveSize(2);
    });

    it('counts a row with no field name as an event, not a field', () => {
      const entries = groupChangeLogEntries([
        row({ propertyName: '', actionLabel: 'Authorized user added' }),
      ]);
      expect(entries[0].fieldCount).toBe(0);
      expect(entries[0].diffs).toHaveSize(1);
    });

    it('hints host staff and outside only, and mutes system-like actors', () => {
      expect(whoHint('HostStaff')).toBe('host staff');
      expect(whoHint('Outside')).toBe('outside');
      expect(whoHint('Staff')).toBe('');
      expect(whoHint(null)).toBe('');
      expect(whoIsMuted('System')).toBeTrue();
      expect(whoIsMuted('CaseTracker')).toBeTrue();
      expect(whoIsMuted('Party')).toBeTrue();
      expect(whoIsMuted('Staff')).toBeFalse();
    });
  });
});
