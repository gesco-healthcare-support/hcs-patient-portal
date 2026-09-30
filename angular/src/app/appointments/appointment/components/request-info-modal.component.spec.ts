import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { RequestInfoModalComponent } from './request-info-modal.component';
import { AppointmentInfoRequestService } from '../../../proxy/appointment-info-requests/appointment-info-request.service';

/**
 * The staff "Request info" / Send Back modal: flag specific appointment fields, each with an
 * optional hint, plus a required note, which moves the appointment to InfoRequested and emails
 * the requester a fix-it link.
 *
 * <p>It had NO spec and sat at 2 of 61 lines covered.</p>
 *
 * <p>The behaviour worth holding is the field picker. Sixty-five flaggable fields are grouped
 * into wizard-parity sections, each with a tri-state select-all. Two rules inside that are easy
 * to break and silent when broken: a field that is DESELECTED must lose its hint (otherwise a
 * hint for a field nobody flagged rides along in the email), and the selected list must come out
 * in registry order rather than click order (so the requester reads the fields in the same order
 * the form presents them).</p>
 *
 * <p>Group names are taken from the component's own `groups` rather than hardcoded, so this spec
 * does not pin the section list it is not testing.</p>
 *
 * <p>All notes, hints and identifiers below are synthetic.</p>
 */
describe('RequestInfoModalComponent', () => {
  let infoRequests: { sendBack: jasmine.Spy };
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };

  interface Probe {
    [key: string]: any;
  }

  function create(): Probe {
    infoRequests = { sendBack: jasmine.createSpy('sendBack').and.returnValue(of({})) };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };

    TestBed.configureTestingModule({
      providers: [
        { provide: AppointmentInfoRequestService, useValue: infoRequests },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    const c = TestBed.createComponent(RequestInfoModalComponent)
      .componentInstance as unknown as Probe;
    c.appointmentId = 'appt-1';
    c.visible = true;
    return c;
  }

  /** The first section, and its field keys, read from the component's own registry. */
  function firstGroup(c: Probe): { group: string; keys: string[] } {
    const g = c.groups[0];
    return { group: g.group, keys: g.fields.map((f: { key: string }) => f.key) };
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('selecting fields', () => {
    it('adds a field on the first toggle and removes it on the second', () => {
      const c = create();
      const { keys } = firstGroup(c);

      c.toggle(keys[0]);
      expect(c.selected.has(keys[0])).toBeTrue();

      c.toggle(keys[0]);
      expect(c.selected.has(keys[0])).toBeFalse();
    });

    it('DISCARDS the hint when a field is deselected', () => {
      /**
       * A hint belongs to the field that was flagged. Leaving it behind means a hint for
       * a field nobody flagged travels in the email and onto the fix-it page, attached to
       * nothing the requester can see.
       */
      const c = create();
      const { keys } = firstGroup(c);

      c.toggle(keys[0]);
      c.setHint(keys[0], 'does not match the panel on file');
      expect(c.hints[keys[0]]).toBeTruthy();

      c.toggle(keys[0]);

      expect(c.hints[keys[0]]).toBeUndefined();
    });

    it('keeps a hint while the field stays selected', () => {
      const c = create();
      const { keys } = firstGroup(c);
      c.toggle(keys[0]);
      c.setHint(keys[0], 'check the year');
      c.toggle(keys[1]);
      expect(c.hints[keys[0]]).toBe('check the year');
    });

    it('returns the selected keys in REGISTRY order, not click order', () => {
      // The requester reads these in the email; registry order matches the form they will
      // be sent back to, so the two can be followed side by side.
      const c = create();
      const { keys } = firstGroup(c);
      if (keys.length < 2) {
        pending('needs a section with at least two fields');
        return;
      }

      c.toggle(keys[1]);
      c.toggle(keys[0]);

      expect(c.selectedKeys()).toEqual([keys[0], keys[1]]);
    });

    it('names a field by its label, falling back to the raw key', () => {
      const c = create();
      const { keys } = firstGroup(c);
      expect(c.labelOf(keys[0])).toBeTruthy();
      expect(c.labelOf('not-a-field')).toBe('not-a-field');
    });

    it('records a hint against its field', () => {
      const c = create();
      const { keys } = firstGroup(c);
      c.setHint(keys[0], 'a hint');
      expect(c.hints[keys[0]]).toBe('a hint');
    });
  });

  describe('the section select-all', () => {
    it('reports none, some and all as the section fills up', () => {
      const c = create();
      const { group, keys } = firstGroup(c);
      if (keys.length < 2) {
        pending('needs a section with at least two fields');
        return;
      }

      expect(c.sectionState(group)).toBe('none');

      c.toggle(keys[0]);
      expect(c.sectionState(group)).toBe('some');

      keys.slice(1).forEach((k: string) => c.toggle(k));
      expect(c.sectionState(group)).toBe('all');
    });

    it('counts only the fields in that section', () => {
      const c = create();
      const { group, keys } = firstGroup(c);
      c.toggle(keys[0]);
      expect(c.groupSelectedCount(group)).toBe(1);
    });

    it('selects every field in the section in one click', () => {
      const c = create();
      const { group, keys } = firstGroup(c);

      c.toggleSection(group);

      keys.forEach((k: string) => expect(c.selected.has(k)).toBeTrue());
      expect(c.sectionState(group)).toBe('all');
    });

    it('EXPANDS the section it just selected, so the choice is visible', () => {
      // Selecting fields the user cannot see would leave them unable to review or remove
      // them except through the chip list.
      const c = create();
      const { group } = firstGroup(c);
      expect(c.isExpanded(group)).toBeFalse();

      c.toggleSection(group);

      expect(c.isExpanded(group)).toBeTrue();
    });

    it('clears the whole section when it is already fully selected', () => {
      const c = create();
      const { group, keys } = firstGroup(c);
      c.toggleSection(group);

      c.toggleSection(group);

      keys.forEach((k: string) => expect(c.selected.has(k)).toBeFalse());
      expect(c.sectionState(group)).toBe('none');
    });

    it('discards every hint in the section when it is cleared', () => {
      const c = create();
      const { group, keys } = firstGroup(c);
      c.toggleSection(group);
      keys.forEach((k: string) => c.setHint(k, 'a hint'));

      c.toggleSection(group);

      keys.forEach((k: string) => expect(c.hints[k]).toBeUndefined());
    });

    it('completes a partly-filled section rather than clearing it', () => {
      const c = create();
      const { group, keys } = firstGroup(c);
      if (keys.length < 2) {
        pending('needs a section with at least two fields');
        return;
      }
      c.toggle(keys[0]);

      c.toggleSection(group);

      expect(c.sectionState(group)).toBe('all');
    });

    it('returns no fields for a section that does not exist', () => {
      const c = create();
      expect(c.fieldsIn('not-a-section')).toEqual([]);
      expect(c.groupSelectedCount('not-a-section')).toBe(0);
      expect(c.sectionState('not-a-section')).toBe('none');
    });
  });

  describe('expanding sections', () => {
    it('starts collapsed and toggles open and shut', () => {
      // Collapsed by default so 65 fields do not scroll endlessly on open.
      const c = create();
      const { group } = firstGroup(c);

      expect(c.isExpanded(group)).toBeFalse();
      c.toggleExpand(group);
      expect(c.isExpanded(group)).toBeTrue();
      c.toggleExpand(group);
      expect(c.isExpanded(group)).toBeFalse();
    });
  });

  describe('the send gate', () => {
    it('needs both a flagged field and a note', () => {
      const c = create();
      const { keys } = firstGroup(c);

      expect(c.canSend).withContext('nothing chosen').toBeFalse();

      c.toggle(keys[0]);
      expect(c.canSend).withContext('no note yet').toBeFalse();

      c.note = 'Please confirm the claim number.';
      expect(c.canSend).toBeTrue();
    });

    it('does not accept a whitespace-only note', () => {
      const c = create();
      const { keys } = firstGroup(c);
      c.toggle(keys[0]);
      c.note = '   ';
      expect(c.canSend).toBeFalse();
    });
  });

  describe('sending', () => {
    function ready(c: Probe): string[] {
      const { keys } = firstGroup(c);
      c.toggle(keys[0]);
      c.note = 'Please confirm the claim number.';
      return keys;
    }

    it('does nothing without an appointment', async () => {
      const c = create();
      ready(c);
      c.appointmentId = null;
      await c.send();
      expect(infoRequests.sendBack).not.toHaveBeenCalled();
    });

    it('does nothing while the gate is unsatisfied', async () => {
      const c = create();
      await c.send();
      expect(infoRequests.sendBack).not.toHaveBeenCalled();
    });

    it('does nothing while a send is already running', async () => {
      const c = create();
      ready(c);
      c.isBusy = true;
      await c.send();
      expect(infoRequests.sendBack).not.toHaveBeenCalled();
    });

    it('sends the flagged fields, their hints and the note', async () => {
      const c = create();
      const keys = ready(c);
      c.setHint(keys[0], 'does not match the panel on file');

      await c.send();

      expect(infoRequests.sendBack).toHaveBeenCalled();
      const [id, input] = infoRequests.sendBack.calls.mostRecent().args;
      expect(id).toBe('appt-1');
      expect(JSON.stringify(input)).toContain(keys[0]);
      expect(JSON.stringify(input)).toContain('Please confirm the claim number.');
    });

    it('confirms, announces success and closes', async () => {
      const c = create();
      ready(c);
      let succeeded = 0;
      let visibleChanged: boolean | null = null;
      c.succeeded.subscribe(() => (succeeded += 1));
      c.visibleChange.subscribe((v: boolean) => (visibleChanged = v));

      await c.send();

      expect(toaster.success).toHaveBeenCalled();
      expect(succeeded).toBe(1);
      expect(visibleChanged).toBeFalse();
      expect(c.visible).toBeFalse();
    });

    it('clears the picker so a reopened modal starts fresh', () => {
      // The modal instance survives between openings; leftover selections would flag
      // fields on the NEXT appointment the staff member sends back.
      const c = create();
      const keys = ready(c);
      c.setHint(keys[0], 'a hint');
      c.toggleExpand(c.groups[0].group);

      c.close();

      expect(c.selected.size).toBe(0);
      expect(Object.keys(c.hints).length).toBe(0);
      expect(c.note).toBe('');
      expect(c.isBusy).toBeFalse();
      expect(c.expandedGroups.size).toBe(0);
    });

    it('STAYS open and releases the button when the send fails', async () => {
      /**
       * Safe to exercise: this failure path is caught inside send(), so the rejection is
       * handled rather than escaping as an unhandled error. Closing here would discard a
       * note the user still needs.
       */
      const c = create();
      ready(c);
      infoRequests.sendBack.and.returnValue(throwError(() => ({ status: 500 })));
      let succeeded = 0;
      c.succeeded.subscribe(() => (succeeded += 1));

      await c.send();

      expect(c.isBusy).toBeFalse();
      expect(c.visible).withContext('the note must not be discarded').toBeTrue();
      expect(succeeded).toBe(0);
      expect(toaster.success).not.toHaveBeenCalled();
    });

    it('keeps the selection after a failure so the send can be retried', async () => {
      const c = create();
      const keys = ready(c);
      infoRequests.sendBack.and.returnValue(throwError(() => ({ status: 500 })));

      await c.send();

      expect(c.selected.has(keys[0])).toBeTrue();
      expect(c.note).toBeTruthy();
    });
  });
});
