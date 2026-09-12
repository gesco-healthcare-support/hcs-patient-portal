import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { DateAdapter } from '@abp/ng.theme.shared';
import { NgbDateAdapter } from '@ng-bootstrap/ng-bootstrap';
import { of } from 'rxjs';

import {
  AppointmentAddClaimInformationComponent,
  AppointmentInjuryDraft,
} from './appointment-add-claim-information.component';

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
   * Fills the always-required injury fields (Date of Injury + Claim Number);
   * each test then fills the body-part row(s) so the modal form is valid and
   * saveInjuryModal commits the draft. CI2 (2026-06-05) removed the per-injury
   * Insurance + Claim Examiner sub-sections (they moved to the appointment
   * level), so they are no longer part of the scaffold.
   */
  function fillRequiredScaffold(claimNumber: string): void {
    component.openAddInjuryModal();
    component.injuryForm.get('injuryDateOfInjury')!.setValue('2025-03-15');
    component.injuryForm.get('injuryClaimNumber')!.setValue(claimNumber);
    component.injuryForm.get('injuryWcabAdj')!.setValue('ADJ-CI3'); // CI3: ADJ# now required
  }

  it('seeds exactly one required body-part row when the modal opens', () => {
    component.openAddInjuryModal();
    expect(component.bodyPartsArray).toHaveSize(1);
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
    expect(component.injuryDrafts).toHaveSize(1);
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

    expect(component.injuryDrafts).toHaveSize(1);
    expect(component.injuryDrafts[0].bodyParts).toEqual(['Shoulder']);
    expect(component.injuryDrafts[0].bodyPartsSummary).toBe('Shoulder');
  });

  it('removeBodyPart keeps at least one row', () => {
    component.openAddInjuryModal();
    expect(component.bodyPartsArray).toHaveSize(1);
    component.removeBodyPart(0);
    expect(component.bodyPartsArray).toHaveSize(1);
  });
});

/**
 * Settles BUG-040 / #555: "cumulative trauma flag and ToDateOfInjury not persisting".
 *
 * <p>The finding was promoted from an observation with the driver named as the most likely
 * cause, and asked for a human live repro to decide. It offered a fix shape -- loosen
 * `v.injuryCumulative === true` to `Boolean(...)` -- on the theory that the radio might put
 * the STRING `'true'` into the control, in which case the strict comparison would drop the
 * flag silently.</p>
 *
 * <p>This suite is that repro, driven through the real
 * `RadioControlValueAccessor` rather than by setting the control directly, which is the
 * only way to tell the two theories apart. Setting `injuryCumulative` by hand would
 * assume the answer.</p>
 *
 * <p>Both symptoms in the finding share one cause, which is why they were seen together:
 * the To Date input is inside `@if (injuryForm.get('injuryCumulative')?.value)`, so if the
 * flag never became truthy the input never rendered and the date could never be filled.</p>
 */
describe('AppointmentAddClaimInformationComponent cumulative trauma (BUG-040 / #555)', () => {
  function create(drafts: AppointmentInjuryDraft[]) {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: RestService,
          // Lookups only populate two dropdowns; neither gates the save path.
          useValue: { request: () => of({ items: [], totalCount: 0 }) },
        },
        // The section does not provide its own date adapter -- it inherits the host's, and
        // both booking surfaces provide ABP's `NgbDateAdapter<string>` (see the reasoning in
        // `availability-date-adapter.ts`). Without it the default struct adapter cannot read
        // a `yyyy-MM-dd` model and the datepicker writes the date control back to null, which
        // is an artefact of the harness rather than of the component.
        { provide: NgbDateAdapter, useClass: DateAdapter },
      ],
    });
    const fixture = TestBed.createComponent(AppointmentAddClaimInformationComponent);
    fixture.componentRef.setInput('injuryDrafts', drafts);
    fixture.detectChanges();
    return fixture;
  }

  /** Clicks a radio the way a user does, so Angular's value accessor is what runs. */
  function clickCumulativeYes(host: HTMLElement): void {
    const radios = Array.from(
      host.querySelectorAll<HTMLInputElement>('input[type="radio"][name="injuryCumulative"]'),
    );
    expect(radios.length).withContext('the Yes/No radio pair should render').toBe(2);
    radios[0].click();
  }

  /**
   * Opens the modal by clicking the real Add button.
   *
   * <p>Calling `openAddInjuryModal()` from the test sets `isInjuryModalOpen` but leaves the
   * modal unrendered: the component is OnPush, so a method invoked directly never marks the
   * view dirty. Going through the DOM event is both the fix and the more faithful drive --
   * the whole question here is what real interaction produces.</p>
   */
  function openModal(fixture: ReturnType<typeof create>): void {
    const add = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '.card-header button',
    );
    expect(add).withContext('the Add button should render').not.toBeNull();
    add!.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.isInjuryModalOpen)
      .withContext('clicking Add should open the injury modal')
      .toBeTrue();
  }

  function setControl(fixture: ReturnType<typeof create>, name: string, value: unknown): void {
    fixture.componentInstance.injuryForm.get(name)!.setValue(value);
  }

  afterEach(() => TestBed.resetTestingModule());

  it('puts a real boolean in the control when the Yes radio is clicked', () => {
    // The heart of it. If this were the string 'true', `=== true` in serializeInjuryForm
    // would silently drop the flag and the finding's suggested fix would be right.
    const fixture = create([]);
    openModal(fixture);

    clickCumulativeYes(fixture.nativeElement as HTMLElement);
    fixture.detectChanges();

    const value = fixture.componentInstance.injuryForm.get('injuryCumulative')!.value;
    expect(value).toBe(true);
    expect(typeof value).toBe('boolean');
  });

  it('reveals the To Date input once cumulative is selected', () => {
    const fixture = create([]);
    openModal(fixture);

    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).not.toContain('To Date');

    clickCumulativeYes(host);
    fixture.detectChanges();

    expect(host.textContent).toContain('To Date');
  });

  it('persists the flag and the To date through to the draft', () => {
    const drafts: AppointmentInjuryDraft[] = [];
    const fixture = create(drafts);
    openModal(fixture);

    clickCumulativeYes(fixture.nativeElement as HTMLElement);
    fixture.detectChanges();

    setControl(fixture, 'injuryDateOfInjury', '2025-06-01');
    setControl(fixture, 'injuryToDateOfInjury', '2025-12-10');
    setControl(fixture, 'injuryClaimNumber', 'CLM-0001');
    setControl(fixture, 'injuryWcabAdj', 'ADJ-0001');
    fixture.componentInstance.injuryForm.get('injuryBodyParts')!.setValue(['Lower back']);

    fixture.componentInstance.saveInjuryModal();

    expect(fixture.componentInstance.injuryModalError).toBeNull();
    expect(drafts).toHaveSize(1);
    // The exact two columns the finding measured as 0 and NULL in the database.
    expect(drafts[0].isCumulativeInjury).toBeTrue();
    expect(drafts[0].toDateOfInjury).toBe('2025-12-10');
    expect(drafts[0].dateOfInjury).toBe('2025-06-01');
  });

  it('keeps the flag false and the To date null for a non-cumulative injury', () => {
    // The control case: the same save path must NOT set the flag when nobody clicked Yes,
    // otherwise the test above would pass for the wrong reason.
    const drafts: AppointmentInjuryDraft[] = [];
    const fixture = create(drafts);
    openModal(fixture);

    setControl(fixture, 'injuryDateOfInjury', '2025-06-01');
    setControl(fixture, 'injuryClaimNumber', 'CLM-0002');
    setControl(fixture, 'injuryWcabAdj', 'ADJ-0002');
    fixture.componentInstance.injuryForm.get('injuryBodyParts')!.setValue(['Left shoulder']);

    fixture.componentInstance.saveInjuryModal();

    expect(drafts).toHaveSize(1);
    expect(drafts[0].isCumulativeInjury).toBeFalse();
    expect(drafts[0].toDateOfInjury).toBeNull();
  });

  it('still refuses an inverted cumulative range', () => {
    // checkInjuryDates reads the same control through the same `=== true`, so it is part of
    // the same question: the range rules only run when the flag is genuinely boolean true.
    const drafts: AppointmentInjuryDraft[] = [];
    const fixture = create(drafts);
    openModal(fixture);

    clickCumulativeYes(fixture.nativeElement as HTMLElement);
    fixture.detectChanges();

    setControl(fixture, 'injuryDateOfInjury', '2025-12-10');
    setControl(fixture, 'injuryToDateOfInjury', '2025-06-01');
    setControl(fixture, 'injuryClaimNumber', 'CLM-0003');
    setControl(fixture, 'injuryWcabAdj', 'ADJ-0003');
    fixture.componentInstance.injuryForm.get('injuryBodyParts')!.setValue(['Neck']);

    fixture.componentInstance.saveInjuryModal();

    expect(drafts).toHaveSize(0);
    expect(fixture.componentInstance.injuryModalError).toContain('earlier than');
  });
});

/**
 * Sweep #632 replaced this method's `JSON.parse(JSON.stringify(draft))` with
 * `structuredClone` (typescript:S7784). The swap is exact for
 * AppointmentInjuryDraft -- every field is boolean, string, string | null or
 * string[], and all four producers coalesce with `??`, so no Date, function or
 * undefined can reach it -- but the method had no coverage at all, so these
 * specs pin the edit-load contract that the clone sits inside.
 *
 * Note on what they do and do not prove: the draft is isolated from the form
 * by TWO independent mechanisms -- the clone here, and buildInjuryForm mapping
 * every field into fresh controls. The isolation spec below pins the
 * observable contract; it would still pass if either mechanism alone were
 * removed, and that is deliberate -- the contract is what callers rely on.
 */
describe('AppointmentAddClaimInformationComponent openEditInjuryModal', () => {
  let component: AppointmentAddClaimInformationComponent;

  function draft(overrides: Partial<AppointmentInjuryDraft> = {}): AppointmentInjuryDraft {
    return {
      isCumulativeInjury: true,
      dateOfInjury: '2025-06-01',
      toDateOfInjury: '2025-12-10',
      claimNumber: 'CLM-0042',
      wcabOfficeId: null,
      wcabAdj: 'ADJ-0042',
      bodyParts: ['Lower back', 'Right knee'],
      bodyPartsSummary: 'Lower back, Right knee',
      ...overrides,
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AppointmentAddClaimInformationComponent],
      providers: [{ provide: RestService, useValue: { request: () => of({ items: [] }) } }],
    });
    component = TestBed.createComponent(AppointmentAddClaimInformationComponent).componentInstance;
    component.injuryDrafts = [];
  });

  it('loads the selected draft into the modal form', () => {
    component.injuryDrafts = [draft()];

    component.openEditInjuryModal(0);

    expect(component.isInjuryModalOpen).toBeTrue();
    expect(component.injuryEditingIndex).toBe(0);
    expect(component.injuryModalError).toBeNull();
    expect(component.injuryForm.get('injuryClaimNumber')!.value).toBe('CLM-0042');
    expect(component.injuryForm.get('injuryToDateOfInjury')!.value).toBe('2025-12-10');
    expect(component.injuryForm.get('injuryCumulative')!.value).toBeTrue();
    // One control per stored body part, rather than the single seeded row.
    expect(component.bodyPartsArray).toHaveSize(2);
    expect(component.bodyPartsArray.value).toEqual(['Lower back', 'Right knee']);
  });

  it('is a no-op for an index that holds no draft', () => {
    component.injuryDrafts = [draft()];

    component.openEditInjuryModal(7);

    expect(component.isInjuryModalOpen).toBeFalse();
    expect(component.injuryEditingIndex).toBe(-1);
  });

  it('leaves the stored draft untouched while the form is edited', () => {
    const stored = draft();
    component.injuryDrafts = [stored];

    component.openEditInjuryModal(0);
    component.injuryForm.get('injuryClaimNumber')!.setValue('CHANGED');
    component.bodyPartsArray.at(0).setValue('Neck');

    expect(stored.claimNumber).toBe('CLM-0042');
    expect(stored.bodyParts).toEqual(['Lower back', 'Right knee']);
  });

  it('replaces the edited draft in place on save rather than appending', () => {
    const drafts = [draft(), draft({ claimNumber: 'CLM-0099' })];
    component.injuryDrafts = drafts;

    component.openEditInjuryModal(0);
    component.injuryForm.get('injuryClaimNumber')!.setValue('CLM-EDITED');
    component.saveInjuryModal();

    expect(component.injuryModalError).toBeNull();
    expect(drafts).toHaveSize(2);
    expect(drafts[0].claimNumber).toBe('CLM-EDITED');
    expect(drafts[1].claimNumber).toBe('CLM-0099');
  });
});
