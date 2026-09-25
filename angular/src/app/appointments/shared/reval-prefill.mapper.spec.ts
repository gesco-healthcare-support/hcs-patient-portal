import { buildRevalPrefill, type RevalPrefillSources } from './reval-prefill.mapper';

/**
 * Issue #3 (2026-07-16): a carried-forward accessor must keep a valid role on reval.
 * The mapper previously defaulted userRole to '' when the accessor was not in the
 * tenant's authorized-user options, which failed role validation on the re-POST. It
 * now falls back to the server-resolved `userRoleName` on the accessor read DTO.
 */
function baseSources(overrides: Partial<RevalPrefillSources> = {}): RevalPrefillSources {
  return {
    appointment: {} as RevalPrefillSources['appointment'],
    employer: null,
    applicantAttorney: null,
    defenseAttorney: null,
    injuries: [],
    accessors: [],
    authorizedUserOptions: [],
    claimExaminer: null,
    primaryInsurance: null,
    ...overrides,
  };
}

function accessorRow(userRoleName?: string): RevalPrefillSources['accessors'][number] {
  return {
    appointmentAccessor: { identityUserId: 'u1', accessTypeId: 23 },
    identityUser: { name: 'Pat', surname: 'Lee', email: 'pat@x.test' },
    userRoleName,
  } as RevalPrefillSources['accessors'][number];
}

describe('buildRevalPrefill -- accessor role fallback (#3)', () => {
  it('falls back to the server-resolved userRoleName when the accessor is not in the options', () => {
    const result = buildRevalPrefill(
      baseSources({ accessors: [accessorRow('Applicant Attorney')], authorizedUserOptions: [] }),
    );
    expect(result.authorizedUsers).toHaveSize(1);
    expect(result.authorizedUsers[0].userRole).toBe('Applicant Attorney');
  });

  it('prefers the options userRole over userRoleName when the accessor is in the options', () => {
    const result = buildRevalPrefill(
      baseSources({
        accessors: [accessorRow('Applicant Attorney')],
        authorizedUserOptions: [
          {
            identityUserId: 'u1',
            firstName: 'Pat',
            lastName: 'Lee',
            email: 'pat@x.test',
            userRole: 'Defense Attorney',
          },
        ],
      }),
    );
    expect(result.authorizedUsers[0].userRole).toBe('Defense Attorney');
  });

  it('yields empty role only when neither options nor userRoleName resolve', () => {
    const result = buildRevalPrefill(baseSources({ accessors: [accessorRow(undefined)] }));
    expect(result.authorizedUsers[0].userRole).toBe('');
  });
});

/**
 * Item 3 (2026-08-17): the prefill must NOT carry attorney entity ids or concurrency
 * stamps.
 *
 * Carrying the id sent the server's upsert down its id-present branch, which writes the
 * submitted values UNCONDITIONALLY -- so a field the booker happened to leave blank was
 * blanked on the shared attorney master that other appointments also point at. Without
 * the id the server resolves the same attorney by email instead, on a branch that merges
 * (`input.X ?? existing.X`) and therefore preserves what the booker did not fill in.
 *
 * Reuse is not lost: email is the authoritative identity for a party (R2-2), and the
 * booking wizard requires an attorney email whenever the attorney section is included.
 */
describe('buildRevalPrefill -- attorney entity identity (item 3)', () => {
  const carriedKeys = [
    'applicantAttorneyId',
    'applicantAttorneyConcurrencyStamp',
    'defenseAttorneyId',
    'defenseAttorneyConcurrencyStamp',
  ];

  /**
   * LOAD-BEARING DECOYS. The attorneys must arrive WITH an entity id and a concurrency stamp,
   * or this test proves nothing: against null attorneys the mapper returns {} for both, and no
   * key could ever appear. It was written that way first and passed with the regression applied.
   * The values are distinctive so the value check below still catches a key that was renamed
   * rather than removed.
   */
  const applicantWithIdentity = {
    applicantAttorneyId: 'ATTY-DECOY-APPLICANT-ID',
    concurrencyStamp: 'ATTY-DECOY-APPLICANT-STAMP',
    identityUserId: 'user-applicant',
    email: 'grace@example.test',
  };
  const defenseWithIdentity = {
    defenseAttorneyId: 'ATTY-DECOY-DEFENSE-ID',
    concurrencyStamp: 'ATTY-DECOY-DEFENSE-STAMP',
    identityUserId: 'user-defense',
    email: 'alan@example.test',
  };
  const decoys = [
    applicantWithIdentity.applicantAttorneyId,
    applicantWithIdentity.concurrencyStamp,
    defenseWithIdentity.defenseAttorneyId,
    defenseWithIdentity.concurrencyStamp,
  ];

  it('returns no attorney entity id or concurrency stamp', () => {
    // The keys would land in formPatch -- the patch applied to the booking form -- so that is
    // the object to inspect. `result` itself only ever has the three payload fields.
    const { formPatch } = buildRevalPrefill(
      baseSources({
        applicantAttorney: applicantWithIdentity,
        defenseAttorney: defenseWithIdentity,
      }),
    );

    // Positive control: both attorneys WERE mapped, so an absence below is a real absence.
    expect(formPatch['applicantAttorneyEmail']).toBe('grace@example.test');
    expect(formPatch['defenseAttorneyEmail']).toBe('alan@example.test');

    for (const key of carriedKeys) {
      expect(Object.prototype.hasOwnProperty.call(formPatch, key)).withContext(key).toBe(false);
    }
    expect(Object.values(formPatch).filter((value) => decoys.includes(value as string))).toEqual(
      [],
    );
  });

  it('returns only the three payload fields', () => {
    // Pins the TOP-LEVEL result shape only. It does not guard against re-adding an attorney
    // entity id -- such a key would land inside formPatch, which this never inspects; the test
    // above owns that guarantee. Patient identity is deliberately absent here and always was --
    // the patient is reused through the component's currentPatientProfile /
    // isPatientAlreadyExist, not through this mapper.
    const result = buildRevalPrefill(baseSources());

    expect(Object.keys(result).sort()).toEqual(['authorizedUsers', 'formPatch', 'injuryDrafts']);
  });
});

/**
 * #296: a re-evaluation copies the prior appointment's parties forward, so the booker edits
 * rather than retypes. Every fixture above passes null for these blocks, so until these the
 * populated branch of each mapper had never run. One test per block, asserting the fields
 * whose names CHANGE on the way into the form, because those are where a mapping goes wrong.
 */
describe('buildRevalPrefill -- copy-forward into a re-evaluation (#296)', () => {
  it('copies the employer block into the employer controls', () => {
    const { formPatch } = buildRevalPrefill(
      baseSources({
        employer: {
          employerName: 'Example Manufacturing',
          occupation: 'Machinist',
          zipCode: '00000',
        } as RevalPrefillSources['employer'],
      }),
    );

    expect(formPatch['employerName']).toBe('Example Manufacturing');
    expect(formPatch['employerOccupation']).toBe('Machinist');
    expect(formPatch['employerZipCode']).toBe('00000');
  });

  it('copies the applicant attorney under the applicantAttorney prefix', () => {
    const { formPatch } = buildRevalPrefill(
      baseSources({
        applicantAttorney: {
          identityUserId: 'user-applicant',
          email: 'grace@example.test',
          firmName: 'Example Law',
        },
      }),
    );

    expect(formPatch['applicantAttorneyIdentityUserId']).toBe('user-applicant');
    expect(formPatch['applicantAttorneyEmail']).toBe('grace@example.test');
    expect(formPatch['applicantAttorneyFirmName']).toBe('Example Law');
  });

  it('copies the defense attorney under the defenseAttorney prefix, and only there', () => {
    const { formPatch } = buildRevalPrefill(
      baseSources({ defenseAttorney: { email: 'alan@example.test', lastName: 'Example' } }),
    );

    expect(formPatch['defenseAttorneyEmail']).toBe('alan@example.test');
    expect(formPatch['defenseAttorneyLastName']).toBe('Example');
    expect(formPatch['applicantAttorneyEmail']).toBeUndefined();
  });

  it('carries each injury forward with its body parts trimmed and blanks dropped', () => {
    const { injuryDrafts } = buildRevalPrefill(
      baseSources({
        injuries: [
          {
            appointmentInjuryDetail: { isCumulativeInjury: true, claimNumber: 'CLM-0001' },
            bodyParts: [
              { bodyPartDescription: '  Left knee ' },
              { bodyPartDescription: '   ' },
              null,
              { bodyPartDescription: 'Lower back' },
            ],
          },
        ] as unknown as RevalPrefillSources['injuries'],
      }),
    );

    expect(injuryDrafts).toHaveSize(1);
    expect(injuryDrafts[0].bodyParts).toEqual(['Left knee', 'Lower back']);
    expect(injuryDrafts[0].bodyPartsSummary).toBe('Left knee, Lower back');
    expect(injuryDrafts[0].claimNumber).toBe('CLM-0001');
    expect(injuryDrafts[0].isCumulativeInjury).toBeTrue();
  });

  it('copies the claim examiner into the appointment-level controls', () => {
    const { formPatch } = buildRevalPrefill(
      baseSources({
        claimExaminer: {
          name: 'Alex Example',
          email: 'alex@example.test',
          zip: '00000',
        } as RevalPrefillSources['claimExaminer'],
      }),
    );

    expect(formPatch['appointmentClaimExaminerName']).toBe('Alex Example');
    expect(formPatch['appointmentClaimExaminerEmail']).toBe('alex@example.test');
    expect(formPatch['appointmentClaimExaminerZip']).toBe('00000');
  });

  it('copies the primary insurance into the appointment-level controls', () => {
    const { formPatch } = buildRevalPrefill(
      baseSources({
        primaryInsurance: {
          name: 'Example Insurance',
          faxNumber: '555-0100',
          zip: '00000',
        } as RevalPrefillSources['primaryInsurance'],
      }),
    );

    expect(formPatch['appointmentInsuranceName']).toBe('Example Insurance');
    expect(formPatch['appointmentInsuranceFaxNumber']).toBe('555-0100');
    expect(formPatch['appointmentInsuranceZip']).toBe('00000');
  });
});
