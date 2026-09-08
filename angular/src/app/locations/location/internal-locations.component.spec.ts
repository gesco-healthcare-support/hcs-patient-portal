import { TestBed } from '@angular/core/testing';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalLocationsComponent } from './internal-locations.component';
import { LocationService } from '../../proxy/locations/location.service';

/**
 * Covers the Escape-to-close handler added in sweep #658. Before it, both
 * modals could be dismissed only with the mouse.
 *
 * The component is created but never change-detected, so `ngOnInit` -- which
 * loads state and appointment-type lookups over HTTP -- does not run. Only the
 * handler is exercised, driven by the writable signals.
 */
describe('InternalLocationsComponent Escape handling (sweep #658)', () => {
  interface Probe {
    form: { set(value: unknown): void };
    confirmDelete: { set(value: unknown): void };
    isBusy: { set(value: boolean): void };
    onEscapeKey(): void;
  }

  function create() {
    TestBed.configureTestingModule({
      providers: [
        { provide: LocationService, useValue: {} },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    const fixture = TestBed.createComponent(InternalLocationsComponent);
    return {
      fixture,
      probe: fixture.componentInstance as unknown as Probe,
      readForm: () => (fixture.componentInstance as unknown as { form(): unknown }).form(),
      readDelete: () =>
        (fixture.componentInstance as unknown as { confirmDelete(): unknown }).confirmDelete(),
    };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('closes the edit form', () => {
    const c = create();
    c.probe.form.set({ name: 'Ontario' });
    c.probe.onEscapeKey();
    expect(c.readForm()).toBeNull();
  });

  it('closes the delete confirmation', () => {
    const c = create();
    c.probe.confirmDelete.set({ location: { id: 'x' } });
    c.probe.onEscapeKey();
    expect(c.readDelete()).toBeNull();
  });

  it('closes only the delete confirmation when both are open, because it renders on top', () => {
    const c = create();
    c.probe.form.set({ name: 'Ontario' });
    c.probe.confirmDelete.set({ location: { id: 'x' } });
    c.probe.onEscapeKey();
    expect(c.readDelete()).toBeNull();
    expect(c.readForm()).not.toBeNull();
  });

  it('does not discard a save in flight', () => {
    // The guard is inherited from closeModal/cancelDelete rather than
    // reimplemented, so Escape must be inert while isBusy is set.
    const c = create();
    c.probe.form.set({ name: 'Ontario' });
    c.probe.confirmDelete.set({ location: { id: 'x' } });
    c.probe.isBusy.set(true);
    c.probe.onEscapeKey();
    expect(c.readDelete()).not.toBeNull();
    c.probe.confirmDelete.set(null);
    c.probe.onEscapeKey();
    expect(c.readForm()).not.toBeNull();
  });

  it('is inert when nothing is open', () => {
    const c = create();
    expect(() => c.probe.onEscapeKey()).not.toThrow();
    expect(c.readForm()).toBeNull();
    expect(c.readDelete()).toBeNull();
  });

  it('is wired to a real document Escape keypress, not just callable', () => {
    // Proves the @HostListener binding, which a direct method call cannot.
    const c = create();
    c.probe.form.set({ name: 'Ontario' });
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.readForm()).toBeNull();
  });
});
