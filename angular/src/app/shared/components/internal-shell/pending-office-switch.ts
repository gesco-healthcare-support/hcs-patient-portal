/**
 * F Half 2 (office -> office single-click): the in-office switcher cannot hop
 * straight to another office because ABP's impersonation grant forbids nested
 * impersonation. Instead the shell de-impersonates to host and then re-impersonates
 * the target tenant -- two stock operations separated by a full-page reload. This
 * helper persists the intended target across that reload via sessionStorage (tab-
 * scoped, cleared on tab close), so the resume step can complete the second hop.
 */
const PENDING_OFFICE_SWITCH_KEY = 'gesco.pendingOfficeSwitch';

export interface PendingOfficeSwitch {
  /** Target office (tenant) id to impersonate after returning to host. */
  officeId: string;
  /**
   * Username passed to the impersonation grant: '' for an Intake operator (the
   * custom grant forces their own limited shadow user), 'admin' for a
   * supervisor / IT Admin (lands as the office admin).
   */
  userName: string;
}

/** Persist the target office to resume after the de-impersonation reload. */
export function storePendingOfficeSwitch(pending: PendingOfficeSwitch): void {
  try {
    sessionStorage.setItem(PENDING_OFFICE_SWITCH_KEY, JSON.stringify(pending));
  } catch {
    // sessionStorage can be unavailable (private mode / quota); the switch simply
    // will not auto-resume -- the operator lands at host and can re-pick. No throw.
  }
}

/** Read the pending target, or null if none / unreadable. */
export function readPendingOfficeSwitch(): PendingOfficeSwitch | null {
  try {
    const raw = sessionStorage.getItem(PENDING_OFFICE_SWITCH_KEY);
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw) as Partial<PendingOfficeSwitch> | null;
    if (parsed && typeof parsed.officeId === 'string' && typeof parsed.userName === 'string') {
      return { officeId: parsed.officeId, userName: parsed.userName };
    }
    return null;
  } catch {
    return null;
  }
}

/** Remove the pending target. Call BEFORE resuming, to prevent re-entrancy loops. */
export function clearPendingOfficeSwitch(): void {
  try {
    sessionStorage.removeItem(PENDING_OFFICE_SWITCH_KEY);
  } catch {
    // ignore -- a stale record at worst causes one no-op resume attempt.
  }
}
