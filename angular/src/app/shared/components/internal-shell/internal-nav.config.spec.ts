import {
  InternalNavGroup,
  IN_NAV,
  IN_NAV_HOST,
  filterNavGroups,
  resolveNavGroups,
} from './internal-nav.config';

/** es2018-safe flatten of item ids (the tsconfig lib excludes Array.flatMap). */
function itemIds(groups: InternalNavGroup[]): string[] {
  return groups.reduce<string[]>((acc, g) => acc.concat(g.items.map((it) => it.id)), []);
}

describe('filterNavGroups', () => {
  it('keeps only the items a supervisor can see and drops empty groups', () => {
    const groups = filterNavGroups(IN_NAV, 'supervisor');
    const ids = itemIds(groups);
    expect(ids).toContain('dashboard');
    expect(ids).toContain('change-requests');
    expect(ids).toContain('identity');
    // every returned group has at least one item
    expect(groups.every((g) => g.items.length > 0)).toBe(true);
  });

  it('limits Intake Staff to its reduced item set', () => {
    const ids = itemIds(filterNavGroups(IN_NAV, 'intake'));
    // Intake sees the shared operational items...
    expect(ids).toContain('dashboard');
    expect(ids).toContain('appointments');
    expect(ids).toContain('patients');
    expect(ids).toContain('invite-external');
    // ...but not supervisor-only config / scheduling / admin items.
    expect(ids).not.toContain('change-requests');
    expect(ids).not.toContain('identity');
    expect(ids).not.toContain('locations');
    expect(ids).not.toContain('appt-types');
  });

  it('lets the superuser see every item in the source nav', () => {
    const total = IN_NAV.reduce((n, g) => n + g.items.length, 0);
    const adminCount = filterNavGroups(IN_NAV, 'admin').reduce((n, g) => n + g.items.length, 0);
    expect(adminCount).toBe(total);
  });
});

describe('resolveNavGroups', () => {
  it('gives an IT Admin at host scope the platform nav', () => {
    const sects = resolveNavGroups('itadmin', true).map((g) => g.sect);
    expect(sects).toContain('Platform');
    expect(sects).toContain('SaaS');
  });

  it('gives a Staff Supervisor the tenant operational nav, never the platform nav', () => {
    // Even if host-scoped (hostScope=true), a supervisor stays on IN_NAV.
    const hostSects = resolveNavGroups('supervisor', true).map((g) => g.sect);
    const tenantSects = resolveNavGroups('supervisor', false).map((g) => g.sect);
    expect(hostSects).not.toContain('Platform');
    expect(hostSects).toContain('Workspace');
    expect(tenantSects).toContain('Workspace');
  });

  it('gives Intake Staff the tenant nav and never the platform nav', () => {
    const sects = resolveNavGroups('intake', false).map((g) => g.sect);
    expect(sects).toContain('Workspace');
    expect(sects).not.toContain('Platform');
  });

  it('gives the superuser the full platform nav at host scope', () => {
    const total = IN_NAV_HOST.reduce((n, g) => n + g.items.length, 0);
    const count = resolveNavGroups('admin', true).reduce((n, g) => n + g.items.length, 0);
    expect(count).toBe(total);
  });
});

describe('permission gating (requiredPolicy)', () => {
  it('hides a role-visible item when its requiredPolicy is not granted', () => {
    // A supervisor sees States by role, but the granted-policy check denies it
    // (e.g. an admin revoked CaseEvaluation.States from the role).
    const deny = (p: string) => p !== 'CaseEvaluation.States';
    const ids = itemIds(filterNavGroups(IN_NAV, 'supervisor', deny));
    expect(ids).not.toContain('states');
    // a sibling whose policy is still granted stays visible
    expect(ids).toContain('appt-types');
  });

  it('keeps items that carry no requiredPolicy regardless of the predicate', () => {
    const denyAll = () => false;
    const ids = itemIds(filterNavGroups(IN_NAV, 'supervisor', denyAll));
    // Dashboard carries no ABP policy -> always shown by role.
    expect(ids).toContain('dashboard');
    // ...but every policy-carrying item is filtered out.
    expect(ids).not.toContain('appointments');
    expect(ids).not.toContain('patients');
  });

  it('resolveNavGroups forwards the granted-policy predicate', () => {
    const deny = (p: string) => p !== 'CaseEvaluation.Patients';
    const ids = itemIds(resolveNavGroups('supervisor', false, deny));
    expect(ids).not.toContain('patients');
    expect(ids).toContain('applicant-attorneys');
  });
});
