import {
  AddressChoice,
  AddressDiffItem,
  ConfirmAddressDialogComponent,
} from './confirm-address-dialog.component';

/**
 * The pre-submit address dialog: one entry per address whose standardized form differs, each
 * defaulting to the suggestion, resolved as a per-address choice map.
 *
 * <p>It had no spec. It injects nothing, so it is built with `new`.</p>
 *
 * <p>Addresses below are synthetic.</p>
 */
describe('ConfirmAddressDialogComponent', () => {
  const item = (key: string): AddressDiffItem => ({
    key,
    label: `${key} address`,
    enteredLines: ['1 Example Way'],
    suggestedLines: ['1 Example Way Apt 2'],
  });

  function create(items: AddressDiffItem[]): {
    c: ConfirmAddressDialogComponent;
    out: Record<string, AddressChoice>[];
  } {
    const c = new ConfirmAddressDialogComponent();
    c.items = items;
    const out: Record<string, AddressChoice>[] = [];
    c.resolved.subscribe((choices) => out.push(choices));
    return { c, out };
  }

  it('defaults every address to the suggestion', () => {
    const { c } = create([item('patient'), item('employer')]);
    c.ngOnInit();
    expect(c.choices).toEqual({ patient: 'suggested', employer: 'suggested' });
  });

  it('resolves with the choices as they stand, as a copy', () => {
    const { c, out } = create([item('patient'), item('employer')]);
    c.ngOnInit();
    c.choices['employer'] = 'mine';

    c.confirm();

    expect(out).toEqual([{ patient: 'suggested', employer: 'mine' }]);
    expect(out[0]).not.toBe(c.choices);
  });

  it('keeps every address as entered, whatever was chosen', () => {
    const { c, out } = create([item('patient'), item('employer')]);
    c.ngOnInit();

    c.keepAllMine();

    expect(out).toEqual([{ patient: 'mine', employer: 'mine' }]);
  });
});
