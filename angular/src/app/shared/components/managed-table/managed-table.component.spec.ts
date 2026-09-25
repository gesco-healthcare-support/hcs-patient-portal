import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Observable, Subject, of } from 'rxjs';
import { ManagedTableComponent } from './managed-table.component';
import {
  ManagedTableCellDirective,
  ManagedTableRowActionsDirective,
} from './managed-table-cell.directive';
import { ManagedTableColumn, ManagedTablePage, ManagedTableQuery } from './managed-table.models';

interface Row {
  id: string;
  name: string;
  count: number;
}

/**
 * Host harness: a column with a projected cell (name), a plain text column
 * (count), and a row-actions slot. Records every query the table issues so the
 * spec can assert the server contract (filter / sorting / skip / take).
 */
@Component({
  standalone: true,
  imports: [ManagedTableComponent, ManagedTableCellDirective, ManagedTableRowActionsDirective],
  template: `
    <app-managed-table
      [dataSource]="dataSource"
      [columns]="columns"
      [pageSize]="pageSize"
      [reload$]="reload$"
      emptyText="Nothing here."
    >
      @if (showCell) {
        <b *managedTableCell="'name'; let row">NAME:{{ row.name }}</b>
      }
      <button class="act" *managedTableRowActions="let row">ACT:{{ row.id }}</button>
    </app-managed-table>
  `,
})
class HostComponent {
  pageSize = 2;
  /** Toggled by one test to add and remove the projected cell template after first render. */
  showCell = true;
  readonly reload$ = new Subject<void>();
  columns: ManagedTableColumn[] = [
    { key: 'name', header: 'Name', sortable: true, sortKey: 'name' },
    { key: 'count', header: 'Count' },
  ];
  readonly queries: ManagedTableQuery[] = [];
  page: ManagedTablePage<Row> = {
    items: [{ id: '1', name: 'Alpha', count: 3 }],
    totalCount: 5,
  };
  dataSource = (query: ManagedTableQuery): Observable<ManagedTablePage<Row>> => {
    this.queries.push({ ...query });
    return of(this.page);
  };
}

describe('ManagedTableComponent (QA item B)', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const last = () => host.queries[host.queries.length - 1];

  it('issues the initial query (empty filter/sort, page 0, page size)', () => {
    expect(host.queries).toHaveSize(1);
    expect(last().search).toBe('');
    expect(last().sorting).toBe('');
    expect(last().skipCount).toBe(0);
    expect(last().maxResultCount).toBe(2);
  });

  it('renders projected cells, plain text columns, and the row-action slot', () => {
    expect(el().textContent).toContain('NAME:Alpha');
    const cellTexts = Array.from(el().querySelectorAll('tbody td')).map((c) =>
      (c.textContent || '').trim(),
    );
    expect(cellTexts).toContain('3'); // plain "count" column rendered via row[key]
    expect(el().querySelector('button.act')?.textContent).toContain('ACT:1');
  });

  it('debounces search, resets to page 0, and passes the term as filter', fakeAsync(() => {
    const input = el().querySelector('input[type="search"]') as HTMLInputElement;
    input.value = 'beta';
    input.dispatchEvent(new Event('input'));
    tick(300);
    fixture.detectChanges();
    expect(last().search).toBe('beta');
    expect(last().skipCount).toBe(0);
  }));

  it('toggles sort direction on header click and builds the sorting clause', () => {
    const header = el().querySelector('th.sortable') as HTMLElement;
    header.click();
    fixture.detectChanges();
    expect(last().sorting).toBe('name asc');
    header.click();
    fixture.detectChanges();
    expect(last().sorting).toBe('name desc');
    expect(last().skipCount).toBe(0);
  });

  it('pages forward by maxResultCount via the offset pager', () => {
    const buttons = Array.from(el().querySelectorAll('.mt__pbtn')) as HTMLButtonElement[];
    // totalCount 5 > pageSize 2 -> pager is shown (Prev + Next).
    expect(buttons).toHaveSize(2);
    buttons[1].click(); // Next
    fixture.detectChanges();
    expect(last().skipCount).toBe(2);
  });

  it('renders the empty message when a page returns no rows', () => {
    host.page = { items: [], totalCount: 0 };
    host.reload$.next();
    fixture.detectChanges();
    expect(el().textContent).toContain('Nothing here.');
  });

  it('refetches when the reload trigger emits (preserving the current page)', () => {
    const before = host.queries.length;
    host.reload$.next();
    fixture.detectChanges();
    expect(host.queries).toHaveSize(before + 1);
  });

  /**
   * The Sort by list and its direction button, the paging guards, and a cell template that
   * comes and goes after the first render.
   *
   * <p>The guards are called on the table directly: the template disables the buttons that
   * would reach them, and a click on a disabled button never fires, so the DOM cannot prove
   * the method refuses on its own.</p>
   */
  describe('sort controls, paging guards and template changes', () => {
    interface TableProbe {
      toggleDir(): void;
      prev(): void;
      next(): void;
    }
    const table = () =>
      fixture.debugElement.query(By.directive(ManagedTableComponent))
        .componentInstance as unknown as TableProbe;

    function chooseSort(value: string): void {
      const select = el().querySelector('.mt__sort select') as HTMLSelectElement;
      select.value = value;
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    }

    it('sorts ascending by the column chosen in Sort by, from the first page', () => {
      table().next();
      chooseSort('name');
      expect(last().sorting).toBe('name asc');
      expect(last().skipCount).toBe(0);
    });

    it('returns to the default order when Default is chosen', () => {
      chooseSort('name');
      chooseSort('');
      expect(last().sorting).toBe('');
    });

    it('flips the direction from the direction button once a column is chosen', () => {
      chooseSort('name');
      (el().querySelector('button.mt__dir') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(last().sorting).toBe('name desc');
      expect(last().skipCount).toBe(0);
    });

    it('does not reload for a direction change with no sort column', () => {
      const before = host.queries.length;
      table().toggleDir();
      expect(host.queries).toHaveSize(before);
    });

    it('ignores a click on a column that is not sortable', () => {
      const before = host.queries.length;
      (el().querySelectorAll('thead th')[1] as HTMLElement).click();
      fixture.detectChanges();
      expect(host.queries).toHaveSize(before);
    });

    it('pages back by one page, and not past the first', () => {
      const before = host.queries.length;
      table().prev();
      expect(host.queries).withContext('already on the first page').toHaveSize(before);

      table().next();
      table().prev();
      expect(last().skipCount).toBe(0);
      expect(host.queries).toHaveSize(before + 2);
    });

    it('does not page past the last page', () => {
      host.page = { items: [], totalCount: 2 };
      host.reload$.next();
      const before = host.queries.length;
      table().next();
      expect(host.queries).toHaveSize(before);
    });

    it('picks up a cell template that is removed or added after the first render', () => {
      host.showCell = false;
      fixture.detectChanges();
      expect(el().textContent).not.toContain('NAME:Alpha');

      host.showCell = true;
      fixture.detectChanges();
      expect(el().textContent).toContain('NAME:Alpha');
    });
  });
});
