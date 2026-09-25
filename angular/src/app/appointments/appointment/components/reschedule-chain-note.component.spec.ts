import { RescheduleChainNoteComponent } from './reschedule-chain-note.component';

/**
 * The note on a rescheduled appointment that links back to the one it replaced, with a
 * collapsible history of who agreed.
 *
 * <p>It had no spec. It injects nothing, so it is built with `new`.</p>
 */
describe('RescheduleChainNoteComponent', () => {
  interface Probe {
    canOpenSource: boolean;
    historyOpen: boolean;
    toggleHistory(): void;
    stepLabel(kind: string): string;
  }

  const create = () => new RescheduleChainNoteComponent() as unknown as Probe;

  it('starts with the source not linkable and the history collapsed', () => {
    const c = create();
    expect(c.canOpenSource).toBeFalse();
    expect(c.historyOpen).toBeFalse();
  });

  it('opens and closes the history', () => {
    const c = create();
    c.toggleHistory();
    expect(c.historyOpen).toBeTrue();
    c.toggleHistory();
    expect(c.historyOpen).toBeFalse();
  });

  it('names each step in the chain', () => {
    const c = create();
    expect(c.stepLabel('side-a-agreed')).toBe('Patient side agreed');
    expect(c.stepLabel('side-b-agreed')).toBe('Defense side agreed');
    expect(c.stepLabel('decided')).toBe('Finalized by staff');
  });
});
