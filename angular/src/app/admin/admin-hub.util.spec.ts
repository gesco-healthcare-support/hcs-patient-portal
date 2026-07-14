import {
  ADMIN_SECTIONS,
  auditMethodClass,
  auditResultLabel,
  auditStatusClass,
  buildAuditCsv,
  humanizeToken,
  insertVariable,
  isLockedRole,
  previewSegments,
  roleKind,
  SP_GROUPS,
  SpField,
} from './admin-hub.util';

describe('admin-hub.util', () => {
  describe('ADMIN_SECTIONS', () => {
    it('has the four sections in rail order, each with a gating policy', () => {
      expect(ADMIN_SECTIONS.map((s) => s.key)).toEqual([
        'templates',
        'parameters',
        'roles',
        'audit',
      ]);
      expect(ADMIN_SECTIONS.every((s) => !!s.policy && !!s.route && !!s.icon)).toBeTrue();
    });
  });

  describe('isLockedRole / roleKind', () => {
    it('locks only IT Admin', () => {
      expect(isLockedRole('IT Admin')).toBeTrue();
      expect(isLockedRole('Staff Supervisor')).toBeFalse();
      expect(isLockedRole(null)).toBeFalse();
    });
    it('groups internal vs external roles', () => {
      expect(roleKind('Staff Supervisor')).toBe('Internal');
      expect(roleKind('Applicant Attorney')).toBe('External');
    });
  });

  describe('previewSegments', () => {
    it('returns a single plain segment when there are no variables', () => {
      expect(previewSegments('plain text')).toEqual([{ text: 'plain text', isVar: false }]);
    });
    it('splits plain and variable segments, humanizing unknown tokens', () => {
      const segs = previewSegments('Hi ##PatientFullName##, code ##ConfirmationNumber##.');
      expect(segs).toEqual([
        { text: 'Hi ', isVar: false },
        { text: 'Patient Full Name', isVar: true },
        { text: ', code ', isVar: false },
        { text: 'Confirmation Number', isVar: true },
        { text: '.', isVar: false },
      ]);
    });
    it('prefers a supplied label over the humanized token', () => {
      const segs = previewSegments('##URL##', { URL: 'Action link' });
      expect(segs).toEqual([{ text: 'Action link', isVar: true }]);
    });
    it('treats null/empty as empty', () => {
      expect(previewSegments(null)).toEqual([]);
    });
  });

  describe('insertVariable', () => {
    it('appends ##token## with a separating space', () => {
      expect(insertVariable('Hello', 'PatientFullName')).toBe('Hello ##PatientFullName##');
    });
    it('does not double-space after a trailing space or newline', () => {
      expect(insertVariable('Hello ', 'X')).toBe('Hello ##X##');
      expect(insertVariable('Hello\n', 'X')).toBe('Hello\n##X##');
    });
    it('handles an empty body', () => {
      expect(insertVariable('', 'X')).toBe('##X##');
    });
  });

  describe('humanizeToken', () => {
    it('splits PascalCase and keeps acronyms intact', () => {
      expect(humanizeToken('ExpiresAt')).toBe('Expires At');
      expect(humanizeToken('URL')).toBe('URL');
    });
  });

  describe('audit chip mappers', () => {
    it('maps method to a chip class', () => {
      expect(auditMethodClass('GET')).toBe('get');
      expect(auditMethodClass('post')).toBe('post');
      expect(auditMethodClass('DELETE')).toBe('del');
      expect(auditMethodClass(null)).toBe('');
    });
    it('maps status to a chip class and result label', () => {
      expect(auditStatusClass(200)).toBe('s2');
      expect(auditStatusClass(403)).toBe('s4');
      expect(auditStatusClass(500)).toBe('s5');
      expect(auditResultLabel(204)).toBe('Success');
      expect(auditResultLabel(429)).toBe('Denied / throttled');
      expect(auditResultLabel(503)).toBe('Server error');
    });
  });

  describe('buildAuditCsv', () => {
    it('writes a header and escapes fields with commas/quotes', () => {
      const csv = buildAuditCsv([
        {
          time: 'Jun 11 10:42',
          user: 'a@b.com',
          method: 'POST',
          url: '/api/app/x',
          status: 200,
          durationMs: 312,
          ip: '1.2.3.4',
          client: 'Chrome, macOS',
          tenant: 'Falkinstein',
        },
      ]);
      const lines = csv.split('\n');
      expect(lines[0]).toBe('Time,User,Method,URL,Status,Duration (ms),IP,Client,Tenant');
      expect(lines[1]).toContain('"Chrome, macOS"');
      expect(lines[1]).toContain('/api/app/x');
    });
  });

  describe('SP_GROUPS', () => {
    it('labels the slot duration in minutes and everything else in days', () => {
      const fields = SP_GROUPS.reduce<SpField[]>((acc, g) => acc.concat(g.fields), []);
      const duration = fields.find((f) => f.key === 'appointmentDurationTime');
      expect(duration?.unit).toBe('minutes');
      expect(
        fields.filter((f) => f.key !== 'appointmentDurationTime').every((f) => f.unit === 'days'),
      ).toBeTrue();
    });
  });
});
