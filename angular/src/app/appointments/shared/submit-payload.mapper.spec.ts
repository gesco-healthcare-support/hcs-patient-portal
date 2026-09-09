import {
  buildPatientCreateInput,
  buildPatientUpdateInput,
  buildSubmitAccessors,
  buildSubmitApplicantAttorney,
  buildSubmitClaimExaminer,
  buildSubmitDefenseAttorney,
  buildSubmitEmployerDetail,
  buildSubmitInjuryDetails,
  buildSubmitPatient,
  buildSubmitPrimaryInsurance,
  hasEmployerDetails,
  SubmitFormRawValue,
  UNASSIGNED_APPOINTMENT_ID,
} from './submit-payload.mapper';

/**
 * Pins the booking submit payload (#603).
 *
 * <p>These projections decide what a booking actually sends -- the patient record, the two
 * attorneys, the employer, the insurer, the claim examiner, every injury and every authorized
 * user. Until they were extracted from `AppointmentAddComponent` none of it had a single
 * test, because reaching it meant standing up a 3,900-line directive with eleven injected
 * services.</p>
 *
 * <p>The guards are the part worth pinning hardest. Each one decides whether a whole party
 * block is sent or silently omitted, and an omitted block is exactly the shape of BUG-045:
 * the Case Tracker stores the party groups as one opaque blob, so a section we never sent and
 * one that was legitimately empty are indistinguishable downstream.</p>
 */
describe('submit-payload.mapper (#603)', () => {
  const raw = (over: Partial<SubmitFormRawValue> = {}): SubmitFormRawValue => ({ ...over });

  describe('employer', () => {
    it('reports no employer details when every field is blank', () => {
      expect(hasEmployerDetails(raw())).toBeFalse();
    });

    it('reports employer details from any single field', () => {
      // Deliberately not just the name: the guard is an OR across seven controls, so a
      // booker who filled only the city still counts as having entered something.
      expect(hasEmployerDetails(raw({ employerCity: 'Ontario' }))).toBeTrue();
      expect(hasEmployerDetails(raw({ employerZipCode: '91761' }))).toBeTrue();
    });

    it('omits the block unless BOTH name and occupation are present', () => {
      // The narrower guard: the server requires both, so a partial block is dropped rather
      // than rejected. Worth pinning because it silently discards what the booker typed.
      expect(buildSubmitEmployerDetail(raw({ employerCity: 'Ontario' }))).toBeUndefined();
      expect(buildSubmitEmployerDetail(raw({ employerName: 'Acme' }))).toBeUndefined();
      expect(buildSubmitEmployerDetail(raw({ employerOccupation: 'Welder' }))).toBeUndefined();
    });

    it('maps every employer field once both required ones are present', () => {
      expect(
        buildSubmitEmployerDetail(
          raw({
            employerName: 'Acme',
            employerOccupation: 'Welder',
            employerPhoneNumber: '9095550101',
            employerStreet: '1 Main St',
            employerCity: 'Ontario',
            employerStateId: 'state-ca',
            employerZipCode: '91761',
          }),
        ),
      ).toEqual({
        appointmentId: UNASSIGNED_APPOINTMENT_ID,
        employerName: 'Acme',
        occupation: 'Welder',
        phoneNumber: '9095550101',
        street: '1 Main St',
        city: 'Ontario',
        stateId: 'state-ca',
        zipCode: '91761',
      });
    });
  });

  describe('attorneys', () => {
    const applicant = (over: Partial<SubmitFormRawValue> = {}) =>
      raw({
        applicantAttorneyEnabled: true,
        applicantAttorneyEmail: 'aa@example.test',
        ...over,
      });

    it('omits the applicant attorney when the section is switched off', () => {
      expect(
        buildSubmitApplicantAttorney(applicant({ applicantAttorneyEnabled: false }), {}),
      ).toBeUndefined();
    });

    it('omits the applicant attorney when no email was entered', () => {
      // Email is the match key the server falls back to, so a block without one is unusable.
      expect(
        buildSubmitApplicantAttorney(applicant({ applicantAttorneyEmail: null }), {}),
      ).toBeUndefined();
    });

    it('sends Guid.Empty for the identity user when none was matched', () => {
      // Not a placeholder for "unknown": it is what makes the server fall through to the
      // email-based lookup. Sending undefined would skip that path.
      const dto = buildSubmitApplicantAttorney(applicant(), {})!;
      expect(dto.identityUserId).toBe(UNASSIGNED_APPOINTMENT_ID);
    });

    it('keeps a matched identity user', () => {
      const dto = buildSubmitApplicantAttorney(
        applicant({ applicantAttorneyIdentityUserId: 'user-1' }),
        {},
      )!;
      expect(dto.identityUserId).toBe('user-1');
    });

    it('carries the master id and concurrency stamp when the booker picked an existing firm', () => {
      const dto = buildSubmitApplicantAttorney(applicant(), {
        id: 'aa-1',
        concurrencyStamp: 'stamp-1',
      })!;
      expect(dto.applicantAttorneyId).toBe('aa-1');
      expect(dto.concurrencyStamp).toBe('stamp-1');
    });

    it('omits the id so the server merges by email when no master was picked', () => {
      // Item 3 (2026-08-17): an id present sends the server down its overwrite branch, which
      // blanks fields left empty on the SHARED master record.
      const dto = buildSubmitApplicantAttorney(applicant(), {})!;
      expect(dto.applicantAttorneyId).toBeUndefined();
      expect(dto.concurrencyStamp).toBeUndefined();
    });

    it('defaults the attorney name fields to empty strings, never undefined', () => {
      const dto = buildSubmitApplicantAttorney(applicant(), {})!;
      expect(dto.firstName).toBe('');
      expect(dto.lastName).toBe('');
    });

    it('applies the same rules to the defense attorney', () => {
      expect(buildSubmitDefenseAttorney(raw({ defenseAttorneyEmail: 'da@example.test' }), {}))
        .withContext('disabled section is omitted')
        .toBeUndefined();

      const dto = buildSubmitDefenseAttorney(
        raw({ defenseAttorneyEnabled: true, defenseAttorneyEmail: 'da@example.test' }),
        { id: 'da-1', concurrencyStamp: 'stamp-2' },
      )!;
      expect(dto.defenseAttorneyId).toBe('da-1');
      expect(dto.identityUserId).toBe(UNASSIGNED_APPOINTMENT_ID);
      expect(dto.email).toBe('da@example.test');
    });
  });

  describe('primary insurance', () => {
    it('is omitted when no company name was entered', () => {
      expect(buildSubmitPrimaryInsurance(raw())).toBeUndefined();
    });

    it('is omitted when the name is only whitespace', () => {
      // The guard trims; a space-only name would otherwise create an unnamed insurer row.
      expect(buildSubmitPrimaryInsurance(raw({ appointmentInsuranceName: '   ' }))).toBeUndefined();
    });

    it('is sent, active, once a name is present', () => {
      const dto = buildSubmitPrimaryInsurance(
        raw({ appointmentInsuranceName: 'Statewide Mutual', appointmentInsuranceZip: '91761' }),
      )!;
      expect(dto.appointmentId).toBe(UNASSIGNED_APPOINTMENT_ID);
      expect(dto.isActive).toBeTrue();
      expect(dto.name).toBe('Statewide Mutual');
      expect(dto.zip).toBe('91761');
    });
  });

  describe('claim examiner', () => {
    it('is always sent and always active, even when empty', () => {
      // Unlike insurance there is no guard here. Pinned so that stays a decision rather than
      // drifting into one -- the appointment always carries a claim-examiner row.
      const dto = buildSubmitClaimExaminer(raw())!;
      expect(dto.appointmentId).toBe(UNASSIGNED_APPOINTMENT_ID);
      expect(dto.isActive).toBeTrue();
    });

    it('maps the examiner fields through', () => {
      const dto = buildSubmitClaimExaminer(
        raw({
          appointmentClaimExaminerName: 'Dana Reyes',
          appointmentClaimExaminerEmail: 'ce@example.test',
          appointmentClaimExaminerFax: '9095550188',
        }),
      )!;
      expect(dto.name).toBe('Dana Reyes');
      expect(dto.email).toBe('ce@example.test');
      expect(dto.fax).toBe('9095550188');
    });
  });

  describe('accessors', () => {
    it('is an empty list when nobody was authorized', () => {
      expect(buildSubmitAccessors([])).toEqual([]);
    });

    it('maps each authorized user, blanking empty names to undefined', () => {
      const dto = buildSubmitAccessors([
        { email: 'a@example.test', firstName: 'Ada', lastName: '', userRole: 3, accessTypeId: 1 },
      ] as never)!;
      expect(dto.length).toBe(1);
      expect(dto[0].appointmentId).toBe(UNASSIGNED_APPOINTMENT_ID);
      expect(dto[0].firstName).toBe('Ada');
      expect(dto[0].lastName).toBeUndefined();
    });
  });

  describe('injuries', () => {
    const draft = (over: Record<string, unknown> = {}) =>
      ({
        isCumulativeInjury: false,
        dateOfInjury: '2025-06-01',
        toDateOfInjury: null,
        claimNumber: 'CLM-1',
        wcabOfficeId: null,
        wcabAdj: 'ADJ-1',
        bodyParts: ['Lower back'],
        bodyPartsSummary: 'Lower back',
        ...over,
      }) as never;

    it('is an empty list when no injury was added', () => {
      expect(buildSubmitInjuryDetails([])).toEqual([]);
    });

    it('carries the cumulative flag and the To date, which BUG-040 measured as lost', () => {
      const dto = buildSubmitInjuryDetails([
        draft({ isCumulativeInjury: true, toDateOfInjury: '2025-12-10' }),
      ])!;
      expect(dto[0].injury!.isCumulativeInjury).toBeTrue();
      expect(dto[0].injury!.toDateOfInjury).toBe('2025-12-10');
    });

    it('trims body parts and drops the blank ones', () => {
      const dto = buildSubmitInjuryDetails([
        draft({ bodyParts: ['  Neck  ', '   ', 'Left wrist'] }),
      ])!;
      expect(dto[0].bodyParts!.map((b) => b.bodyPartDescription)).toEqual(['Neck', 'Left wrist']);
    });

    it('parents every body part on the unassigned id, not the appointment', () => {
      const dto = buildSubmitInjuryDetails([draft()])!;
      expect(dto[0].bodyParts![0].appointmentInjuryDetailId).toBe(UNASSIGNED_APPOINTMENT_ID);
    });

    it('survives a draft with no body parts at all', () => {
      const dto = buildSubmitInjuryDetails([draft({ bodyParts: null })])!;
      expect(dto[0].bodyParts).toEqual([]);
    });
  });

  describe('patient', () => {
    const patientRaw = (over: Partial<SubmitFormRawValue> = {}) =>
      raw({
        firstName: 'Ada',
        lastName: 'Lovelace',
        dateOfBirth: '1990-05-15',
        ...over,
      });

    it('creates a patient when the form carries no id', () => {
      const out = buildSubmitPatient(patientRaw(), undefined);
      expect(out.patientId).toBeUndefined();
      expect(out.patient).toBeDefined();
      expect(out.patientUpdate).toBeUndefined();
    });

    it('does not create one when an existing patient was selected', () => {
      const out = buildSubmitPatient(patientRaw({ patientId: 'p-1' }), undefined);
      expect(out.patientId).toBe('p-1');
      expect(out.patient).toBeUndefined();
    });

    it('updates only when the existing record actually has an id', () => {
      const out = buildSubmitPatient(patientRaw({ patientId: 'p-1' }), { id: 'p-1' } as never);
      expect(out.patientUpdate).toBeDefined();
      expect(buildSubmitPatient(patientRaw(), {} as never).patientUpdate).toBeUndefined();
    });

    it('refuses to create a patient with no date of birth', () => {
      // Loud rather than silent: a patient row without a DOB is not identifiable, and this is
      // medical-legal data. The throw is caught by the submit handler.
      expect(() => buildPatientCreateInput(patientRaw({ dateOfBirth: null }))).toThrowError(
        /Date of birth is required/,
      );
    });

    it('sends a blank patient email as null, not an empty string', () => {
      // task_d5407b22: the DTO's [EmailAddress] rejects "" but allows null.
      expect(buildPatientCreateInput(patientRaw({ email: '   ' })).email).toBeNull();
      expect(buildPatientCreateInput(patientRaw({ email: 'ada@example.test' })).email).toBe(
        'ada@example.test',
      );
    });

    it('routes the Unit # control into apptNumber and never sends address', () => {
      // The control is still NAMED `address`; sending it too would revive the old two-column
      // split. Pinned because the naming invites exactly that mistake.
      const dto = buildPatientCreateInput(patientRaw({ address: '4B' }));
      expect(dto.apptNumber).toBe('4B');
      expect((dto as unknown as Record<string, unknown>)['address']).toBeUndefined();
    });

    it('drops the interpreter vendor unless an interpreter is actually needed', () => {
      expect(
        buildPatientCreateInput(
          patientRaw({ needsInterpreter: false, interpreterVendorName: 'Acme Interpreting' }),
        ).interpreterVendorName,
      ).toBeUndefined();
      expect(
        buildPatientCreateInput(
          patientRaw({ needsInterpreter: true, interpreterVendorName: 'Acme Interpreting' }),
        ).interpreterVendorName,
      ).toBe('Acme Interpreting');
    });

    it('treats the string "true" as needing an interpreter on update', () => {
      // The update path coerces deliberately; the create path does not. Pinned so the
      // asymmetry is visible rather than looking like a typo.
      const existing = { id: 'p-1', concurrencyStamp: 'cs' } as never;
      const dto = buildPatientUpdateInput(
        patientRaw({
          needsInterpreter: 'true' as unknown as boolean,
          interpreterVendorName: 'Acme Interpreting',
        }),
        existing,
      );
      expect(dto!.interpreterVendorName).toBe('Acme Interpreting');
    });

    it('carries the concurrency stamp so a concurrent edit is refused, not clobbered', () => {
      const existing = { id: 'p-1', concurrencyStamp: 'cs-9' } as never;
      expect(buildPatientUpdateInput(patientRaw(), existing)!.concurrencyStamp).toBe('cs-9');
    });

    it('prefers the form identity user over the existing one', () => {
      const existing = { id: 'p-1', identityUserId: 'old', concurrencyStamp: 'cs' } as never;
      expect(
        buildPatientUpdateInput(patientRaw({ identityUserId: 'new' }), existing)!.identityUserId,
      ).toBe('new');
      expect(buildPatientUpdateInput(patientRaw(), existing)!.identityUserId).toBe('old');
    });
  });
});
