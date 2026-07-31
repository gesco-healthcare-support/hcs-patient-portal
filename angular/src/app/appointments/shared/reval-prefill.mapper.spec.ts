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
