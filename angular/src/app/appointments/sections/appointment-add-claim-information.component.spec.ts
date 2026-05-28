import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { of } from 'rxjs';
import { AppointmentAddClaimInformationComponent } from './appointment-add-claim-information.component';

/**
 * OBS-41 (2026-05-27) -- structured body parts. The claim modal captures a
 * repeatable FormArray of per-part descriptions; on save it serializes into
 * the AppointmentInjuryDraft shape with a derived BodyPartsSummary
 * (comma-join, trimmed, empty rows dropped) so existing readers keep working.
 * Complements the live E2E that confirmed the AppAppointmentBodyParts rows.
 */
describe('AppointmentAddClaimInformationComponent body parts (OBS-41)', () => {
  let component: AppointmentAddClaimInformationComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AppointmentAddClaimInformationComponent],
      providers: [
        // openAddInjuryModal -> loadInjuryLookups issues lookup GETs we don't
        // exercise here; a stub returning an empty paged result is enough.
        { provide: RestService, useValue: { request: () => of({ items: [] }) } },
      ],
    });
    const fixture = TestBed.createComponent(AppointmentAddClaimInformationComponent);
    component = fixture.componentInstance;
    component.injuryDrafts = [];
  });

  /**
   * Fills the always-required claim fields + disables the optional Insurance /
   * Claim Examiner sub-sections so the modal form is valid and saveInjuryModal
   * commits the draft.
   */
  function fillRequiredScaffold(claimNumber: string): void {
    component.openAddInjuryModal();
    component.injuryForm.get('injuryDateOfInjury')!.setValue('2025-03-15');
    component.injuryForm.get('injuryClaimNumber')!.setValue(claimNumber);
    component.injuryForm.get('injuryInsuranceEnabled')!.setValue(false);
    component.injuryForm.get('injuryClaimExaminerEnabled')!.setValue(false);
  }

  it('seeds exactly one required body-part row when the modal opens', () => {
    component.openAddInjuryModal();
    expect(component.bodyPartsArray.length).toBe(1);
    expect(component.bodyPartsArray.at(0).hasError('required')).toBe(true);
  });

  it('derives BodyPartsSummary as a comma-join of the structured rows on save', () => {
    fillRequiredScaffold('CLM-1');
    component.bodyPartsArray.at(0).setValue('Lower back');
    component.addBodyPart();
    component.bodyPartsArray.at(1).setValue('Right knee');
    component.addBodyPart();
    component.bodyPartsArray.at(2).setValue('Left wrist');

    component.saveInjuryModal();

    expect(component.injuryModalError).toBeNull();
    expect(component.injuryDrafts.length).toBe(1);
    expect(component.injuryDrafts[0].bodyParts).toEqual(['Lower back', 'Right knee', 'Left wrist']);
    expect(component.injuryDrafts[0].bodyPartsSummary).toBe('Lower back, Right knee, Left wrist');
  });

  it('trims each row and drops whitespace-only rows from the derived summary', () => {
    fillRequiredScaffold('CLM-2');
    component.bodyPartsArray.at(0).setValue('  Shoulder  ');
    component.addBodyPart();
    // Whitespace-only passes Validators.required (non-empty string) but is
    // trimmed away when the summary is derived.
    component.bodyPartsArray.at(1).setValue('   ');

    component.saveInjuryModal();

    expect(component.injuryDrafts.length).toBe(1);
    expect(component.injuryDrafts[0].bodyParts).toEqual(['Shoulder']);
    expect(component.injuryDrafts[0].bodyPartsSummary).toBe('Shoulder');
  });

  it('removeBodyPart keeps at least one row', () => {
    component.openAddInjuryModal();
    expect(component.bodyPartsArray.length).toBe(1);
    component.removeBodyPart(0);
    expect(component.bodyPartsArray.length).toBe(1);
  });
});
