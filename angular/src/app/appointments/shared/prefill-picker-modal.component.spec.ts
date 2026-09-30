import { PrefillPickerModalComponent } from './prefill-picker-modal.component';
import { PREFILL_SECTIONS, PrefillSelection, defaultPrefillSelection } from './prefill-sections';

/**
 * The "what has changed since the source appointment" picker. Each ticked section is cleared
 * from the prefilled form.
 *
 * <p>It had no spec. It injects nothing, so it is built with `new`.</p>
 */
describe('PrefillPickerModalComponent', () => {
  interface Probe {
    sections: typeof PREFILL_SECTIONS;
    draft: PrefillSelection;
    selection: PrefillSelection | null;
    confirmed: { subscribe(fn: (s: PrefillSelection) => void): unknown };
    toggle(section: keyof PrefillSelection): void;
    onConfirm(): void;
    onKeepEverything(): void;
  }

  function create(): { c: Probe; out: PrefillSelection[] } {
    const c = new PrefillPickerModalComponent() as unknown as Probe;
    const out: PrefillSelection[] = [];
    c.confirmed.subscribe((s) => out.push(s));
    return { c, out };
  }

  it('offers every prefill section and starts with nothing ticked', () => {
    const { c } = create();
    expect(c.sections).toBe(PREFILL_SECTIONS);
    expect(c.draft).toEqual(defaultPrefillSelection());
  });

  it('seeds the ticks from a previous answer, and resets to none for no answer', () => {
    const { c } = create();
    c.selection = { ...defaultPrefillSelection(), employer: true };
    expect(c.draft.employer).toBeTrue();

    c.selection = null;
    expect(c.draft).toEqual(defaultPrefillSelection());
  });

  it("does not share the seeded object, so ticking cannot change the caller's answer", () => {
    const { c } = create();
    const previous = { ...defaultPrefillSelection(), employer: true };
    c.selection = previous;

    c.toggle('employer');

    expect(previous.employer).toBeTrue();
    expect(c.draft.employer).toBeFalse();
  });

  it('confirms the ticked sections', () => {
    const { c, out } = create();
    c.toggle('patient');
    c.toggle('insurance');
    c.toggle('insurance');

    c.onConfirm();

    expect(out).toEqual([{ ...defaultPrefillSelection(), patient: true }]);
  });

  it('answers "nothing changed" with every section unticked, whatever is ticked', () => {
    const { c, out } = create();
    c.toggle('patient');

    c.onKeepEverything();

    expect(out).toEqual([defaultPrefillSelection()]);
  });
});
