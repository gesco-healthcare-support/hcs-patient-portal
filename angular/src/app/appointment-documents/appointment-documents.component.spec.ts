import { TestBed } from '@angular/core/testing';
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
