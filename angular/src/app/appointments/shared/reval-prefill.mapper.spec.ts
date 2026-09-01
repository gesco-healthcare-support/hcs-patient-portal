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
    expect(result.authorizedUsers.length).toBe(1);
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

  it('returns no attorney entity id or concurrency stamp', () => {
    const result = buildRevalPrefill(baseSources());

    for (const key of carriedKeys) {
      expect(Object.prototype.hasOwnProperty.call(result, key)).toBe(false);
    }
  });

  it('returns only the three payload fields', () => {
    // Pins the whole result shape, so re-adding an entity id later is a visible break
    // rather than a silent regression. Patient identity is deliberately absent here and
    // always was -- the patient is reused through the component's currentPatientProfile
    // / isPatientAlreadyExist, not through this mapper.
    const result = buildRevalPrefill(baseSources());

    expect(Object.keys(result).sort()).toEqual(['authorizedUsers', 'formPatch', 'injuryDrafts']);
  });
});
