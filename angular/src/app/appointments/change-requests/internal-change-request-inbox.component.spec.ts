import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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
    outcome: { set(value: unknown): void };
    reason: { set(value: string): void };
    confirmDate(): void;
    resendConsent(): void;
    confirmApprove(): void;
    confirmReject(): void;
  }

  function create() {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AppointmentChangeRequestApprovalService,
          useValue: { getPending: () => of({ items: [], totalCount: 0 }) },
        },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    const fixture = TestBed.createComponent(InternalChangeRequestInboxComponent);
    const inst = fixture.componentInstance as unknown as Probe & { modal(): unknown };
    return { fixture, probe: inst as Probe, readModal: () => inst.modal() };
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

  /**
   * The four entry points whose `!m || m.kind !== 'x'` guard was collapsed to
   * `m?.kind !== 'x'` in this sweep (Sonar typescript:S6582). Each guard is the
   * first statement in its method, so calling it with no modal open exercises
   * exactly the changed line.
   *
   * `not.toThrow()` is the assertion on purpose, and it is stronger than it
   * looks: the approval service is stubbed as an empty object, so a method that
   * failed to return early would call a function that does not exist and throw.
   * Passing therefore proves the guard short-circuited before touching it.
   */
  describe('nullish-modal guards (sweep #656)', () => {
    it('returns early from every guarded action when no modal is open', () => {
      const c = create();
      expect(() => c.probe.confirmDate()).not.toThrow();
      expect(() => c.probe.resendConsent()).not.toThrow();
      expect(() => c.probe.confirmApprove()).not.toThrow();
      expect(() => c.probe.confirmReject()).not.toThrow();
    });

    it('returns early when a modal of the wrong kind is open', () => {
      // confirmReject wants kind 'reject'; the approve modal must not satisfy it.
      const c = create();
      c.probe.modal.set({ kind: 'approve', row: { id: 'cr-1' } });
      expect(() => c.probe.confirmReject()).not.toThrow();
    });
  });

  /**
   * Escape must survive the real bubble path, not just the binding.
   *
   * The actions container carries a stopPropagation guard so that Enter on a
   * row button does not also fire the row's own (keydown.enter). Both buttons
   * that OPEN the modal live inside that container, and there is no focus
   * management here -- no autofocus, no ViewChild().focus(), no focus trap --
   * so after activating one, focus stays on the button, inside the guard.
   *
   * A document-level HostListener sits at the END of the bubble path. So an
   * unconditional (keydown) guard on the container swallows Escape before it
   * arrives, and the modal cannot be dismissed by keyboard at all. Dispatching
   * straight at `document` cannot see this, because it starts the event AT the
   * listener and skips the path entirely.
   */
  describe('Escape survives the bubble path from the actions container', () => {
    it('closes the modal when Escape is pressed with focus inside the actions container', () => {
      const c = create();
      // This first detectChanges runs ngOnInit -> load(), which sets `rows` from
      // the service and clears `loading`. Seeding rows before it would be undone.
      c.fixture.detectChanges();
      // Now seed one row so the real actions container and its buttons render.
      // The default tab is 'all', so visibleRows() applies no filter.
      (c.probe as unknown as { rows: { set(v: unknown[]): void } }).rows.set([
        {
          id: 'cr-1',
          appointmentConfirmationNumber: 'A00001',
          creationTime: '2026-09-01T00:00:00Z',
        },
      ]);
      c.fixture.detectChanges();

      const host = c.fixture.nativeElement as HTMLElement;
      const button = host.querySelector('.cr-row__acts button') as HTMLButtonElement | null;
      expect(button).withContext('actions container button should render').not.toBeNull();

      c.probe.modal.set({ kind: 'approve', row: { id: 'cr-1' } });
      button!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      expect(c.readModal())
        .withContext('Escape from inside the actions container must reach the document')
        .toBeNull();
    });
  });

  /**
   * Each guard is a chain of clauses, and the specs above only ever take the
   * FIRST one (wrong kind). The later clauses are the ones that stop a
   * double-submit or a write with missing data, so they are worth pinning in
   * their own right -- and until they are, Sonar reports the guards as
   * partially covered even though every LINE is executed, because new_coverage
   * counts conditions and not just lines.
   *
   * The approval service is stubbed as an empty object, so any of these
   * reaching past its guard would call a function that does not exist and
   * throw. not.toThrow() therefore asserts the guard held.
   */
  describe('later guard clauses (sweep #656)', () => {
    const approve = { kind: 'approve', row: { id: 'cr-1' } };

    it('blocks every guarded action while a request is in flight', () => {
      const c = create();
      c.probe.modal.set(approve);
      c.probe.outcome.set(1);
      c.probe.isBusy.set(true);
      expect(() => c.probe.confirmDate()).not.toThrow();
      expect(() => c.probe.resendConsent()).not.toThrow();
      expect(() => c.probe.confirmApprove()).not.toThrow();

      c.probe.modal.set({ kind: 'reject', row: { id: 'cr-1' } });
      c.probe.reason.set('needs a new date');
      expect(() => c.probe.confirmReject()).not.toThrow();
    });

    it('blocks when the row carries no id', () => {
      const c = create();
      c.probe.modal.set({ kind: 'approve', row: {} });
      c.probe.outcome.set(1);
      expect(() => c.probe.confirmDate()).not.toThrow();
      expect(() => c.probe.resendConsent()).not.toThrow();
      expect(() => c.probe.confirmApprove()).not.toThrow();
    });

    it('blocks approval until an outcome is chosen', () => {
      const c = create();
      c.probe.modal.set(approve);
      c.probe.outcome.set(null);
      expect(() => c.probe.confirmApprove()).not.toThrow();
    });

    it('blocks rejection on a blank or whitespace-only reason', () => {
      const c = create();
      c.probe.modal.set({ kind: 'reject', row: { id: 'cr-1' } });
      c.probe.reason.set('');
      expect(() => c.probe.confirmReject()).not.toThrow();
      // Whitespace is trimmed before the check, so it must not pass either.
      c.probe.reason.set('   ');
      expect(() => c.probe.confirmReject()).not.toThrow();
    });
  });
});
