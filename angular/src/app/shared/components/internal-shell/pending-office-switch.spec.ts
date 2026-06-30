import {
  clearPendingOfficeSwitch,
  readPendingOfficeSwitch,
  storePendingOfficeSwitch,
} from './pending-office-switch';

const KEY = 'gesco.pendingOfficeSwitch';

describe('pending-office-switch helper', () => {
  afterEach(() => {
    clearPendingOfficeSwitch();
  });

  it('returns null when nothing is stored', () => {
    expect(readPendingOfficeSwitch()).toBeNull();
  });

  it('round-trips a stored target', () => {
    storePendingOfficeSwitch({ officeId: 'office-b', userName: 'admin' });
    expect(readPendingOfficeSwitch()).toEqual({ officeId: 'office-b', userName: 'admin' });
  });

  it('preserves an empty username (the intake shadow-user target)', () => {
    storePendingOfficeSwitch({ officeId: 'office-b', userName: '' });
    expect(readPendingOfficeSwitch()).toEqual({ officeId: 'office-b', userName: '' });
  });

  it('clear removes the record', () => {
    storePendingOfficeSwitch({ officeId: 'office-b', userName: 'admin' });
    clearPendingOfficeSwitch();
    expect(readPendingOfficeSwitch()).toBeNull();
  });

  it('returns null for a malformed (non-JSON) record', () => {
    sessionStorage.setItem(KEY, '{not valid json');
    expect(readPendingOfficeSwitch()).toBeNull();
  });

  it('returns null for a record missing required fields', () => {
    sessionStorage.setItem(KEY, JSON.stringify({ officeId: 'office-b' }));
    expect(readPendingOfficeSwitch()).toBeNull();
  });
});
