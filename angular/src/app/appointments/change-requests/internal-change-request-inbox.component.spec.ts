import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalChangeRequestInboxComponent } from './internal-change-request-inbox.component';
import { AppointmentChangeRequestApprovalService } from '../../proxy/appointment-change-requests/appointment-change-request-approval.service';

/**
 * Covers the Escape-to-close handler added in sweep #656. The modal could
 * previously be dismissed only with the mouse.
 *
 * The component is created but never change-detected, so `ngOnInit` -- which
 * loads the inbox over HTTP -- does not run.
 */
describe('InternalChangeRequestInboxComponent Escape handling (sweep #656)', () => {
  interface Probe {
    modal: { set(value: unknown): void };
    isBusy: { set(value: boolean): void };
    onEscapeKey(): void;
  }

  function create() {
    TestBed.configureTestingModule({
      providers: [
        { provide: AppointmentChangeRequestApprovalService, useValue: {} },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    const fixture = TestBed.createComponent(InternalChangeRequestInboxComponent);
    const inst = fixture.componentInstance as unknown as Probe & { modal(): unknown };
    return { probe: inst as Probe, readModal: () => inst.modal() };
  }

  /** Shape only matters insofar as the handler checks truthiness. */
  const anyModal = { kind: 'approve', row: { id: 'cr-1' } };

  afterEach(() => TestBed.resetTestingModule());

  it('closes an open modal on Escape', () => {
    const c = create();
    c.probe.modal.set(anyModal);
    c.probe.onEscapeKey();
    expect(c.readModal()).toBeNull();
  });

  it('does not discard an approval in flight', () => {
    // The guard is inherited from closeModal rather than reimplemented.
    const c = create();
    c.probe.modal.set(anyModal);
    c.probe.isBusy.set(true);
    c.probe.onEscapeKey();
    expect(c.readModal()).not.toBeNull();
  });

  it('is inert when no modal is open', () => {
    const c = create();
    expect(() => c.probe.onEscapeKey()).not.toThrow();
    expect(c.readModal()).toBeNull();
  });

  it('is wired to a real document Escape keypress, not just callable', () => {
    // Proves the @HostListener binding, which a direct method call cannot.
    const c = create();
    c.probe.modal.set(anyModal);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.readModal()).toBeNull();
  });
});
