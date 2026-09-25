import { SortHeaderComponent } from './sort-header.component';
import type { SortModel } from './sort-state';

/**
 * A sortable column header. A click raises the next sort for its column: ascending, then
 * descending, then cleared. Another column's sort is replaced, not extended.
 *
 * <p>It had no spec. It injects nothing, so it is built with `new`.</p>
 */
describe('SortHeaderComponent', () => {
  function clickWith(model: SortModel): SortModel[] {
    const c = new SortHeaderComponent();
    c.sortKey = 'lastName';
    c.model = model;
    const out: SortModel[] = [];
    c.sortChange.subscribe((next) => out.push(next));
    (c as unknown as { toggle(): void }).toggle();
    return out;
  }

  it('starts ascending on an unsorted column', () => {
    expect(clickWith({ key: null, dir: 'asc' })).toEqual([{ key: 'lastName', dir: 'asc' }]);
  });

  it('takes over from another sorted column, ascending', () => {
    expect(clickWith({ key: 'email', dir: 'desc' })).toEqual([{ key: 'lastName', dir: 'asc' }]);
  });

  it('turns ascending into descending', () => {
    expect(clickWith({ key: 'lastName', dir: 'asc' })).toEqual([{ key: 'lastName', dir: 'desc' }]);
  });

  it('clears the sort after descending', () => {
    expect(clickWith({ key: 'lastName', dir: 'desc' })).toEqual([{ key: null, dir: 'asc' }]);
  });
});
