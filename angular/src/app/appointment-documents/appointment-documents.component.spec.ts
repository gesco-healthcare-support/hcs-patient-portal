import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { PermissionService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentDocumentDto } from '../proxy/appointment-documents/models';
import { LookupDto } from '../proxy/shared/models';
import { AppointmentDocumentUrls } from './appointment-document-urls';
import { AppointmentDocumentsComponent } from './appointment-documents.component';

/**
 * QA item J: a panel-strike-list document must show exactly ONE "Panel Strike
 * List" badge. The status surfaces both via the document category label
 * (documentTypeLabel) and the IsPanelStrikeList flag; the standalone flag badge
 * must defer to the category badge when they would coincide, while still
 * rendering when the flag stands alone (different or absent category).
 */
describe('AppointmentDocumentsComponent strike-list badge dedup (QA item J)', () => {
  const STRIKE_LIST_TYPE_ID = 'strike-list-type-id';
  const STRIKE_LIST_LABEL = 'Panel Strike List';

  let component: AppointmentDocumentsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppointmentDocumentsComponent],
      providers: [
        { provide: AppointmentDocumentService, useValue: {} },
        { provide: ToasterService, useValue: {} },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => false } },
        { provide: RestService, useValue: { request: () => ({ subscribe: () => undefined }) } },
        { provide: AppointmentDocumentUrls, useValue: {} },
        { provide: HttpClient, useValue: {} },
      ],
    }).compileComponents();

    component = TestBed.createComponent(AppointmentDocumentsComponent).componentInstance;
    component.documentTypes = [
      { id: STRIKE_LIST_TYPE_ID, displayName: STRIKE_LIST_LABEL } as LookupDto<string>,
      { id: 'med-report-id', displayName: 'Medical Report' } as LookupDto<string>,
    ];
  });

  const doc = (partial: Partial<AppointmentDocumentDto>): AppointmentDocumentDto =>
    partial as AppointmentDocumentDto;

  it('suppresses the flag badge when the category badge already says "Panel Strike List"', () => {
    const d = doc({ isPanelStrikeList: true, appointmentDocumentTypeId: STRIKE_LIST_TYPE_ID });
    // The category badge renders this label, so the standalone flag badge must
    // be hidden -> the row shows the strike-list status exactly once.
    expect(component.documentTypeLabel(d)).toBe(STRIKE_LIST_LABEL);
    expect(component.showStrikeListFlagBadge(d)).toBe(false);
  });

  it('shows the flag badge when the document is flagged under a different category', () => {
    const d = doc({ isPanelStrikeList: true, appointmentDocumentTypeId: 'med-report-id' });
    expect(component.showStrikeListFlagBadge(d)).toBe(true);
  });

  it('shows the flag badge when the document is flagged with no category', () => {
    const d = doc({ isPanelStrikeList: true });
    expect(component.showStrikeListFlagBadge(d)).toBe(true);
  });

  it('shows no flag badge when the document is not a strike list', () => {
    const d = doc({ isPanelStrikeList: false, appointmentDocumentTypeId: 'med-report-id' });
    expect(component.showStrikeListFlagBadge(d)).toBe(false);
  });
});

/**
 * #612: after a FAILED upload the document name kept the PREVIOUS file's base name.
 *
 * <p>The failure handler is not the bug. It deliberately leaves the form populated so the
 * user can retry, which is right. The bug was in the derive step that runs when they then
 * choose a different file: it only filled the name box when the box was EMPTY, and after a
 * failed attempt the box is not empty. So the new file went up under the old file's name,
 * which on a medical-legal document is a mislabelled record rather than a cosmetic slip.</p>
 *
 * <p>The reason it cannot simply always overwrite is that the box is also where the user
 * types a name of their own. Emptiness cannot tell "we derived this" from "they typed this",
 * so the component now tracks which it is.</p>
 */
describe('AppointmentDocumentsComponent document name derivation (#612)', () => {
  let component: AppointmentDocumentsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppointmentDocumentsComponent],
      providers: [
        { provide: AppointmentDocumentService, useValue: {} },
        { provide: ToasterService, useValue: {} },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => false } },
        { provide: RestService, useValue: { request: () => ({ subscribe: () => undefined }) } },
        { provide: AppointmentDocumentUrls, useValue: {} },
        { provide: HttpClient, useValue: {} },
      ],
    }).compileComponents();

    component = TestBed.createComponent(AppointmentDocumentsComponent).componentInstance;
  });

  const file = (name: string): File => new File(['x'], name, { type: 'application/pdf' });

  /** Picking a file through the input, the way the template does. */
  function choose(name: string): void {
    component.onFileSelected({
      target: { files: [file(name)] },
    } as unknown as Event);
  }

  it('derives the name from the first file, without its extension', () => {
    choose('discharge-summary.pdf');
    expect(component.documentName).toBe('discharge-summary');
  });

  it('replaces a derived name when a different file is chosen', () => {
    // The #612 regression. Nothing here fails an upload, because the failure only matters
    // for leaving the box populated -- which choosing a first file does just as well.
    choose('wrong-file.pdf');
    expect(component.documentName).toBe('wrong-file');

    choose('right-file.pdf');
    expect(component.documentName)
      .withContext('the second file must not upload under the first file name')
      .toBe('right-file');
  });

  it('keeps a name the user typed, however many files they then choose', () => {
    component.onDocumentNameInput('Radiology report 2026-Q1');
    choose('IMG_4471.pdf');
    choose('IMG_4472.pdf');
    expect(component.documentName).toBe('Radiology report 2026-Q1');
  });

  it('resumes deriving once the user clears the box', () => {
    component.onDocumentNameInput('Mine');
    component.onDocumentNameInput('');
    choose('scan.pdf');
    expect(component.documentName).toBe('scan');
  });

  it('applies the same rule to a dropped file as to a picked one', () => {
    choose('first.pdf');
    component.onFilesDropped([file('second.pdf')]);
    expect(component.documentName).toBe('second');
  });

  it('does not let a dropped file overwrite a typed name either', () => {
    component.onDocumentNameInput('Operative note');
    component.onFilesDropped([file('second.pdf')]);
    expect(component.documentName).toBe('Operative note');
  });

  it('strips only the final extension', () => {
    choose('report.2026.06.01.pdf');
    expect(component.documentName).toBe('report.2026.06.01');
  });
});

/**
 * The other half of #612: a SUCCESSFUL upload clears the name box, and the derived-name flag
 * has to be cleared with it. If the flag were left set, the box would be empty but still
 * marked as "ours" -- harmless today, because an empty box is derivable anyway, but it makes
 * the flag and the field it describes disagree, which is how this class of bug starts.
 *
 * Separated from the block above because it needs a RestService that actually completes and
 * an AppointmentDocumentService for the post-upload refresh.
 */
describe('AppointmentDocumentsComponent name reset after a successful upload (#612)', () => {
  let component: AppointmentDocumentsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppointmentDocumentsComponent],
      providers: [
        {
          provide: AppointmentDocumentService,
          useValue: {
            getList: () => of([]),
            getMissingRequiredDocuments: () => of(null),
          },
        },
        { provide: ToasterService, useValue: { success: () => undefined } },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => false } },
        // Completes, so upload() runs its success branch rather than hanging.
        { provide: RestService, useValue: { request: () => of({}) } },
        { provide: AppointmentDocumentUrls, useValue: {} },
        { provide: HttpClient, useValue: {} },
      ],
    }).compileComponents();

    component = TestBed.createComponent(AppointmentDocumentsComponent).componentInstance;
    component.appointmentId = 'appt-1';
  });

  it('derives afresh for the next file once an upload has succeeded', () => {
    component.onFileSelected({
      target: { files: [new File(['x'], 'first.pdf', { type: 'application/pdf' })] },
    } as unknown as Event);
    expect(component.documentName).toBe('first');

    component.upload();
    expect(component.documentName).withContext('success clears the box').toBe('');

    component.onFileSelected({
      target: { files: [new File(['x'], 'second.pdf', { type: 'application/pdf' })] },
    } as unknown as Event);
    expect(component.documentName).toBe('second');
  });
});
