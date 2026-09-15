import { ConfigStateService } from '@abp/ng.core';
import { isHostScope, resolveInternalRoleKey } from './internal-user-roles';

describe('resolveInternalRoleKey', () => {
  it('maps the seeded internal role names to nav keys', () => {
    expect(resolveInternalRoleKey(['IT Admin'])).toBe('itadmin');
    expect(resolveInternalRoleKey(['Staff Supervisor'])).toBe('supervisor');
    expect(resolveInternalRoleKey(['Intake Staff'])).toBe('intake');
    expect(resolveInternalRoleKey(['admin'])).toBe('admin');
  });

  it('is case-insensitive and trims', () => {
    expect(resolveInternalRoleKey(['  staff supervisor  '])).toBe('supervisor');
    expect(resolveInternalRoleKey(['INTAKE STAFF'])).toBe('intake');
  });

  it('lets the superuser win over a staff role', () => {
    expect(resolveInternalRoleKey(['Intake Staff', 'admin'])).toBe('admin');
  });

  it('returns null for external-only or empty roles', () => {
    expect(resolveInternalRoleKey(['Patient'])).toBeNull();
    expect(resolveInternalRoleKey(['Applicant Attorney', 'Defense Attorney'])).toBeNull();
    expect(resolveInternalRoleKey([])).toBeNull();
    expect(resolveInternalRoleKey(null)).toBeNull();
  });
});

describe('isHostScope', () => {
  function configWith(tenant: unknown): ConfigStateService {
    return {
      getOne: (key: string) => (key === 'currentTenant' ? tenant : null),
    } as ConfigStateService;
  }

  it('is true at host (no current tenant)', () => {
    expect(isHostScope(configWith(null))).toBe(true);
    expect(isHostScope(configWith({ id: null }))).toBe(true);
  });

  it('is false inside a tenant', () => {
    expect(
      isHostScope(configWith({ id: 'b854b96f-e0a0-ada6-adb3-3a21cfccd950', name: 'Falkinstein' })),
    ).toBe(false);
  });
});
