import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormBuilder } from '@angular/forms';
import { ChangeDetectorRef } from '@angular/core';
import { of } from 'rxjs';
import { EnvironmentService, PermissionService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { AppointmentDocumentsComponent } from './appointment-documents.component';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentDocumentUrls } from './appointment-document-urls';
import { AppointmentReportComponent } from '../reports/appointment-report.component';
import { ReportService } from '../proxy/reports';
import { AppointmentTypeService } from '../proxy/appointment-types';
import { LocationService } from '../proxy/locations';

/**
 * #628 replaced `document.body.removeChild(anchor)` with `anchor.remove()` in
 * both download helpers (S7762). Neither had any coverage, so the swap would
 * have gone in unverified.
 *
 * The property worth asserting is not which API removes it but that the
 * synthetic anchor leaves the document at all. These helpers append a real
 * element to `document.body` on every download; a leak there accumulates
 * invisible nodes for the whole session, and nothing else in the app would
 * notice.
 */
function captureAnchors(): HTMLAnchorElement[] {
  const anchors: HTMLAnchorElement[] = [];
  const realCreate = document.createElement.bind(document);
  spyOn(document, 'createElement').and.callFake((tag: string) => {
    const el = realCreate(tag);
    if (tag === 'a') {
      spyOn(el as HTMLAnchorElement, 'click').and.stub();
      anchors.push(el as HTMLAnchorElement);
    }
    return el;
  });
  spyOn(URL, 'createObjectURL').and.returnValue('blob:test');
  spyOn(URL, 'revokeObjectURL').and.stub();
  return anchors;
}

describe('download helpers clean up their synthetic anchor (#628)', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('appointment-documents removes the anchor after clicking it', () => {
    const anchors = captureAnchors();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: HttpClient,
          useValue: { get: () => of(new HttpResponse({ body: new Blob(['x']), status: 200 })) },
        },
        { provide: AppointmentDocumentService, useValue: { getList: () => of({ items: [] }) } },
        {
          provide: AppointmentDocumentUrls,
          useValue: { buildDownload: () => '/doc', build: () => '/doc' },
        },
        { provide: RestService, useValue: { request: () => of(null) } },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    const c = TestBed.runInInjectionContext(() => new AppointmentDocumentsComponent());
    c.appointmentId = 'appt-1';

    c.download({ id: 'doc-1', fileName: 'referral.pdf' } as never);

    expect(anchors).toHaveSize(1);
    expect(anchors[0].download).toBe('referral.pdf');
    expect(anchors[0].isConnected).toBeFalse();
  });

  it('appointment-report removes the anchor after exporting', async () => {
    const anchors = captureAnchors();
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        {
          provide: HttpClient,
          useValue: {
            get: () =>
              of(
                new HttpResponse({
                  body: new Blob(['a,b']),
                  headers: new HttpHeaders({ 'content-disposition': 'attachment; filename=r.csv' }),
                  status: 200,
                }),
              ),
          },
        },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
        { provide: ChangeDetectorRef, useValue: { markForCheck: () => undefined } },
        { provide: EnvironmentService, useValue: { getApiUrl: () => '' } },
        { provide: ReportService, useValue: { getReport: () => of({ items: [], totalCount: 0 }) } },
        {
          provide: AppointmentTypeService,
          useValue: { getList: () => of({ items: [], totalCount: 0 }) },
        },
        { provide: LocationService, useValue: { getList: () => of({ items: [], totalCount: 0 }) } },
      ],
    });
    const c = TestBed.runInInjectionContext(() => new AppointmentReportComponent()) as unknown as {
      download(kind: string): Promise<void>;
    };

    await c.download('csv');

    expect(anchors).toHaveSize(1);
    expect(anchors[0].download).toBe('r.csv');
    expect(anchors[0].isConnected).toBeFalse();
  });
});
