import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { IconComponent } from '../ui/icon/icon.component';
import { ariaSortFor, nextSort, type SortModel } from './sort-state';

/**
 * QA #15 item 6 (2026-07-07) -- a clickable, sortable table header for the bespoke
 * `.ia-table` grids (appointments, reports, admin audit rail). Used as an attribute
 * on the `<th>` itself so the cell keeps its `columnheader` role and carries the
 * `aria-sort` state; the projected content stays the label, and a sort indicator
 * is appended (a faint idle glyph when unsorted so the column reads as sortable,
 * an up/down arrow when active).
 *
 *   <th appSortHeader="Patient.LastName" [model]="sort()" (sortChange)="onSort($event)">
 *     Patient
 *   </th>
 *
 * Emits the next {@link SortModel} on click (the 3-state asc/desc/clear cycle from
 * sort-state); the host owns the signal and decides what to do with the new model.
 */
@Component({
  selector: 'th[appSortHeader]',
  standalone: true,
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="sort-th" (click)="toggle()">
      <span class="sort-th__label"><ng-content /></span>
      @if (isActive) {
        <app-icon [name]="model.dir === 'asc' ? 'arrowUp' : 'arrowDown'" [size]="13" />
      } @else {
        <span class="sort-th__hint"><app-icon name="sort" [size]="13" /></span>
      }
    </button>
  `,
  styles: `
    .sort-th {
      all: unset;
      display: inline-flex;
      align-items: center;
      gap: 5px;
      cursor: pointer;
      font: inherit;
      color: inherit;
      letter-spacing: inherit;
      text-transform: inherit;
    }
    .sort-th:hover {
      color: var(--blue-700, #055495);
    }
    .sort-th:focus-visible {
      outline: 2px solid var(--blue-400, #2f7cbf);
      outline-offset: 2px;
      border-radius: 3px;
    }
    .sort-th__hint {
      opacity: 0.35;
      display: inline-flex;
    }
    .sort-th:hover .sort-th__hint {
      opacity: 0.7;
    }
  `,
  host: {
    class: 'ia-sortable',
    '[attr.aria-sort]': 'ariaSort',
  },
})
export class SortHeaderComponent {
  /** The server/comparator sort key this column carries. */
  @Input({ required: true, alias: 'appSortHeader' }) sortKey = '';
  /** The table's current sort. */
  @Input({ required: true }) model: SortModel = { key: null, dir: 'asc' };
  /** Emits the next SortModel (asc -> desc -> clear) when the header is clicked. */
  @Output() sortChange = new EventEmitter<SortModel>();

  protected get isActive(): boolean {
    return this.model.key === this.sortKey && this.model.key !== null;
  }

  protected get ariaSort(): 'ascending' | 'descending' | 'none' {
    return ariaSortFor(this.model, this.sortKey);
  }

  protected toggle(): void {
    this.sortChange.emit(nextSort(this.model, this.sortKey));
  }
}
