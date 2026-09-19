import {
  ariaSortFor,
  compareValues,
  makeComparator,
  nextSort,
  sortingClause,
  type SortModel,
} from './sort-state';

const NONE: SortModel = { key: null, dir: 'asc' };

describe('sort-state', () => {
  describe('nextSort (3-state cycle: unsorted -> asc -> desc -> unsorted)', () => {
    it('starts a fresh column ascending', () => {
      expect(nextSort(NONE, 'name')).toEqual({ key: 'name', dir: 'asc' });
    });

    it('advances the active column asc -> desc', () => {
      expect(nextSort({ key: 'name', dir: 'asc' }, 'name')).toEqual({ key: 'name', dir: 'desc' });
    });

    it('clears the active column on the third click (desc -> unsorted)', () => {
      expect(nextSort({ key: 'name', dir: 'desc' }, 'name')).toEqual({ key: null, dir: 'asc' });
    });

    it('resets to ascending when switching to a different column', () => {
      expect(nextSort({ key: 'name', dir: 'desc' }, 'email')).toEqual({ key: 'email', dir: 'asc' });
    });
  });

  describe('sortingClause (ABP `sorting` string)', () => {
    it('joins key and direction', () => {
      expect(sortingClause({ key: 'Patient.LastName', dir: 'asc' })).toBe('Patient.LastName asc');
      expect(sortingClause({ key: 'Appointment.AppointmentDate', dir: 'desc' })).toBe(
        'Appointment.AppointmentDate desc',
      );
    });

    it('is empty when unsorted (server applies its default)', () => {
      expect(sortingClause(NONE)).toBe('');
    });
  });

  describe('ariaSortFor', () => {
    it('reports the active column direction', () => {
      expect(ariaSortFor({ key: 'name', dir: 'asc' }, 'name')).toBe('ascending');
      expect(ariaSortFor({ key: 'name', dir: 'desc' }, 'name')).toBe('descending');
    });

    it('reports none for a column that is not the active sort', () => {
      expect(ariaSortFor({ key: 'name', dir: 'asc' }, 'email')).toBe('none');
      expect(ariaSortFor(NONE, 'name')).toBe('none');
    });
  });

  describe('compareValues (ascending, non-null primitives)', () => {
    it('orders numbers numerically, not lexically', () => {
      expect(compareValues(2, 10)).toBeLessThan(0);
      expect(compareValues(10, 2)).toBeGreaterThan(0);
      expect(compareValues(5, 5)).toBe(0);
    });

    it('orders strings case-insensitively and numeric-aware', () => {
      expect(compareValues('apple', 'Banana')).toBeLessThan(0);
      expect(compareValues('item 2', 'item 10')).toBeLessThan(0);
    });
  });

  describe('makeComparator', () => {
    interface Row {
      name: string | null;
      n: number;
    }
    const accessor = (row: Row, key: string): string | number | null =>
      key === 'name' ? row.name : row.n;

    it('is a no-op (stable) when unsorted', () => {
      const cmp = makeComparator<Row>(NONE, accessor);
      expect(cmp({ name: 'z', n: 1 }, { name: 'a', n: 2 })).toBe(0);
    });

    it('sorts ascending by the active key', () => {
      const cmp = makeComparator<Row>({ key: 'n', dir: 'asc' }, accessor);
      const rows: Row[] = [
        { name: 'b', n: 3 },
        { name: 'a', n: 1 },
        { name: 'c', n: 2 },
      ];
      expect([...rows].sort(cmp).map((r) => r.n)).toEqual([1, 2, 3]);
    });

    it('sorts descending by the active key', () => {
      const cmp = makeComparator<Row>({ key: 'n', dir: 'desc' }, accessor);
      const rows: Row[] = [
        { name: 'b', n: 3 },
        { name: 'a', n: 1 },
        { name: 'c', n: 2 },
      ];
      expect([...rows].sort(cmp).map((r) => r.n)).toEqual([3, 2, 1]);
    });

    it('keeps blank values last in BOTH directions', () => {
      const rows: Row[] = [
        { name: 'b', n: 1 },
        { name: null, n: 2 },
        { name: 'a', n: 3 },
      ];
      const asc = [...rows].sort(makeComparator<Row>({ key: 'name', dir: 'asc' }, accessor));
      expect(asc.map((r) => r.name)).toEqual(['a', 'b', null]);
      const desc = [...rows].sort(makeComparator<Row>({ key: 'name', dir: 'desc' }, accessor));
      expect(desc.map((r) => r.name)).toEqual(['b', 'a', null]);
    });
  });
});
