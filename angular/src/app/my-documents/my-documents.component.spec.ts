import { TestBed, ComponentFixture } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { ConfigStateService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { MyDocumentsComponent } from './my-documents.component';
import { BrandingService } from '../shared/branding/branding.service';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { DocumentStatus } from '../proxy/appointment-documents/document-status.enum';
import { AppointmentDocumentUrls } from '../appointment-documents/appointment-document-urls';

/**
 * #729 My Documents. Every assertion reads the rendered DOM. The access point under test:
 * documents are fetched per appointment, only when it is opened, and a refusal for one
 * appointment must show an error for that appointment and no documents -- never rows.
 * All names and ids are synthetic.
 */
describe('MyDocumentsComponent', () => {
  let appointments: { getList: jasmine.Spy };
  let docs: { getList: jasmine.Spy };
  let http: { get: jasmine.Spy };
  let router: { navigate: jasmine.Spy; navigateByUrl: jasmine.Spy };
  let fixture: ComponentFixture<MyDocumentsComponent>;

  const apptRow = (id: string, conf: string) => ({
    appointment: { id, requestConfirmationNumber: conf },
    appointmentType: { name: 'AME' },
  });
  const doc = (id: string, name: string, status: DocumentStatus, extra = {}) => ({
    id,
    documentName: name,
    fileName: `${name}.pdf`,
    status,
    ...extra,
  });

  function create(items: unknown[] | 'error'): HTMLElement {
    appointments.getList.and.returnValue(
      items === 'error' ? throwError(() => new Error('x')) : of({ items }),
    );
    fixture = TestBed.createComponent(MyDocumentsComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }
  const heads = (el: HTMLElement) => Array.from(el.querySelectorAll<HTMLElement>('.md__head'));
  const click = (b: HTMLElement) => {
    b.click();
    fixture.detectChanges();
  };

  beforeEach(() => {
    appointments = { getList: jasmine.createSpy('getList') };
    docs = { getList: jasmine.createSpy('docs.getList') };
    http = { get: jasmine.createSpy('get') };
    router = {
      navigate: jasmine.createSpy('navigate'),
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    };
    TestBed.configureTestingModule({
      imports: [MyDocumentsComponent],
      providers: [
        { provide: AppointmentService, useValue: appointments },
        { provide: AppointmentDocumentService, useValue: docs },
        { provide: HttpClient, useValue: http },
        { provide: Router, useValue: router },
        { provide: BrandingService, useValue: { logoUrl: () => '', displayName: () => '' } },
        { provide: ToasterService, useValue: { error: jasmine.createSpy('error') } },
        { provide: AppointmentDocumentUrls, useValue: { build: () => '/dl' } },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (k: string) => (k === 'currentUser' ? { email: 'a@test.invalid' } : null),
          },
        },
      ],
    });
  });

  it('shows the empty state when the caller has no appointments', () => {
    const el = create([]);
    expect(el.textContent).toContain('No appointments yet');
    expect(heads(el).length).toBe(0);
  });

  it('shows a load-failure state, not an empty list, when the appointment list fails', () => {
    const el = create('error');
    expect(el.textContent).toContain('Could not load your appointments');
  });

  it('fetches documents only for an appointment that is opened', () => {
    docs.getList.and.returnValue(of([doc('d1', 'Report', DocumentStatus.Accepted)]));
    const el = create([apptRow('a1', 'C1'), apptRow('a2', 'C2')]);
    expect(heads(el).length).toBe(2);
    expect(docs.getList).not.toHaveBeenCalled();

    click(heads(el)[0]);
    expect(docs.getList).toHaveBeenCalledOnceWith('a1');
    expect(el.querySelectorAll('.md__doc').length).toBe(1);
    expect(el.querySelector('.md__doc')?.textContent).toContain('Report');
    expect(el.querySelector('.md__badge')?.textContent).toContain('Accepted');
  });

  it('renders an error and no document rows when the API refuses an appointment', () => {
    docs.getList.and.returnValue(throwError(() => ({ status: 403 })));
    const el = create([apptRow('a1', 'C1')]);
    click(heads(el)[0]);
    expect(el.textContent).toContain('Could not load documents for this appointment');
    expect(el.querySelectorAll('.md__doc').length).toBe(0);
  });

  it('shows the rejection reason on a rejected document', () => {
    docs.getList.and.returnValue(
      of([doc('d1', 'Bad', DocumentStatus.Rejected, { rejectionReason: 'Unreadable scan' })]),
    );
    const el = create([apptRow('a1', 'C1')]);
    click(heads(el)[0]);
    expect(el.querySelector('.md__rej')?.textContent).toContain('Unreadable scan');
  });

  it('filters documents by search text and by status', () => {
    docs.getList.and.returnValue(
      of([doc('d1', 'Alpha', DocumentStatus.Accepted), doc('d2', 'Beta', DocumentStatus.Rejected)]),
    );
    const el = create([apptRow('a1', 'C1')]);
    click(heads(el)[0]);
    expect(el.querySelectorAll('.md__doc').length).toBe(2);

    const search = el.querySelector<HTMLInputElement>('#md-search')!;
    search.value = 'beta';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(el.querySelectorAll('.md__doc').length).toBe(1);
    expect(el.querySelector('.md__doc')?.textContent).toContain('Beta');

    search.value = '';
    search.dispatchEvent(new Event('input'));
    const sel = el.querySelector<HTMLSelectElement>('#md-status')!;
    sel.value = sel.options[1].value; // Accepted
    sel.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(el.querySelectorAll('.md__doc').length).toBe(1);
    expect(el.querySelector('.md__doc')?.textContent).toContain('Alpha');
  });

  it('opens the appointment from the upload link', () => {
    docs.getList.and.returnValue(of([]));
    const el = create([apptRow('a1', 'C1')]);
    click(heads(el)[0]);
    el.querySelector<HTMLElement>('.md__open')!.click();
    expect(router.navigate).toHaveBeenCalledWith(['/appointments/view', 'a1']);
  });
});
