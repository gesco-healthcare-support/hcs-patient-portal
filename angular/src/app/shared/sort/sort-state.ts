/**
 * QA #15 item 6 (2026-07-07) -- shared, framework-free sort helpers used by every
 * bespoke tenant-scope table (appointments, reports, change-request inbox, admin
 * audit rail). Pure functions so they unit-test without TestBed; each table holds
 * its own `signal<SortModel>` and calls these.
 *
 * The header-click cycle matches ABP's ngx-datatable scaffolds so the whole app
 * behaves the same: unsorted -> ascending -> descending -> unsorted.
 */

export type SortDir = 'asc' | 'desc';

/** The active sort: which column key (null = unsorted) and its direction. */
export interface SortModel {
  key: string | null;
  dir: SortDir;
}

/** A cell value the client-side comparator can order. Blank = null/undefined/''. */
export type SortValue = string | number | null | undefined;

/**
 * Advance the 3-state cycle for a clicked column: a fresh column starts
 * ascending; the active column goes asc -> desc, then desc -> unsorted (clear).
 * Switching columns always restarts at ascending.
 */
export function nextSort(current: SortModel, key: string): SortModel {
  if (current.key !== key) {
    return { key, dir: 'asc' };
  }
  if (current.dir === 'asc') {
    return { key, dir: 'desc' };
  }
  return { key: null, dir: 'asc' };
}

/**
 * The ABP `sorting` clause for a server-paged endpoint (e.g. "Patient.LastName
 * asc"). Empty when unsorted so the server applies its own default order.
 */
export function sortingClause(model: SortModel): string {
  return model.key ? `${model.key} ${model.dir}` : '';
}

/** The `aria-sort` value for a header cell keyed by `key`. */
export function ariaSortFor(model: SortModel, key: string): 'ascending' | 'descending' | 'none' {
  if (model.key !== key) {
    return 'none';
  }
  return model.dir === 'asc' ? 'ascending' : 'descending';
}

/** True when a value should sort to the end regardless of direction. */
function isBlank(value: SortValue): boolean {
  return value === null || value === undefined || value === '';
}

/**
 * Ascending comparison of two NON-blank primitives: numbers compare numerically,
 * everything else compares as a case-insensitive, digit-aware string so "item 2"
 * precedes "item 10".
 */
export function compareValues(a: string | number, b: string | number): number {
  if (typeof a === 'number' && typeof b === 'number') {
    return a - b;
  }
  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: 'base' });
}

/**
 * Build an Array.sort comparator for a client-side table. `accessor` maps a row +
 * column key to the primitive being ordered (dates should be pre-normalized to a
 * number/ISO string by the caller). Blank values always sort last, in either
 * direction; an unsorted model yields a stable no-op comparator.
 */
export function makeComparator<T>(
  model: SortModel,
  accessor: (row: T, key: string) => SortValue,
): (a: T, b: T) => number {
  const { key, dir } = model;
  if (!key) {
    return () => 0;
  }
  const factor = dir === 'asc' ? 1 : -1;
  return (a, b) => {
    const av = accessor(a, key);
    const bv = accessor(b, key);
    const aBlank = isBlank(av);
    const bBlank = isBlank(bv);
    if (aBlank && bBlank) {
      return 0;
    }
    if (aBlank) {
      return 1;
    }
    if (bBlank) {
      return -1;
    }
    return factor * compareValues(av as string | number, bv as string | number);
  };
}
