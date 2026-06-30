import { Observable } from 'rxjs';

/**
 * Public API for the reusable server-driven table (app-managed-table).
 *
 * The component owns search + sort + offset paging UI; the host owns the data.
 * Every state change (search, sort, page) is handed to the host as a
 * {@link ManagedTableQuery} via the {@link ManagedTableDataSource} callback, and
 * the host returns one page ({@link ManagedTablePage}). The component never loads
 * a full table into memory -- it asks the server for exactly one page at a time,
 * so it scales to tables with thousands of rows.
 */

/** The query the table hands to the data source on every search/sort/page change. */
export interface ManagedTableQuery {
  /** Free-text search box value (already trimmed by the component). */
  search: string;
  /** ABP `sorting` clause, e.g. "name asc" / "email desc". Empty = server default. */
  sorting: string;
  /** Offset of the first row to return (skipCount). */
  skipCount: number;
  /** Page size (maxResultCount). */
  maxResultCount: number;
}

/** One page of rows returned by the data source -- mirrors ABP `PagedResultDto<T>`. */
export interface ManagedTablePage<T> {
  items: T[];
  totalCount: number;
}

/**
 * Server-paging callback. Given a {@link ManagedTableQuery}, return an Observable
 * of one page. The host wires this to its paged proxy endpoint (filter / sorting /
 * skipCount / maxResultCount -> PagedResultDto).
 */
export type ManagedTableDataSource<T> = (query: ManagedTableQuery) => Observable<ManagedTablePage<T>>;

/**
 * One column definition. Columns with a matching projected `*managedTableCell`
 * template render that template; the rest render `row[key]` as plain text.
 */
export interface ManagedTableColumn {
  /** Cell key. Used to match a projected template and, by default, to read the value. */
  key: string;
  /** Column header text. */
  header: string;
  /** When true the header is clickable and the column appears in the Sort by dropdown. */
  sortable?: boolean;
  /** The server `sorting` field for this column. Defaults to {@link key}. */
  sortKey?: string;
  /** Cell + header text alignment. Defaults to left. */
  align?: 'left' | 'right' | 'center';
  /** When true the header label is suppressed (cell still renders). */
  headerHidden?: boolean;
}
