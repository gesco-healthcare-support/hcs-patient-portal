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
    // MO4 (2026-06-30): System Parameters is supervisor-only now.
    expect(ids).not.toContain('settings');
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
    // Item 1 (2026-06-30): the 'SaaS' group merged into 'Practice Management'.
    expect(sects).toContain('Practice Management');
  });

  it('gives a host Staff Supervisor the platform nav, and the tenant nav once switched into an office', () => {
    // Phase D (2026-06-25): Staff Supervisor is a HOST operator -> at host scope
    // it gets the platform nav (office switch + intake assignments), NOT the
    // tenant nav. Once it impersonates into an office (hostScope=false), it falls
    // back to IN_NAV (the office admin's tenant nav).
    const hostSects = resolveNavGroups('supervisor', true).map((g) => g.sect);
    expect(hostSects).toContain('Practice Management');
    expect(hostSects).not.toContain('Workspace');
    const tenantSects = resolveNavGroups('supervisor', false).map((g) => g.sect);
    expect(tenantSects).toContain('Workspace');
    expect(tenantSects).not.toContain('Platform');
  });

  it('gives a host Intake operator the office switcher, and the tenant nav once switched in', () => {
    // Phase D: Intake is a HOST operator -> at host scope it sees only the
    // Offices group (its My Offices switcher); once impersonating its per-office
    // shadow user (hostScope=false) it gets the tenant operational nav.
    const hostSects = resolveNavGroups('intake', true).map((g) => g.sect);
    expect(hostSects).toContain('Practice Management');
    expect(hostSects).not.toContain('Workspace');
    const tenantSects = resolveNavGroups('intake', false).map((g) => g.sect);
    expect(tenantSects).toContain('Workspace');
    expect(tenantSects).not.toContain('Platform');
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
