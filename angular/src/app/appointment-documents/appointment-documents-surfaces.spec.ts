import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { PermissionService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { AppointmentDocumentsComponent } from './appointment-documents.component';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentDocumentUrls } from './appointment-document-urls';
import { DocumentStatus } from '../proxy/appointment-documents/document-status.enum';
import { RequiredDocumentState } from '../proxy/appointment-documents/required-document-state.enum';
import type { AppointmentDocumentDto } from '../proxy/appointment-documents/models';

/**
 * The appointment-documents panel, embedded in the appointment detail view.
 *
 * <p>It sat at 97 of 197 lines uncovered. Four specs already exist for it and each is narrow:
 * the strike-list badge dedup, the #612 document-name derivation (three blocks of it), the
 * download helper and the upload validation constants. None of that is repeated here.</p>
 *
 * <p>What this covers is the rest of the panel: the load chain, the type filter, the
 * required-documents indicator, and the approve / reject / delete actions. Two of those carry
 * guarantees worth stating. `allRequiredReceived` must stay FALSE when the appointment type has
 * no required documents at all -- otherwise a type with no requirements would display as "all
 * required documents received", which is a different and much stronger claim. And both approve
 * and reject are permission-gated in the component, not only in the template, so a stale click
 * cannot fire a request the server will refuse.</p>
 *
 * <p>All document names, file names and identifiers below are synthetic.</p>
 */
describe('AppointmentDocumentsComponent surfaces', () => {
  let service: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let rest: { request: jasmine.Spy };
  let http: { get: jasmine.Spy };
  let urls: { build: jasmine.Spy };
  let granted: boolean;
  let changed: number;

  const APPROVE_POLICY = 'CaseEvaluation.AppointmentDocuments.Approve';

  function doc(over: Partial<AppointmentDocumentDto> = {}): AppointmentDocumentDto {
    return {
      id: 'doc-1',
      documentName: 'Intake form',
      fileName: 'intake-form.pdf',
      ...over,
    } as AppointmentDocumentDto;
  }

  function create(options: { canApprove?: boolean } = {}): AppointmentDocumentsComponent {
    granted = options.canApprove ?? false;
    changed = 0;

    service = {
      getList: jasmine.createSpy('getList').and.returnValue(of([])),
      getMissingRequiredDocuments: jasmine
        .createSpy('getMissingRequiredDocuments')
        .and.returnValue(of(null)),
      delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
      approve: jasmine.createSpy('approve').and.returnValue(of(undefined)),
      reject: jasmine.createSpy('reject').and.returnValue(of(undefined)),
    };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    rest = { request: jasmine.createSpy('request').and.returnValue(of([])) };
    http = {
      get: jasmine.createSpy('get').and.returnValue(of({ body: new Blob(['x']) })),
    };
    urls = { build: jasmine.createSpy('build').and.returnValue('https://api.test/doc') };

    TestBed.configureTestingModule({
      imports: [AppointmentDocumentsComponent],
      providers: [
        { provide: AppointmentDocumentService, useValue: service },
        { provide: ToasterService, useValue: toaster },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => granted } },
        { provide: RestService, useValue: rest },
        { provide: AppointmentDocumentUrls, useValue: urls },
        { provide: HttpClient, useValue: http },
      ],
    });

    const component = TestBed.createComponent(AppointmentDocumentsComponent).componentInstance;
    component.appointmentId = 'appt-1';
    component.documentsChanged.subscribe(() => (changed += 1));
    return component;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('reacting to the appointment id', () => {
    it('loads the list and the type options when the id arrives', () => {
      const c = create();
      c.ngOnChanges({ appointmentId: {} as never });
      expect(service['getList']).toHaveBeenCalledWith('appt-1');
      expect(rest.request).toHaveBeenCalled();
    });

    it('does nothing while the id is still null', () => {
      const c = create();
      c.appointmentId = null;
      c.ngOnChanges({ appointmentId: {} as never });
      expect(service['getList']).not.toHaveBeenCalled();
    });

    it('ignores a change to some other input', () => {
      const c = create();
      c.ngOnChanges({ somethingElse: {} as never });
      expect(service['getList']).not.toHaveBeenCalled();
    });
  });

  describe('loading the document list', () => {
    it('stores the rows and clears the loading flag', () => {
      const c = create();
      service['getList'].and.returnValue(of([doc()]));
      c.refresh();
      expect(c.documents.length).toBe(1);
      expect(c.isLoading).toBeFalse();
    });

    it('treats a null payload as an empty list', () => {
      const c = create();
      service['getList'].and.returnValue(of(null));
      c.refresh();
      expect(c.documents).toEqual([]);
    });

    it('clears the loading flag when the list request fails', () => {
      const c = create();
      service['getList'].and.returnValue(throwError(() => ({ status: 500 })));
      c.refresh();
      expect(c.isLoading).toBeFalse();
    });

    it('reloads the required-documents indicator alongside the list', () => {
      // The indicator reflects each approve / reject / upload / delete, so it cannot
      // be loaded once and left.
      const c = create();
      c.refresh();
      expect(service['getMissingRequiredDocuments']).toHaveBeenCalledWith('appt-1');
    });

    it('does not fetch anything without an appointment id', () => {
      const c = create();
      c.appointmentId = null;
      c.refresh();
      c.loadMissingRequired();
      c.loadDocumentTypeOptions();
      expect(service['getList']).not.toHaveBeenCalled();
      expect(service['getMissingRequiredDocuments']).not.toHaveBeenCalled();
      expect(rest.request).not.toHaveBeenCalled();
    });
  });

  describe('the required-documents indicator', () => {
    it('stores the result', () => {
      const c = create();
      service['getMissingRequiredDocuments'].and.returnValue(of({ requiredCount: 3, missing: [] }));
      c.loadMissingRequired();
      expect(c.requiredCount).toBe(3);
    });

    it('hides itself rather than blocking the panel when the request fails', () => {
      const c = create();
      c.requiredStatus = { requiredCount: 2, missing: [] } as never;
      service['getMissingRequiredDocuments'].and.returnValue(throwError(() => ({ status: 500 })));
      c.loadMissingRequired();
      expect(c.requiredStatus).toBeNull();
      expect(c.requiredCount).toBe(0);
      expect(c.missingRequired).toEqual([]);
    });

    it('does NOT claim all required documents are in when none are required', () => {
      /**
       * An appointment type with no required documents has an empty `missing` list, which
       * on its own reads identically to "every requirement satisfied". The count has to
       * gate it, or a type with no requirements would display a completeness claim it has
       * not earned.
       */
      const c = create();
      c.requiredStatus = { requiredCount: 0, missing: [] } as never;
      expect(c.allRequiredReceived).toBeFalse();
    });

    it('claims completion only when something was required and nothing is missing', () => {
      const c = create();
      c.requiredStatus = { requiredCount: 2, missing: [] } as never;
      expect(c.allRequiredReceived).toBeTrue();
    });

    it('does not claim completion while something is still missing', () => {
      const c = create();
      c.requiredStatus = {
        requiredCount: 2,
        missing: [{ documentTypeName: 'Medical Report' }],
      } as never;
      expect(c.allRequiredReceived).toBeFalse();
      expect(c.missingRequired.length).toBe(1);
    });

    it('names each state a missing document can be in', () => {
      const c = create();
      expect(c.requiredStateLabel(RequiredDocumentState.AwaitingReview)).toBe('Awaiting review');
      expect(c.requiredStateLabel(RequiredDocumentState.Rejected)).toBe('Rejected');
      expect(c.requiredStateLabel(undefined)).toBe('Not uploaded');
    });
  });

  describe('the document-type options', () => {
    it('stores the rows the endpoint returns', () => {
      const c = create();
      rest.request.and.returnValue(of([{ id: 't-1', displayName: 'Medical Report' }]));
      c.loadDocumentTypeOptions();
      expect(c.documentTypes.length).toBe(1);
    });

    it('treats a null payload as no options', () => {
      const c = create();
      rest.request.and.returnValue(of(null));
      c.loadDocumentTypeOptions();
      expect(c.documentTypes).toEqual([]);
    });

    it('offers no admin types rather than failing when the lookup errors', () => {
      const c = create();
      rest.request.and.returnValue(throwError(() => ({ status: 500 })));
      c.loadDocumentTypeOptions();
      expect(c.documentTypes).toEqual([]);
    });
  });

  describe('the type filter', () => {
    function seeded(): AppointmentDocumentsComponent {
      const c = create();
      c.documentTypes = [
        { id: 't-1', displayName: 'Medical Report' },
        { id: 't-2', displayName: 'Panel Strike List' },
      ] as never;
      c.documents = [
        doc({ id: 'a', appointmentDocumentTypeId: 't-1' }),
        doc({ id: 'b', appointmentDocumentTypeId: 't-1' }),
        doc({ id: 'c', appointmentDocumentTypeId: 't-2' }),
        doc({ id: 'd' }),
      ];
      return c;
    }

    it('lists each present category once', () => {
      const c = seeded();
      expect(c.filterTypeOptions).toEqual([
        { id: 't-1', label: 'Medical Report' },
        { id: 't-2', label: 'Panel Strike List' },
      ]);
    });

    it('omits a category whose label cannot be resolved', () => {
      // A document can carry a type id the current options list does not describe (the
      // admin deactivated it); a blank dropdown entry would be unselectable noise.
      const c = seeded();
      c.documents = [doc({ id: 'x', appointmentDocumentTypeId: 'gone' })];
      expect(c.filterTypeOptions).toEqual([]);
    });

    it('reports whether anything is uncategorized', () => {
      const c = seeded();
      expect(c.hasUncategorized).toBeTrue();

      c.documents = [doc({ id: 'a', appointmentDocumentTypeId: 't-1' })];
      expect(c.hasUncategorized).toBeFalse();
    });

    it('shows everything by default', () => {
      const c = seeded();
      expect(c.filteredDocuments.length).toBe(4);
    });

    it('narrows to one category', () => {
      const c = seeded();
      c.selectedFilterTypeId = 't-1';
      expect(c.filteredDocuments.map((d) => d.id)).toEqual(['a', 'b']);
    });

    it('gathers the untyped documents into the uncategorized bucket', () => {
      const c = seeded();
      c.selectedFilterTypeId = c.OTHER_FILTER;
      expect(c.filteredDocuments.map((d) => d.id)).toEqual(['d']);
    });
  });

  describe('uploading', () => {
    const file = (name: string, size = 10) => {
      const f = new File(['x'.repeat(size)], name, { type: 'application/pdf' });
      return f;
    };

    it('refuses a file above the server cap and names the cap', () => {
      // The client cap exists to fail fast; the authoritative one is the server's.
      const c = create();
      const big = file('huge.pdf');
      Object.defineProperty(big, 'size', { value: c.maxBytes + 1 });
      c.selectedFile = big;

      c.upload();

      expect(rest.request).not.toHaveBeenCalled();
      expect(toaster.error).toHaveBeenCalled();
      expect(toaster.error.calls.mostRecent().args[0]).toContain('MB upload cap');
    });

    it('refuses an "Other" type with no label typed', () => {
      const c = create();
      c.selectedFile = file('scan.pdf');
      c.selectedDocumentTypeId = c.OTHER_TYPE_VALUE;
      c.otherDocumentTypeName = '   ';

      c.upload();

      expect(rest.request).not.toHaveBeenCalled();
      expect(toaster.error).toHaveBeenCalledWith('Enter a label for the "Other" document type.');
    });

    it('does nothing without a file or without an appointment', () => {
      const c = create();
      c.upload();
      expect(rest.request).not.toHaveBeenCalled();

      c.selectedFile = file('scan.pdf');
      c.appointmentId = null;
      c.upload();
      expect(rest.request).not.toHaveBeenCalled();
    });

    it('does nothing while an upload is already running', () => {
      const c = create();
      c.selectedFile = file('scan.pdf');
      c.isUploading = true;
      c.upload();
      expect(rest.request).not.toHaveBeenCalled();
    });

    it('sends the chosen admin category', () => {
      const c = create();
      c.selectedFile = file('scan.pdf');
      c.selectedDocumentTypeId = 't-1';
      c.upload();

      const body = rest.request.calls.mostRecent().args[0].body as FormData;
      expect(body.get('appointmentDocumentTypeId')).toBe('t-1');
      expect(body.get('otherDocumentTypeName')).toBeNull();
    });

    it('sends the free-text label instead when the category is "Other"', () => {
      const c = create();
      c.selectedFile = file('scan.pdf');
      c.selectedDocumentTypeId = c.OTHER_TYPE_VALUE;
      c.otherDocumentTypeName = '  Employer letter  ';
      c.upload();

      const body = rest.request.calls.mostRecent().args[0].body as FormData;
      expect(body.get('otherDocumentTypeName')).toBe('Employer letter');
      expect(body.get('appointmentDocumentTypeId')).toBeNull();
    });

    it('falls back to the file name when the name box is blank', () => {
      const c = create();
      c.selectedFile = file('scan.pdf');
      c.documentName = '   ';
      c.upload();

      const body = rest.request.calls.mostRecent().args[0].body as FormData;
      expect(body.get('documentName')).toBe('scan.pdf');
    });

    it('announces the change so the parent can refresh its packet panel', () => {
      const c = create();
      c.selectedFile = file('scan.pdf');
      c.upload();
      expect(changed).toBe(1);
    });
  });

  describe('downloading', () => {
    it('fetches the built url as a blob rather than opening a tab', () => {
      /**
       * A new tab carries no Authorization header, so window.open 401s under JWT auth.
       * The fetch has to go through HttpClient for ABP's interceptor to attach the token.
       */
      const c = create();
      c.download(doc());
      expect(urls.build).toHaveBeenCalledWith('appt-1', 'doc-1');
      expect(http.get).toHaveBeenCalled();
      expect(http.get.calls.mostRecent().args[1].responseType).toBe('blob');
    });

    it('says so when the download fails', () => {
      const c = create();
      http.get.and.returnValue(throwError(() => ({ status: 404 })));
      c.download(doc());
      expect(toaster.error).toHaveBeenCalledWith('Could not download document.');
    });

    it('does nothing for a row with no id or without an appointment', () => {
      const c = create();
      c.download(doc({ id: undefined }));
      expect(http.get).not.toHaveBeenCalled();

      c.appointmentId = null;
      c.download(doc());
      expect(http.get).not.toHaveBeenCalled();
    });
  });

  describe('deleting', () => {
    it('does nothing when the confirmation is declined', () => {
      const c = create();
      spyOn(window, 'confirm').and.returnValue(false);
      c.delete(doc());
      expect(service['delete']).not.toHaveBeenCalled();
    });

    it('deletes, reports and refreshes once confirmed', () => {
      const c = create();
      spyOn(window, 'confirm').and.returnValue(true);
      service['getList'].calls.reset();

      c.delete(doc());

      expect(service['delete']).toHaveBeenCalledWith('appt-1', 'doc-1');
      expect(toaster.success).toHaveBeenCalledWith('Document deleted.');
      expect(service['getList']).toHaveBeenCalled();
      expect(changed).toBe(1);
    });

    it('names the document in the confirmation prompt', () => {
      const c = create();
      const confirmSpy = spyOn(window, 'confirm').and.returnValue(false);
      c.delete(doc({ documentName: 'Intake form' }));
      expect(confirmSpy.calls.mostRecent().args[0]).toContain('Intake form');
    });

    it('does nothing for a row with no id', () => {
      const c = create();
      spyOn(window, 'confirm').and.returnValue(true);
      c.delete(doc({ id: undefined }));
      expect(service['delete']).not.toHaveBeenCalled();
    });
  });

  describe('approving', () => {
    it('refuses without the approve permission', () => {
      // The button is hidden too, but a stale click must not reach a request the
      // server will refuse.
      const c = create({ canApprove: false });
      c.approve(doc());
      expect(service['approve']).not.toHaveBeenCalled();
    });

    it('approves, reports and refreshes when permitted', () => {
      const c = create({ canApprove: true });
      service['getList'].calls.reset();

      c.approve(doc());

      expect(service['approve']).toHaveBeenCalledWith('appt-1', 'doc-1');
      expect(toaster.success).toHaveBeenCalledWith('Document approved.');
      expect(service['getList']).toHaveBeenCalled();
      expect(changed).toBe(1);
    });

    it('does nothing for a row with no id', () => {
      const c = create({ canApprove: true });
      c.approve(doc({ id: undefined }));
      expect(service['approve']).not.toHaveBeenCalled();
    });
  });

  describe('rejecting', () => {
    it('refuses to open the modal without the approve permission', () => {
      const c = create({ canApprove: false });
      c.openRejectModal(doc());
      expect(c.rejectingDoc).toBeNull();
    });

    it('opens the modal pre-filled with any existing reason', () => {
      const c = create({ canApprove: true });
      c.openRejectModal(doc({ rejectionReason: 'Illegible scan' }));
      expect(c.rejectingDoc).not.toBeNull();
      expect(c.rejectionReason).toBe('Illegible scan');
    });

    it('opens with an empty reason when the document has none', () => {
      const c = create({ canApprove: true });
      c.openRejectModal(doc());
      expect(c.rejectionReason).toBe('');
    });

    it('clears the in-flight flag when the modal closes', () => {
      // Leaving it set would keep the submit button disabled the next time the modal
      // is opened, with nothing on screen explaining why.
      const c = create({ canApprove: true });
      c.openRejectModal(doc());
      c.isSubmittingReject = true;
      c.closeRejectModal();
      expect(c.rejectingDoc).toBeNull();
      expect(c.rejectionReason).toBe('');
      expect(c.isSubmittingReject).toBeFalse();
    });

    it('requires a reason', () => {
      const c = create({ canApprove: true });
      c.openRejectModal(doc());
      c.rejectionReason = '   ';
      c.submitReject();
      expect(service['reject']).not.toHaveBeenCalled();
      expect(toaster.error).toHaveBeenCalledWith('Rejection reason is required.');
    });

    it('caps the reason at 500 characters', () => {
      const c = create({ canApprove: true });
      c.openRejectModal(doc());
      c.rejectionReason = 'x'.repeat(501);
      c.submitReject();
      expect(service['reject']).not.toHaveBeenCalled();
      expect(toaster.error).toHaveBeenCalledWith('Rejection reason exceeds 500 characters.');
    });

    it('accepts a reason of exactly 500 characters', () => {
      const c = create({ canApprove: true });
      c.openRejectModal(doc());
      c.rejectionReason = 'x'.repeat(500);
      c.submitReject();
      expect(service['reject']).toHaveBeenCalled();
    });

    it('sends the trimmed reason, then closes and refreshes', () => {
      const c = create({ canApprove: true });
      service['getList'].calls.reset();
      c.openRejectModal(doc());
      c.rejectionReason = '  Illegible scan  ';

      c.submitReject();

      expect(service['reject']).toHaveBeenCalledWith('appt-1', 'doc-1', {
        reason: 'Illegible scan',
      });
      expect(c.rejectingDoc).toBeNull();
      expect(service['getList']).toHaveBeenCalled();
      expect(changed).toBe(1);
    });

    it('keeps the modal open and releases the button when the reject fails', () => {
      const c = create({ canApprove: true });
      service['reject'].and.returnValue(throwError(() => ({ status: 500 })));
      c.openRejectModal(doc());
      c.rejectionReason = 'Illegible scan';

      c.submitReject();

      expect(c.rejectingDoc).withContext('the user must be able to retry').not.toBeNull();
      expect(c.isSubmittingReject).toBeFalse();
    });

    it('does nothing without an appointment or an open document', () => {
      const c = create({ canApprove: true });
      c.submitReject();
      expect(service['reject']).not.toHaveBeenCalled();
    });
  });

  describe('display helpers', () => {
    it('names each document status', () => {
      const c = create();
      expect(c.statusLabel(DocumentStatus.Accepted)).toBe('Approved');
      expect(c.statusLabel(DocumentStatus.Rejected)).toBe('Rejected');
      expect(c.statusLabel(undefined as never)).toBe('Uploaded');
    });

    it('colours each document status', () => {
      const c = create();
      expect(c.statusBadgeClass(DocumentStatus.Accepted)).toBe('bg-success');
      expect(c.statusBadgeClass(DocumentStatus.Rejected)).toBe('bg-danger');
      expect(c.statusBadgeClass(undefined as never)).toBe('bg-secondary');
    });

    it('scales a byte count to the right unit', () => {
      const c = create();
      expect(c.formatBytes(512)).toBe('512 B');
      expect(c.formatBytes(1024)).toBe('1.0 KB');
      expect(c.formatBytes(1024 * 1024 - 1)).toContain('KB');
      expect(c.formatBytes(1024 * 1024)).toBe('1.0 MB');
    });

    it('prefers a free-text label over an admin category', () => {
      const c = create();
      c.documentTypes = [{ id: 't-1', displayName: 'Medical Report' }] as never;
      expect(c.documentTypeLabel(doc({ otherDocumentTypeName: 'Employer letter' }))).toBe(
        'Employer letter',
      );
    });

    it('resolves an admin category id to its label', () => {
      const c = create();
      c.documentTypes = [{ id: 't-1', displayName: 'Medical Report' }] as never;
      expect(c.documentTypeLabel(doc({ appointmentDocumentTypeId: 't-1' }))).toBe('Medical Report');
    });

    it('returns no label for an untyped document or an unresolvable id', () => {
      const c = create();
      c.documentTypes = [];
      expect(c.documentTypeLabel(doc())).toBeNull();
      expect(c.documentTypeLabel(doc({ appointmentDocumentTypeId: 'gone' }))).toBeNull();
    });
  });

  describe('the approve permission', () => {
    it('follows the granted policy', () => {
      expect(create({ canApprove: true }).canApprove).toBeTrue();
      TestBed.resetTestingModule();
      expect(create({ canApprove: false }).canApprove).toBeFalse();
      expect(APPROVE_POLICY).toBeTruthy();
    });
  });

  /**
   * Two guards the blocks above do not reach: deleting with no appointment loaded, and
   * rejecting a document that has no id. Neither may reach the server, and the delete guard
   * must refuse before the confirmation prompt is shown.
   */
  describe('guards without an appointment or a document id', () => {
    it('does not prompt or delete while no appointment is loaded', () => {
      const c = create();
      c.appointmentId = null;
      const confirmSpy = spyOn(window, 'confirm').and.returnValue(true);

      c.delete(doc());

      expect(confirmSpy).not.toHaveBeenCalled();
      expect(service['delete']).not.toHaveBeenCalled();
    });

    it('does not send a rejection for a document with no id', () => {
      const c = create({ canApprove: true });
      c.openRejectModal(doc({ id: undefined }));
      c.rejectionReason = 'Illegible scan';

      c.submitReject();

      expect(service['reject']).not.toHaveBeenCalled();
      expect(c.isSubmittingReject).toBeFalse();
    });
  });
});
