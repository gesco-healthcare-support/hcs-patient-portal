import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
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
      <b *managedTableCell="'name'; let row">NAME:{{ row.name }}</b>
      <button class="act" *managedTableRowActions="let row">ACT:{{ row.id }}</button>
    </app-managed-table>
  `,
})
class HostComponent {
  pageSize = 2;
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
    expect(host.queries.length).toBe(1);
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
    expect(buttons.length).toBe(2);
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
    expect(host.queries.length).toBe(before + 1);
  });
});
