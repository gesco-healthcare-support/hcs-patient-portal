import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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
        {
          provide: LocationService,
          useValue: {
            getStateLookup: () => of({ items: [], totalCount: 0 }),
            getAppointmentTypeLookup: () => of({ items: [], totalCount: 0 }),
            getList: () => of({ items: [], totalCount: 0 }),
          },
        },
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

  /**
   * Escape must survive the real bubble path, not just the binding.
   *
   * The actions cell carries a stopPropagation guard so Enter on a row button
   * does not also fire the row's own (keydown.enter). Both buttons that OPEN a
   * modal -- openEdit and askDelete -- live inside that cell, and there is no
   * focus management here, so focus stays on the button after activation.
   *
   * A document-level HostListener sits at the END of the bubble path, so an
   * unconditional (keydown) guard swallows Escape before it arrives. Dispatching
   * straight at `document` cannot see that, because it starts the event AT the
   * listener and skips the path a real keypress travels.
   */
  it('closes the edit form on Escape pressed from inside the actions cell', () => {
    const c = create();
    // First detectChanges runs ngOnInit, which loads lookups and clears loading.
    // Seeding rows before it would be overwritten by load().
    c.fixture.detectChanges();
    (c.probe as unknown as { rows: { set(v: unknown[]): void } }).rows.set([
      { location: { id: 'loc-1', name: 'Ontario', isActive: true }, appointmentTypes: [] },
    ]);
    c.fixture.detectChanges();

    const host = c.fixture.nativeElement as HTMLElement;
    const button = host.querySelector('.ra-rowbtn') as HTMLButtonElement | null;
    expect(button).withContext('row action button should render').not.toBeNull();

    c.probe.form.set({ name: 'Ontario' });
    button!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.readForm())
      .withContext('Escape from inside the actions cell must reach the document')
      .toBeNull();
  });
});
