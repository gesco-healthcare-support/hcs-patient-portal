import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { PermissionService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { AppointmentDocumentsComponent } from './appointment-documents.component';
import { AppointmentDocumentUrls } from './appointment-document-urls';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { DocumentStatus } from '../proxy/appointment-documents/document-status.enum';
import type { AppointmentDocumentDto } from '../proxy/appointment-documents/models';

/**
 * 2026-10-14 -- "Upload replacement" on a rejected document.
 *
 * <p>Before this the panel offered no way to fix a rejected document except a NEW upload, so the
 * rejected row stayed Rejected, no version of the old file was recorded, and a required document
 * never counted as received. The button replaces the file IN PLACE through
 * POST .../documents/{id}/replace, and the panel announces it through documentProvided, which the
 * external fix-it flow now uses instead of a self-reported click.</p>
 *
 * <p>All names and identifiers are synthetic.</p>
 */
describe('AppointmentDocumentsComponent replacing a rejected document', () => {
  let service: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let rest: { request: jasmine.Spy };
  let granted: boolean;
  let changed: number;
  let provided: number;

  function doc(over: Partial<AppointmentDocumentDto> = {}): AppointmentDocumentDto {
    return {
      id: 'doc-1',
      documentName: 'Intake form',
      fileName: 'intake-form.pdf',
      status: DocumentStatus.Rejected,
      rejectionReason: 'Unreadable scan',
      ...over,
    } as AppointmentDocumentDto;
  }

  function create(canUpload = true) {
    granted = canUpload;
    changed = 0;
    provided = 0;
    service = {
      getList: jasmine.createSpy('getList').and.returnValue(of([])),
      getMissingRequiredDocuments: jasmine
        .createSpy('getMissingRequiredDocuments')
        .and.returnValue(of(null)),
    };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    rest = { request: jasmine.createSpy('request').and.returnValue(of({})) };

    TestBed.configureTestingModule({
      imports: [AppointmentDocumentsComponent],
      providers: [
        { provide: AppointmentDocumentService, useValue: service },
        { provide: ToasterService, useValue: toaster },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => granted } },
        { provide: RestService, useValue: rest },
        { provide: AppointmentDocumentUrls, useValue: { build: () => 'https://api.test/doc' } },
        { provide: HttpClient, useValue: { get: () => of({ body: new Blob(['x']) }) } },
      ],
    });
    const fixture = TestBed.createComponent(AppointmentDocumentsComponent);
    const c = fixture.componentInstance;
    c.appointmentId = 'appt-1';
    c.documentsChanged.subscribe(() => (changed += 1));
    c.documentProvided.subscribe(() => (provided += 1));
    return { fixture, c };
  }

  const file = (bytes = 10) => new File([new Uint8Array(bytes)], 'rescanned.pdf');

  afterEach(() => TestBed.resetTestingModule());

  it('offers "Upload replacement" on a rejected row only', () => {
    const { fixture, c } = create();
    c.documents = [doc(), doc({ id: 'doc-2', status: DocumentStatus.Uploaded })];
    fixture.detectChanges();

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('.replace-btn');
    expect(buttons.length).toBe(1);
    expect(buttons[0].textContent).toContain('Upload replacement');
  });

  it('does not offer it to someone who may not upload', () => {
    const { fixture, c } = create(false);
    c.documents = [doc()];
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.replace-btn')).toBeNull();
  });

  it("sends the file to the row's own replace route, then refreshes and says so", () => {
    const { c } = create();

    c.replace(doc(), file());

    const [request] = rest.request.calls.mostRecent().args;
    expect(request.method).toBe('POST');
    expect(request.url).toBe('/api/app/appointments/appt-1/documents/doc-1/replace');
    expect((request.body as FormData).get('file')).toBeTruthy();
    expect(toaster.success).toHaveBeenCalledWith('Replacement uploaded.');
    expect(service['getList']).toHaveBeenCalledWith('appt-1');
    expect(changed).toBe(1);
    expect(provided).toBe(1);
    expect(c.replacingDoc).toBeNull();
  });

  it('opens the shared picker for the chosen row and replaces with the picked file', () => {
    const { c } = create();
    const picker = document.createElement('input');
    picker.type = 'file';
    const click = spyOn(picker, 'click');

    c.startReplace(doc(), picker);
    expect(click).toHaveBeenCalled();
    expect(c.replacingDoc?.id).toBe('doc-1');

    // A second click while one is in flight is ignored.
    c.startReplace(doc({ id: 'doc-2' }), picker);
    expect(c.replacingDoc?.id).toBe('doc-1');

    const picked = file();
    c.onReplacementSelected({ target: { files: [picked] } } as unknown as Event);
    expect(rest.request.calls.mostRecent().args[0].url).toContain('/documents/doc-1/replace');
  });

  it('sends nothing when the picker is cancelled', () => {
    const { c } = create();
    c.replacingDoc = doc();

    c.onReplacementSelected({ target: { files: [] } } as unknown as Event);

    expect(rest.request).not.toHaveBeenCalled();
    expect(c.replacingDoc).toBeNull();
  });

  it('refuses a file over the cap before sending it', () => {
    const { c } = create();

    c.replace(doc(), file(c.maxBytes + 1));

    expect(rest.request).not.toHaveBeenCalled();
    expect(toaster.error).toHaveBeenCalled();
    expect(c.replacingDoc).toBeNull();
  });

  it('frees the row again when the server refuses', () => {
    const { c } = create();
    rest.request.and.returnValue(throwError(() => new Error('synthetic refusal')));

    c.replace(doc(), file());

    expect(c.replacingDoc).toBeNull();
    expect(provided).toBe(0);
  });

  it('announces a NEW upload as a provided document too', () => {
    const { c } = create();
    c.selectedFile = file();

    c.upload();

    expect(provided).toBe(1);
  });
});
