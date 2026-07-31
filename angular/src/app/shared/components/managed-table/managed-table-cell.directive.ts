import { Directive, Input, TemplateRef, inject } from '@angular/core';

/**
 * Template context exposed to projected cell / row-action templates. The row is
 * the `$implicit` value, so hosts write `*managedTableCell="'email'"; let row` and
 * read `row.email`. The same row is also exposed as `row` for explicit binding.
 */
export interface ManagedTableRowContext<T = any> {
  $implicit: T;
  row: T;
}

/**
 * Projects a custom cell template for one column. Usage:
 *
 *   <ng-container *managedTableCell="'role'; let row">
 *     <span class="ux-role">{{ row.roleName }}</span>
 *   </ng-container>
 *
 * The directive's input value is the column key; the managed table matches it to
 * a {@link ManagedTableColumn}.key. Columns without a matching directive render as
 * plain text.
 */
@Directive({
  // eslint-disable-next-line @angular-eslint/directive-selector -- established short attribute selector; renaming would touch every host table
  selector: '[managedTableCell]',
  standalone: true,
})
export class ManagedTableCellDirective<T = any> {
  /** The column key this template renders. */
  @Input('managedTableCell') columnKey = '';

  readonly template: TemplateRef<ManagedTableRowContext<T>> = inject(TemplateRef);

  /** Lets the template compiler type `let row` as the row, not `any`. */
  static ngTemplateContextGuard<T>(
    _dir: ManagedTableCellDirective<T>,
    _ctx: unknown,
  ): _ctx is ManagedTableRowContext<T> {
    return true;
  }
}

/**
 * Projects the per-row action slot (the trailing actions column). Usage:
 *
 *   <button *managedTableRowActions="let row" (click)="doThing(row)">...</button>
 *
 * The managed table renders a trailing column only when this slot is present.
 */
@Directive({
  // eslint-disable-next-line @angular-eslint/directive-selector -- established short attribute selector; renaming would touch every host table
  selector: '[managedTableRowActions]',
  standalone: true,
})
export class ManagedTableRowActionsDirective<T = any> {
  readonly template: TemplateRef<ManagedTableRowContext<T>> = inject(TemplateRef);

  static ngTemplateContextGuard<T>(
    _dir: ManagedTableRowActionsDirective<T>,
    ctx: unknown,
  ): ctx is ManagedTableRowContext<T> {
    return true;
  }
}
