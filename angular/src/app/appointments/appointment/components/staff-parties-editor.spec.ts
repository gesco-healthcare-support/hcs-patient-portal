import { of } from 'rxjs';
import type { RestService } from '@abp/ng.core';

import { StaffPartiesEditor } from './staff-parties-editor';
import type { AppointmentClaimExaminerDto } from '../../../proxy/appointment-claim-examiners/models';

/** The staff parties editor (2026-10-21): what it sends, and when it sends nothing. */
describe('StaffPartiesEditor', () => {
  let rest: jasmine.SpyObj<RestService>;
  let editor: StaffPartiesEditor;

  const examiner: AppointmentClaimExaminerDto = { name: 'TEST-Cleo', email: 'ce@example.test' };
  const loadedForm = {
    patientEmail: 'p@example.test',
    defenseAttorneyEmail: 'da@example.test',
    defenseAttorneyFirmName: 'TEST-Firm',
    defenseAttorneyEnabled: true,
  };

  beforeEach(() => {
    rest = jasmine.createSpyObj<RestService>('RestService', ['request']);
    rest.request.and.returnValue(of({ id: 'appt-1', concurrencyStamp: 'stamp-2' }) as never);
    editor = new StaffPartiesEditor(rest);
    editor.begin(loadedForm, examiner);
  });

  it('starts the examiner editor from the appointment', () => {
    expect(editor.claimExaminerForm.getRawValue().email).toBe('ce@example.test');
    expect(editor.hadParty('claimExaminer')).toBeTrue();
    expect(editor.hadParty('defenseAttorney')).toBeTrue();
    expect(editor.hadParty('applicantAttorney')).toBeFalse();
  });

  it('sends nothing when nothing changed', async () => {
    const saved = await editor.save('appt-1', { ...loadedForm }, null, 'master-1');

    expect(saved).toBeNull();
    expect(rest.request).not.toHaveBeenCalled();
  });

  it('PUTs only the changed party to the parties endpoint', async () => {
    editor.claimExaminerForm.patchValue({ email: 'ce2@example.test' });

    const saved = await editor.save('appt-1', { ...loadedForm }, null, 'master-1');

    expect(saved?.appointment.concurrencyStamp).toBe('stamp-2');
    const [request] = rest.request.calls.mostRecent().args as unknown as [
      { method: string; url: string; body: any },
    ];
    expect(request.method).toBe('PUT');
    expect(request.url).toBe('/api/app/appointments/appt-1/parties');
    expect(request.body.claimExaminer.email).toBe('ce2@example.test');
    expect(request.body.defenseAttorney).toBeNull();
    expect(request.body.patientEmail).toBeNull();
  });

  it('sends a removal, and undo takes it back', async () => {
    editor.remove('defenseAttorney');
    editor.undo('defenseAttorney');
    expect(editor.removed.defenseAttorney).toBeFalse();

    editor.remove('defenseAttorney');
    await editor.save('appt-1', { ...loadedForm }, null, 'master-1');

    const [request] = rest.request.calls.mostRecent().args as unknown as [{ body: any }];
    expect(request.body.defenseAttorney).toEqual({ remove: true });
  });

  it('cancel restores the examiner and clears the removals', () => {
    editor.claimExaminerForm.patchValue({ name: 'TEST-Changed' });
    editor.remove('claimExaminer');

    editor.cancel();

    expect(editor.claimExaminerForm.getRawValue().name).toBe('TEST-Cleo');
    expect(editor.removed.claimExaminer).toBeFalse();
  });

  it('reports a problem before saving an attorney without an email, or an invalid examiner email', () => {
    expect(
      editor.problem(
        { ...loadedForm, defenseAttorneyEmail: '', defenseAttorneyFirstName: 'TEST-New' },
        null,
        'm',
      ),
    ).toContain('email address');

    editor.claimExaminerForm.patchValue({ email: 'not-an-email' });
    expect(editor.problem({ ...loadedForm }, null, 'm')).toContain('claim examiner');
  });

  it('shows the examiner as saved', () => {
    expect(editor.claimExaminerAfterSave(examiner, { claimExaminer: { remove: true } })).toBeNull();
    expect(editor.claimExaminerAfterSave(examiner, { claimExaminer: null })).toBe(examiner);
    expect(
      editor.claimExaminerAfterSave(examiner, {
        claimExaminer: { remove: false, name: 'TEST-New', email: 'n@example.test' },
      })?.name,
    ).toBe('TEST-New');
  });
});
