import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { SubmitQueryModalComponent } from './submit-query-modal.component';

/**
 * The patient's "ask the clinic a question" modal.
 *
 * Two properties carry most of the value. A FAILED submit must leave the modal OPEN with the
 * message intact -- the catch deliberately sets only isBusy, because closing would discard what
 * the patient typed and ABP's own handler has already shown them the error. And setVisible CLEARS
 * the fields on close but not on open, so reopening is a fresh form rather than a stale one.
 */
describe('SubmitQueryModalComponent', () => {
  let request: jasmine.Spy;
  let success: jasmine.Spy;

  /** Lets the component's `await firstValueFrom(...)` settle before assertions. */
  function settle(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
  }

  beforeEach(() => {
    request = jasmine.createSpy('request').and.returnValue(of(undefined));
    success = jasmine.createSpy('success');
    TestBed.configureTestingModule({
      imports: [SubmitQueryModalComponent],
      providers: [
        { provide: RestService, useValue: { request } },
        { provide: ToasterService, useValue: { success, error: () => undefined } },
      ],
    });
  });

  function make(): SubmitQueryModalComponent {
    return TestBed.createComponent(SubmitQueryModalComponent).componentInstance;
  }

  describe('canSubmit', () => {
    it('refuses an empty message', () => {
      expect(make().canSubmit).toBeFalse();
    });

    it('refuses a whitespace-only message', () => {
      const cmp = make();
      cmp.message = '    ';
      expect(cmp.canSubmit).toBeFalse();
    });

    it('refuses a message over the length cap', () => {
      const cmp = make();
      cmp.message = 'x'.repeat(cmp.maxMessageLength + 1);
      expect(cmp.canSubmit).toBeFalse();
    });

    it('accepts a message exactly at the cap', () => {
      // The boundary, asserted on the inclusive side: `<=` against `<` is the
      // single-character mistake this catches.
      const cmp = make();
      cmp.message = 'x'.repeat(cmp.maxMessageLength);
      expect(cmp.canSubmit).toBeTrue();
    });

    it('refuses while a submit is in flight', () => {
      const cmp = make();
      cmp.message = 'Is my appointment still booked?';
      cmp.isBusy = true;
      expect(cmp.canSubmit).toBeFalse();
    });

    // The positive control for all four refusals above.
    it('accepts an ordinary message', () => {
      const cmp = make();
      cmp.message = 'Is my appointment still booked?';
      expect(cmp.canSubmit).toBeTrue();
    });
  });

  describe('visibility', () => {
    it('emits the new visibility', () => {
      const cmp = make();
      const seen: boolean[] = [];
      cmp.visibleChange.subscribe((v) => seen.push(v));
      cmp.setVisible(true);
      cmp.setVisible(false);
      expect(seen).toEqual([true, false]);
    });

    it('clears the form when closing', () => {
      const cmp = make();
      cmp.message = 'typed';
      cmp.requestConfirmationNumber = 'RCN-1';
      cmp.isBusy = true;
      cmp.setVisible(false);
      expect(cmp.message).toBe('');
      expect(cmp.requestConfirmationNumber).toBe('');
      expect(cmp.isBusy).toBeFalse();
    });

    it('does NOT clear the form when opening', () => {
      // The complement. Without it, `if (!value)` could be dropped entirely and
      // the clearing test above would still pass.
      const cmp = make();
      cmp.message = 'typed';
      cmp.setVisible(true);
      expect(cmp.message).toBe('typed');
    });

    it('closes on Escape when open and idle', () => {
      const cmp = make();
      cmp.visible = true;
      cmp.onEscape();
      expect(cmp.visible).toBeFalse();
    });

    it('is inert on Escape when already closed', () => {
      const cmp = make();
      const seen: boolean[] = [];
      cmp.visibleChange.subscribe((v) => seen.push(v));
      cmp.visible = false;
      cmp.onEscape();
      expect(seen).toEqual([]);
    });

    it('refuses to close on Escape while a submit is in flight', () => {
      const cmp = make();
      cmp.visible = true;
      cmp.isBusy = true;
      cmp.onEscape();
      expect(cmp.visible).toBeTrue();
    });

    it('is wired to a real document Escape keypress, not just callable', () => {
      const cmp = make();
      cmp.visible = true;
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      expect(cmp.visible).toBeFalse();
    });
  });

  describe('submit', () => {
    it('does nothing when the message is not submittable', async () => {
      const cmp = make();
      await cmp.submit();
      expect(request).not.toHaveBeenCalled();
    });

    it('posts the trimmed message to the user-queries endpoint', async () => {
      const cmp = make();
      cmp.message = '  Is my appointment still booked?  ';
      await cmp.submit();
      const [config] = request.calls.mostRecent().args as [
        { method: string; url: string; body: { message: string } },
      ];
      expect(config.method).toBe('POST');
      expect(config.url).toBe('/api/app/user-queries');
      expect(config.body.message).toBe('Is my appointment still booked?');
    });

    it('sends null rather than an empty confirmation number', async () => {
      const cmp = make();
      cmp.message = 'A question';
      cmp.requestConfirmationNumber = '   ';
      await cmp.submit();
      const [config] = request.calls.mostRecent().args as [
        { body: { requestConfirmationNumber: string | null } },
      ];
      expect(config.body.requestConfirmationNumber).toBeNull();
    });

    it('sends a trimmed confirmation number when one is given', async () => {
      const cmp = make();
      cmp.message = 'A question';
      cmp.requestConfirmationNumber = '  RCN-1001  ';
      await cmp.submit();
      const [config] = request.calls.mostRecent().args as [
        { body: { requestConfirmationNumber: string | null } },
      ];
      expect(config.body.requestConfirmationNumber).toBe('RCN-1001');
    });

    it('confirms and closes on success', async () => {
      const cmp = make();
      cmp.visible = true;
      cmp.message = 'A question';
      await cmp.submit();
      expect(success).toHaveBeenCalledWith('Your query has been sent to the clinic team.');
      expect(cmp.visible).toBeFalse();
    });

    // THE ONE THAT MATTERS: a failure must not discard what was typed.
    it('leaves the modal open with the message intact when the send fails', async () => {
      request.and.returnValue(throwError(() => new Error('boom')));
      const cmp = make();
      cmp.visible = true;
      cmp.message = 'A question';
      await cmp.submit();
      expect(cmp.visible).toBeTrue();
      expect(cmp.message).toBe('A question');
      expect(cmp.isBusy).toBeFalse();
      expect(success).not.toHaveBeenCalled();
    });

    it('marks itself busy while the send is in flight', async () => {
      const cmp = make();
      cmp.message = 'A question';
      const inFlight = cmp.submit();
      expect(cmp.isBusy).toBeTrue();
      await inFlight;
      await settle();
    });
  });
});
