import {
  AfterContentInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  ContentChildren,
  DestroyRef,
  Input,
  OnInit,
  QueryList,
  TemplateRef,
  inject,
  signal,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, Subject, merge, of } from 'rxjs';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  startWith,
  switchMap,
  tap,
} from 'rxjs/operators';
import { IconComponent } from '../../ui/icon/icon.component';
import {
  ManagedTableCellDirective,
  ManagedTableRowActionsDirective,
  ManagedTableRowContext,
} from './managed-table-cell.directive';
import {
  ManagedTableColumn,
  ManagedTableDataSource,
  ManagedTablePage,
  ManagedTableQuery,
} from './managed-table.models';

/**
 * QA item B (2026-06-30) -- reusable, server-driven table.
 *
 * One bespoke standalone table that every host-portal grid pages against. It owns
 * the chrome (search box, "Sort by" dropdown + sortable headers, offset pager,
 * loading / empty rows) and delegates ALL data to a {@link ManagedTableDataSource}
 * callback: each search / sort / page change builds a {@link ManagedTableQuery}
 * (filter / sorting / skipCount / maxResultCount) and the host returns one
 * {@link ManagedTablePage}. The component never holds more than one page, so it
 * scales to tables with thousands of rows (the reason a client-side load-all was
 * rejected).
 *
 * Cells: a column whose `key` matches a projected `*managedTableCell` template
 * renders that template (avatars, chips, the officeName pipe, editable inputs,
 * conditional row actions); every other column renders `row[key]` as text. A
 * `*managedTableRowActions` slot adds a trailing actions column. Both slots expose
 * the row as `$implicit` (so hosts write `let row`).
 *
 * Reactivity: OnPush + signals. In-flight requests are switchMap-cancelled, search
 * is debounced 300ms, and any search / sort change resets to page 0. A parent can
 * force a refetch (after a mutation) by emitting on the optional `reload$` trigger.
 */
@Component({
  selector: 'app-managed-table',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, IconComponent],
  template: `
    <div class="mt">
      <div class="mt__toolbar">
        <div class="mt__search">
          <app-icon name="search" [size]="16" />
          <input
            type="search"
            [attr.placeholder]="searchPlaceholder"
            [value]="search()"
            [disabled]="busy"
            (input)="onSearch($event)"
            aria-label="Search"
          />
        </div>
        @if (sortableColumns.length) {
          <div class="mt__sort">
            <app-icon name="sort" [size]="15" />
            <label>Sort by</label>
            <select
              [value]="sortKey() ?? ''"
              [disabled]="busy"
              (change)="onSortSelect($event)"
              aria-label="Sort by column"
            >
              <option value="">Default</option>
              @for (c of sortableColumns; track c.key) {
                <option [value]="sortKeyOf(c)">{{ c.header }}</option>
              }
            </select>
            <button
              type="button"
              class="mt__dir"
              [disabled]="busy || !sortKey()"
              [title]="sortDir() === 'asc' ? 'Ascending' : 'Descending'"
              (click)="toggleDir()"
            >
              <app-icon [name]="sortDir() === 'asc' ? 'arrowUp' : 'arrowDown'" [size]="15" />
            </button>
          </div>
        }
      </div>

      <div class="mt__scroll">
        <table class="mt__table">
          <thead>
            <tr>
              @for (c of columns; track c.key) {
                <th
                  [class.r]="c.align === 'right'"
                  [class.c]="c.align === 'center'"
                  [class.sortable]="c.sortable"
                  [attr.aria-sort]="ariaSort(c)"
                  (click)="onHeaderClick(c)"
                >
                  @if (!c.headerHidden) {
                    <span class="mt__th">
                      {{ c.header }}
                      @if (c.sortable && isSorted(c)) {
                        <app-icon
                          [name]="sortDir() === 'asc' ? 'arrowUp' : 'arrowDown'"
                          [size]="13"
                        />
                      }
                    </span>
                  }
                </th>
              }
              @if (hasRowActions()) {
                <th class="mt__acth"></th>
              }
            </tr>
          </thead>
          <tbody>
            @if (loading()) {
              <tr class="mt__msg">
                <td [attr.colspan]="totalColumns()">Loading...</td>
              </tr>
            } @else if (!rows().length) {
              <tr class="mt__msg">
                <td [attr.colspan]="totalColumns()">{{ emptyText }}</td>
              </tr>
            } @else {
              @for (row of rows(); track trackKey(row)) {
                <tr>
                  @for (c of columns; track c.key) {
                    <td [class.r]="c.align === 'right'" [class.c]="c.align === 'center'">
                      @if (templateFor(c.key); as tpl) {
                        <ng-container
                          [ngTemplateOutlet]="tpl"
                          [ngTemplateOutletContext]="rowContext(row)"
                        />
                      } @else {
                        {{ valueOf(row, c.key) }}
                      }
                    </td>
                  }
                  @if (rowActionsTemplate(); as actions) {
                    <td class="mt__acts">
                      <ng-container
                        [ngTemplateOutlet]="actions"
                        [ngTemplateOutletContext]="rowContext(row)"
                      />
                    </td>
                  }
                </tr>
              }
            }
          </tbody>
        </table>
      </div>

      @if (totalCount() > pageSize) {
        <div class="mt__pager">
          <span class="mt__range">{{ rangeStart() }}-{{ rangeEnd() }} of {{ totalCount() }}</span>
          <button
            type="button"
            class="mt__pbtn"
            [disabled]="busy || loading() || skipCount() === 0"
            (click)="prev()"
          >
            <app-icon name="chevLeft" [size]="15" />
            Prev
          </button>
          <button
            type="button"
            class="mt__pbtn"
            [disabled]="busy || loading() || !hasNext()"
            (click)="next()"
          >
            Next
            <app-icon name="chevRight" [size]="15" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .mt {
      background: #fff;
      border: 1px solid var(--border, #e6ebf2);
      border-radius: var(--r-md, 11px);
      box-shadow: var(--sh-sm, 0 1px 3px rgba(15, 28, 46, 0.07));
      overflow: hidden;
    }
    .mt__toolbar {
      display: flex;
      gap: 10px;
      align-items: center;
      flex-wrap: wrap;
      padding: 12px 14px;
      border-bottom: 1px solid var(--border, #e6ebf2);
      background: var(--n-25, #fafbfd);
    }
    .mt__search {
      flex: 1;
      min-width: 220px;
      max-width: 420px;
      display: flex;
      align-items: center;
      gap: 9px;
      background: #fff;
      border: 1px solid var(--border-strong, #d8deea);
      border-radius: 10px;
      padding: 0 14px;
      color: var(--n-400, #9aa6b8);
      transition: all 0.15s;
    }
    .mt__search:focus-within {
      border-color: var(--blue-400, #2f7cbf);
      box-shadow: 0 0 0 4px var(--blue-50, #eef5fb);
      color: var(--blue-600, #075ca1);
    }
    .mt__search input {
      border: 0;
      outline: none;
      background: transparent;
      padding: 9px 0;
      font-size: 13.5px;
      width: 100%;
      color: var(--n-800, #28303c);
      font-family: var(--font, inherit);
    }
    .mt__sort {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      color: var(--n-500, #6b7789);
      font-size: 12.5px;
      font-weight: 600;
    }
    .mt__sort label {
      margin: 0;
    }
    .mt__sort select {
      border: 1px solid var(--border-strong, #d8deea);
      background: #fff;
      color: var(--n-700, #3b4554);
      border-radius: 9px;
      padding: 8px 10px;
      font-size: 13px;
      font-family: var(--font, inherit);
      cursor: pointer;
    }
    .mt__sort select:focus {
      outline: none;
      border-color: var(--blue-400, #2f7cbf);
      box-shadow: 0 0 0 4px var(--blue-50, #eef5fb);
    }
    .mt__dir {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 34px;
      height: 34px;
      border: 1px solid var(--border-strong, #d8deea);
      background: #fff;
      color: var(--n-600, #515c6e);
      border-radius: 9px;
      cursor: pointer;
      transition: all 0.14s;
    }
    .mt__dir:hover:not(:disabled) {
      border-color: var(--blue-300, #6ea7d6);
      color: var(--blue-700, #055495);
    }
    .mt__dir:disabled {
      opacity: 0.5;
      cursor: default;
    }
    .mt__scroll {
      overflow-x: auto;
    }
    .mt__table {
      width: 100%;
      border-collapse: separate;
      border-spacing: 0;
      font-size: 13px;
      min-width: 680px;
    }
    .mt__table thead th {
      text-align: left;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--n-500, #6b7789);
      padding: 12px 14px;
      background: var(--n-50, #f5f7fa);
      border-bottom: 1px solid var(--border, #e6ebf2);
      white-space: nowrap;
    }
    .mt__table thead th.r {
      text-align: right;
    }
    .mt__table thead th.c {
      text-align: center;
    }
    .mt__table thead th.sortable {
      cursor: pointer;
      user-select: none;
    }
    .mt__table thead th.sortable:hover {
      color: var(--blue-700, #055495);
    }
    .mt__th {
      display: inline-flex;
      align-items: center;
      gap: 5px;
    }
    .mt__table thead th.r .mt__th {
      flex-direction: row-reverse;
    }
    .mt__table tbody td {
      padding: 12px 14px;
      border-bottom: 1px solid var(--n-100, #eef1f6);
      color: var(--n-700, #3b4554);
      vertical-align: middle;
      white-space: nowrap;
    }
    .mt__table tbody td.r {
      text-align: right;
    }
    .mt__table tbody td.c {
      text-align: center;
    }
    .mt__table tbody tr:last-child td {
      border-bottom: 0;
    }
    .mt__table tbody tr:hover {
      background: var(--blue-50, #eef5fb);
    }
    .mt__msg td {
      padding: 22px 14px;
      text-align: center;
      color: var(--n-400, #9aa6b8);
      font-size: 12.5px;
      white-space: normal;
    }
    .mt__msg:hover td {
      background: transparent;
    }
    .mt__acth {
      width: 1%;
    }
    .mt__acts {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      gap: 6px;
    }
    .mt__pager {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 10px;
      padding: 11px 14px;
      border-top: 1px solid var(--border, #e6ebf2);
      background: var(--n-25, #fafbfd);
    }
    .mt__range {
      margin-right: auto;
      font-size: 12.5px;
      color: var(--n-500, #6b7789);
      font-family: var(--font-num, inherit);
    }
    .mt__pbtn {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      border: 1px solid var(--border-strong, #d8deea);
      background: #fff;
      color: var(--n-700, #3b4554);
      padding: 7px 12px;
      border-radius: 9px;
      font-size: 12.5px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.14s;
    }
    .mt__pbtn:hover:not(:disabled) {
      border-color: var(--blue-300, #6ea7d6);
      color: var(--blue-700, #055495);
      background: var(--blue-50, #eef5fb);
    }
    .mt__pbtn:disabled {
      opacity: 0.5;
      cursor: default;
    }
  `,
})
export class ManagedTableComponent implements OnInit, AfterContentInit {
  /** Server-paging callback. Required -- the table is useless without it. */
  @Input({ required: true }) dataSource!: ManagedTableDataSource<unknown>;
  /** Column definitions, left to right. */
  @Input() columns: ManagedTableColumn[] = [];
  /** Rows per page (maxResultCount). */
  @Input() pageSize = 20;
  /** Placeholder for the search box. */
  @Input() searchPlaceholder = 'Search...';
  /** When true, the table chrome (search / sort / pager) is disabled. */
  @Input() busy = false;
  /** Message shown when a page returns no rows. */
  @Input() emptyText = 'No records found.';
  /** Row field used as the @for track key. */
  @Input() trackByKey = 'id';
  /** Optional trigger so a parent can force a refetch (e.g. after a mutation). */
  @Input() reload$?: Observable<void>;

  protected readonly rows = signal<unknown[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly search = signal('');
  protected readonly skipCount = signal(0);
  protected readonly sortKey = signal<string | null>(null);
  protected readonly sortDir = signal<'asc' | 'desc'>('asc');

  /** Sortable columns, for the Sort by dropdown. Derived once from `columns`. */
  protected sortableColumns: ManagedTableColumn[] = [];

  @ContentChildren(ManagedTableCellDirective, { descendants: true })
  private cellDirectives!: QueryList<ManagedTableCellDirective>;
  @ContentChild(ManagedTableRowActionsDirective)
  private rowActionsDirective?: ManagedTableRowActionsDirective;

  private cellTemplates = new Map<string, TemplateRef<ManagedTableRowContext>>();

  private readonly reloadTrigger = new Subject<void>();
  private readonly searchTerm = new Subject<string>();
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);

  ngOnInit(): void {
    this.sortableColumns = this.columns.filter((c) => c.sortable);

    const searchChanges = this.searchTerm.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap((term) => {
        this.search.set(term);
        this.skipCount.set(0);
      }),
      map(() => undefined),
    );

    merge(this.reloadTrigger, searchChanges, this.reload$ ?? EMPTY)
      .pipe(
        startWith(undefined),
        switchMap(() => {
          this.loading.set(true);
          return this.runQuery();
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((page) => {
        this.rows.set(page.items ?? []);
        this.totalCount.set(page.totalCount ?? 0);
        this.loading.set(false);
      });
  }

  ngAfterContentInit(): void {
    this.rebuildCellTemplates();
    this.cellDirectives.changes.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.rebuildCellTemplates();
      this.cdr.markForCheck();
    });
  }

  private runQuery(): Observable<ManagedTablePage<unknown>> {
    const query: ManagedTableQuery = {
      search: this.search(),
      sorting: this.sortingClause(),
      skipCount: this.skipCount(),
      maxResultCount: this.pageSize,
    };
    return this.dataSource(query).pipe(catchError(() => of({ items: [], totalCount: 0 })));
  }

  private sortingClause(): string {
    const key = this.sortKey();
    return key ? `${key} ${this.sortDir()}` : '';
  }

  private rebuildCellTemplates(): void {
    const map = new Map<string, TemplateRef<ManagedTableRowContext>>();
    this.cellDirectives.forEach((dir) => map.set(dir.columnKey, dir.template));
    this.cellTemplates = map;
  }

  protected onSearch(event: Event): void {
    this.searchTerm.next((event.target as HTMLInputElement).value);
  }

  protected onSortSelect(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.sortKey.set(value || null);
    this.sortDir.set('asc');
    this.skipCount.set(0);
    this.reloadTrigger.next();
  }

  protected toggleDir(): void {
    if (!this.sortKey()) {
      return;
    }
    this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    this.skipCount.set(0);
    this.reloadTrigger.next();
  }

  protected onHeaderClick(column: ManagedTableColumn): void {
    if (!column.sortable) {
      return;
    }
    const key = this.sortKeyOf(column);
    if (this.sortKey() === key) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortKey.set(key);
      this.sortDir.set('asc');
    }
    this.skipCount.set(0);
    this.reloadTrigger.next();
  }

  protected prev(): void {
    if (this.skipCount() === 0) {
      return;
    }
    this.skipCount.set(Math.max(0, this.skipCount() - this.pageSize));
    this.reloadTrigger.next();
  }

  protected next(): void {
    if (!this.hasNext()) {
      return;
    }
    this.skipCount.set(this.skipCount() + this.pageSize);
    this.reloadTrigger.next();
  }

  protected hasNext(): boolean {
    return this.skipCount() + this.pageSize < this.totalCount();
  }

  protected rangeStart(): number {
    return this.totalCount() === 0 ? 0 : this.skipCount() + 1;
  }

  protected rangeEnd(): number {
    return Math.min(this.skipCount() + this.pageSize, this.totalCount());
  }

  protected sortKeyOf(column: ManagedTableColumn): string {
    return column.sortKey ?? column.key;
  }

  protected isSorted(column: ManagedTableColumn): boolean {
    return this.sortKey() === this.sortKeyOf(column);
  }

  protected ariaSort(column: ManagedTableColumn): string | null {
    if (!column.sortable || !this.isSorted(column)) {
      return column.sortable ? 'none' : null;
    }
    return this.sortDir() === 'asc' ? 'ascending' : 'descending';
  }

  protected templateFor(key: string): TemplateRef<ManagedTableRowContext> | null {
    return this.cellTemplates.get(key) ?? null;
  }

  protected hasRowActions(): boolean {
    return !!this.rowActionsDirective;
  }

  protected rowActionsTemplate(): TemplateRef<ManagedTableRowContext> | null {
    return this.rowActionsDirective?.template ?? null;
  }

  protected totalColumns(): number {
    return this.columns.length + (this.hasRowActions() ? 1 : 0);
  }

  protected rowContext(row: unknown): ManagedTableRowContext {
    return { $implicit: row, row };
  }

  protected valueOf(row: unknown, key: string): unknown {
    return (row as Record<string, unknown>)?.[key];
  }

  protected trackKey(row: unknown): unknown {
    return (row as Record<string, unknown>)?.[this.trackByKey] ?? row;
  }
}
