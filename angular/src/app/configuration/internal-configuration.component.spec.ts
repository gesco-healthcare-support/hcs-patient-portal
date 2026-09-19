import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalConfigurationComponent } from './internal-configuration.component';
import { ConfigSectionGateway } from './config-section.gateway';
import { AppointmentTypeFieldConfigService } from '../proxy/appointment-type-field-configs/appointment-type-field-config.service';
import { AppointmentTypeService } from '../proxy/appointment-types/appointment-type.service';

/**
 * Covers the Escape-to-close handler added in sweep #653, including the bubble
 * path -- the part a `document.dispatchEvent` spec cannot see, because it starts
 * the event AT the listener and skips the journey a real keypress makes.
 */
describe('InternalConfigurationComponent Escape handling (sweep #653)', () => {
  interface Probe {
    form: { set(value: unknown): void };
    isBusy: { set(value: boolean): void };
    rows: { set(value: unknown[]): void };
    typesSummary(row: unknown): string;
    onEscapeKey(): void;
  }

  function create() {
    TestBed.configureTestingModule({
      providers: [
        { provide: ActivatedRoute, useValue: { data: of({ section: 'types' }) } },
        { provide: ConfigSectionGateway, useValue: { list: () => of([]) } },
        { provide: AppointmentTypeService, useValue: { getList: () => of({ items: [] }) } },
        {
          provide: AppointmentTypeFieldConfigService,
          useValue: { getByAppointmentTypeId: () => of([]) },
        },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    const fixture = TestBed.createComponent(InternalConfigurationComponent);
    const inst = fixture.componentInstance as unknown as Probe & { form(): unknown };
    return { fixture, probe: inst as Probe, readForm: () => inst.form() };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('closes the modal on Escape', () => {
    const c = create();
    c.probe.form.set({ name: 'PQME' });
    c.probe.onEscapeKey();
    expect(c.readForm()).toBeNull();
  });

  it('does not discard a save in flight', () => {
    // The guard is inherited from closeModal rather than reimplemented.
    const c = create();
    c.probe.form.set({ name: 'PQME' });
    c.probe.isBusy.set(true);
    c.probe.onEscapeKey();
    expect(c.readForm()).not.toBeNull();
  });

  it('is inert when no modal is open', () => {
    const c = create();
    expect(() => c.probe.onEscapeKey()).not.toThrow();
    expect(c.readForm()).toBeNull();
  });

  it('is wired to a real document Escape keypress, not just callable', () => {
    // Proves the @HostListener binding, which a direct method call cannot.
    const c = create();
    c.probe.form.set({ name: 'PQME' });
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.readForm()).toBeNull();
  });

  /**
   * Escape must survive the real bubble path, not just the binding.
   *
   * The actions cell carries a stopPropagation guard so Enter on a row button
   * does not also fire the row's own (keydown.enter). openEdit lives inside that
   * cell and there is no focus management here, so focus stays on the button
   * after activation -- and the Escape handler is a document-level HostListener
   * at the END of the bubble path.
   *
   * This is why the guard is (keydown.enter) and not a bare (keydown): the
   * broader form swallows Escape and makes the modal undismissable by keyboard.
   * The sibling sweeps #719 and #720 shipped that bug and had to be corrected;
   * this spec exists so it cannot come back here.
   */
  it('closes the modal on Escape pressed from inside the actions cell', () => {
    const c = create();
    c.fixture.detectChanges();
    (c.probe as unknown as { rows: { set(v: unknown[]): void } }).rows.set([
      { id: 'cfg-1', name: 'PQME', description: 'Panel QME', isSystem: false },
    ]);
    c.fixture.detectChanges();

    const host = c.fixture.nativeElement as HTMLElement;
    const button = host.querySelector('button[title="Edit"]') as HTMLButtonElement | null;
    expect(button).withContext('row edit button should render').not.toBeNull();

    c.probe.form.set({ name: 'PQME' });
    button!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.readForm())
      .withContext('Escape from inside the actions cell must reach the document')
      .toBeNull();
  });

  /**
   * typesSummary had a ternary nested inside a template literal (Sonar
   * typescript:S3358). Extracting the pluralisation is behaviour-preserving only
   * if singular and plural still land on the right side of the boundary, which
   * is exactly what an extraction like this can get wrong -- so both sides of
   * count === 1 are pinned, not just a representative case.
   */
  describe('typesSummary (sweep #653)', () => {
    it('short-circuits when the row applies to all types', () => {
      const c = create();
      expect(c.probe.typesSummary({ appliesToAll: true, appointmentTypeIds: [] })).toBe(
        'All types',
      );
    });

    it('reports none when the list is empty or absent', () => {
      const c = create();
      expect(c.probe.typesSummary({ appliesToAll: false, appointmentTypeIds: [] })).toBe(
        'No types',
      );
      expect(c.probe.typesSummary({ appliesToAll: false })).toBe('No types');
    });

    it('is singular at exactly one and plural above it', () => {
      const c = create();
      expect(c.probe.typesSummary({ appliesToAll: false, appointmentTypeIds: ['a'] })).toBe(
        '1 type',
      );
      expect(c.probe.typesSummary({ appliesToAll: false, appointmentTypeIds: ['a', 'b'] })).toBe(
        '2 types',
      );
    });
  });
});
