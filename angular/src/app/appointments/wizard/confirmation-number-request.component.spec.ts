import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, of, throwError } from 'rxjs';
import { ConfigStateService } from '@abp/ng.core';

import {
  ConfirmationNumberRequestComponent,
  numberRequestErrorState,
} from './confirmation-number-request.component';
import { ConfirmationNumberLookupsService } from '../../proxy/appointments/confirmation-number-lookups.service';

/**
 * 2026-11-06 (R3) -- "I don't know the confirmation number". Renders the real template against a stub
 * client and checks: nothing is sent while a box is empty, the four values go out trimmed with ABP's
 * error card switched off, the one answer names the booker's own login, the hourly limit and a failure
 * have their own messages, and "I have the number now" hands back to the number card. All data is
 * synthetic.
 */
describe('ConfirmationNumberRequestComponent', () => {
  const LOGIN = 'ada@example.test';
  let calls: { input: unknown; config: unknown }[];
  let reply: () => Observable<void>;

  function render(): ComponentFixture<ConfirmationNumberRequestComponent> {
    calls = [];
    TestBed.configureTestingModule({
      imports: [ConfirmationNumberRequestComponent],
      providers: [
        {
          provide: ConfirmationNumberLookupsService,
          useValue: {
            request: (input: unknown, config: unknown) => {
              calls.push({ input, config });
              return reply();
            },
          },
        },
        {
          provide: ConfigStateService,
          useValue: { getOne: (key: string) => (key === 'currentUser' ? { email: LOGIN } : null) },
        },
      ],
    });
    const fixture = TestBed.createComponent(ConfirmationNumberRequestComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: ComponentFixture<ConfirmationNumberRequestComponent>) =>
    f.nativeElement as HTMLElement;

  function type(
    f: ComponentFixture<ConfirmationNumberRequestComponent>,
    id: string,
    value: string,
  ) {
    const input = el(f).querySelector<HTMLInputElement>(id)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function fillAll(f: ComponentFixture<ConfirmationNumberRequestComponent>) {
    type(f, '#wiz-nr-first', '  Jane ');
    type(f, '#wiz-nr-last', ' Sample-Doe ');
    type(f, '#wiz-nr-dob', '1985-04-12');
    type(f, '#wiz-nr-claim', ' clm 123-45 ');
  }

  function send(f: ComponentFixture<ConfirmationNumberRequestComponent>) {
    el(f).querySelector('form')!.dispatchEvent(new Event('submit'));
    f.detectChanges();
  }

  beforeEach(() => (reply = () => of(undefined)));
  afterEach(() => TestBed.resetTestingModule());

  it('sends nothing and names every empty box', () => {
    const f = render();
    type(f, '#wiz-nr-first', 'Jane');
    send(f);

    expect(calls).toHaveSize(0);
    const errors = Array.from(el(f).querySelectorAll('.ra-err')).map((e) => e.textContent?.trim());
    expect(errors).toEqual([
      "Enter the patient's last name.",
      "Enter the patient's date of birth.",
      'Enter a claim number from the earlier evaluation.',
    ]);
    expect(el(f).querySelector('#wiz-nr-last')!.classList).toContain('bad');
  });

  it('treats a box of spaces as empty', () => {
    const f = render();
    fillAll(f);
    type(f, '#wiz-nr-claim', '   ');
    send(f);

    expect(calls).toHaveSize(0);
    expect(el(f).querySelectorAll('.ra-err')).toHaveSize(1);
  });

  it('sends the four values trimmed, with the error card off, and gives the one answer', () => {
    const f = render();
    fillAll(f);
    send(f);

    expect(calls).toHaveSize(1);
    expect(calls[0].input).toEqual({
      patientFirstName: 'Jane',
      patientLastName: 'Sample-Doe',
      patientDateOfBirth: '1985-04-12',
      claimNumber: 'clm 123-45',
    });
    expect(calls[0].config).toEqual({ skipHandleError: true });
    const sent = el(f).querySelector('#wiz-nr-sent');
    expect(sent?.textContent).toContain(LOGIN);
    expect(sent?.textContent).toContain('If these details match one earlier evaluation');
  });

  it('ignores a second submit while the first is on its way', () => {
    const pending = new Subject<void>();
    reply = () => pending.asObservable();
    const f = render();
    fillAll(f);
    send(f);
    send(f);

    expect(calls).toHaveSize(1);
    expect(el(f).querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBeTrue();
    pending.next();
    pending.complete();
    f.detectChanges();
    expect(el(f).querySelector('#wiz-nr-sent')).not.toBeNull();
  });

  it('says to try later when the hourly limit is reached', () => {
    reply = () => throwError(() => new HttpErrorResponse({ status: 429 }));
    const f = render();
    fillAll(f);
    send(f);

    expect(el(f).querySelector('#wiz-nr-throttled')).not.toBeNull();
    expect(el(f).querySelector('#wiz-nr-sent')).toBeNull();
  });

  it('says nothing was sent when the request fails', () => {
    reply = () => throwError(() => new HttpErrorResponse({ status: 500 }));
    const f = render();
    fillAll(f);
    send(f);

    expect(el(f).querySelector('#wiz-nr-failed')).not.toBeNull();
    expect(el(f).querySelector('#wiz-nr-sent')).toBeNull();
  });

  it('hands back to the number card once the number has arrived', () => {
    const f = render();
    let handedBack = 0;
    f.componentInstance.haveNumber.subscribe(() => handedBack++);
    fillAll(f);
    send(f);

    el(f).querySelector<HTMLButtonElement>('#wiz-nr-sent button')!.click();
    expect(handedBack).toBe(1);
  });

  it('maps only a 429 to the hourly-limit message', () => {
    expect(numberRequestErrorState(new HttpErrorResponse({ status: 429 }))).toBe('throttled');
    expect(numberRequestErrorState(new HttpErrorResponse({ status: 403 }))).toBe('failed');
    expect(numberRequestErrorState(new Error('offline'))).toBe('failed');
  });
});
