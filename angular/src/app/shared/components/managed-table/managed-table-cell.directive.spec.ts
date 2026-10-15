import {
  ManagedTableCellDirective,
  ManagedTableRowActionsDirective,
} from './managed-table-cell.directive';

/**
 * The two template-context guards exist for Angular's template type checker: they let `let row`
 * be typed as the row instead of `any`. At runtime they are constant `true`, so this pins only that
 * they never reject a context -- a guard returning false would make the compiler treat every
 * projected template's row as unreachable.
 */
describe('managed-table template context guards', () => {
  it('accepts any context for a cell template', () => {
    expect(
      ManagedTableCellDirective.ngTemplateContextGuard({} as ManagedTableCellDirective, {}),
    ).toBeTrue();
  });

  it('accepts any context for a row-actions template', () => {
    expect(
      ManagedTableRowActionsDirective.ngTemplateContextGuard(
        {} as ManagedTableRowActionsDirective,
        {},
      ),
    ).toBeTrue();
  });
});
