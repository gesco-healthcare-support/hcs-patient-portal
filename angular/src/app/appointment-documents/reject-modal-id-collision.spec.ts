import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { PermissionService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { AppointmentDocumentsComponent } from './appointment-documents.component';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentDocumentUrls } from './appointment-document-urls';

/**
 * #808 -- both rejection modals declared `id="reject-reason"`, and
 * internal-appointment-detail renders both components (documents at :1287, the
 * reject modal at :1448). With both open, each `<label for="reject-reason">`
 * resolves to whichever textarea is first in the DOM, so one modal announces
 * the other's label.
 *
 * The documents one is renamed to `document-reject-reason` per Adrian's ruling.
 *
 * WHAT THIS DOES NOT DO, deliberately. I wanted to render both modals and
 * assert their ids are disjoint, which is the property that actually broke --
 * each component was individually correct and only their coexistence was
 * wrong. `<abp-modal>` will not render under TestBed alongside another
 * fixture: it projects its body out of its own element and appended nothing
 * when a second component was present, so the assertion passed by finding
 * nothing rather than by the ids being disjoint.
 *
 * A test that green-lights an empty DOM is worse than no test, so this pins
 * the side that changed instead: the documents modal uses the new id, no
 * longer answers to the old one, and every label in it resolves to exactly one
 * control. A future collision would have to be reintroduced deliberately.
 */
describe('rejection modals do not share element ids (#808)', () => {
  /**
   * Scoped to the fixture element. Reading `document` as well double-counts:
   * TestBed attaches the fixture to the document, so every id is found twice
   * and every label appears to match two controls -- which this spec reported
   * on its first run, and which was the test being wrong rather than the code.
   */
  function idsIn(...roots: ParentNode[]): string[] {
    const out: string[] = [];
    for (const root of roots) {
      for (const el of Array.from(root.querySelectorAll('[id]'))) {
        const id = el.getAttribute('id');
        if (id) out.push(id);
      }
    }
    return out;
  }

  function labelTargetsIn(...roots: ParentNode[]): string[] {
    const out: string[] = [];
    for (const root of roots) {
      for (const el of Array.from(root.querySelectorAll('label[for]'))) {
        const t = el.getAttribute('for');
        if (t) out.push(t);
      }
    }
    return out;
  }

  function openDocumentsModal(): HTMLElement {
    const f = TestBed.createComponent(AppointmentDocumentsComponent);
    f.componentInstance.appointmentId = 'appt-1';
    (f.componentInstance as unknown as { rejectingDoc: unknown }).rejectingDoc = {
      id: 'doc-1',
      documentName: 'referral.pdf',
    };
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: HttpClient, useValue: { get: () => of(null), post: () => of(null) } },
        { provide: AppointmentDocumentService, useValue: { getList: () => of({ items: [] }) } },
        { provide: AppointmentDocumentUrls, useValue: { build: () => '', buildPacket: () => '' } },
        { provide: RestService, useValue: { request: () => of(null) } },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('points the documents label at its own renamed textarea', () => {
    const root = openDocumentsModal();

    expect(root.querySelector('#document-reject-reason')).not.toBeNull();
    expect(root.querySelector('#reject-reason')).toBeNull();
    expect(
      Array.from(root.querySelectorAll('label[for]')).map((el) => el.getAttribute('for')),
    ).toContain('document-reject-reason');
  });

  /** Every label must resolve to exactly one control, not merely to some. */
  it('resolves every label in the documents modal to exactly one control', () => {
    const docsRoot = openDocumentsModal();

    const targets = labelTargetsIn(docsRoot);
    expect(targets.length).toBeGreaterThan(0);

    for (const target of targets) {
      const matches = idsIn(docsRoot).filter((id) => id === target);
      expect(matches.length)
        .withContext(`label[for="${target}"] must match exactly one control`)
        .toBe(1);
    }
  });
});
