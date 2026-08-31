import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import {
  PHONE_DIGIT_COUNT,
  PhoneNumberDirective,
  formatPartialUsPhone,
  phoneDigits,
} from './phone-number.directive';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, PhoneNumberDirective],
  template: `
    <input appPhoneNumber [formControl]="control" />
  `,
})
class HostComponent {
  readonly control = new FormControl<string | null>(null);
}

describe('phoneDigits / formatPartialUsPhone', () => {
  it('keeps only digits and stops at ten', () => {
    expect(phoneDigits('(213) 555-0134')).toBe('2135550134');
    expect(phoneDigits('213555013499')).toBe('2135550134');
    expect(phoneDigits('  +1 213 555 0134 ext 22 ')).toBe('1213555013');
    expect(phoneDigits(null)).toBe('');
  });

  it('formats progressively so a half-typed number still reads correctly', () => {
    expect(formatPartialUsPhone('')).toBe('');
    expect(formatPartialUsPhone('2')).toBe('(2');
    expect(formatPartialUsPhone('213')).toBe('(213');
    expect(formatPartialUsPhone('2135')).toBe('(213)-5');
    expect(formatPartialUsPhone('213555')).toBe('(213)-555');
    expect(formatPartialUsPhone('2135550')).toBe('(213)-555-0');
    expect(formatPartialUsPhone('2135550134')).toBe('(213)-555-0134');
  });

  it('is idempotent, so re-running on every keystroke is safe', () => {
    const once = formatPartialUsPhone('2135550134');
    expect(formatPartialUsPhone(once)).toBe(once);
  });

  it('never exceeds ten digits however many are supplied', () => {
    expect(phoneDigits('9'.repeat(30)).length).toBe(PHONE_DIGIT_COUNT);
  });
});

describe('PhoneNumberDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;
  let input: HTMLInputElement;

  function type(value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
    input = fixture.nativeElement.querySelector('input');
  });

  it('shows the formatted value but stores bare digits', () => {
    type('2135550134');

    expect(input.value).toBe('(213)-555-0134');
    expect(host.control.value).toBe('2135550134');
  });

  it('formats a stored value on the way in, whatever shape it was stored in', () => {
    host.control.setValue('(213) 555-0134');
    fixture.detectChanges();

    expect(input.value).toBe('(213)-555-0134');
  });

  it('stops hard at the tenth digit', () => {
    type('21355501349999');

    expect(input.value).toBe('(213)-555-0134');
    expect(host.control.value).toBe('2135550134');
  });

  it('stores null rather than an empty string when cleared', () => {
    type('2135550134');
    type('');

    expect(host.control.value).toBeNull();
  });

  it('reports the actual length while a number is incomplete', () => {
    type('213555');

    expect(host.control.valid).toBeFalse();
    expect(host.control.errors).toEqual({
      phoneNumber: { requiredLength: 10, actualLength: 6 },
    });
  });

  it('is valid when empty, so an optional field is not blocked', () => {
    type('');

    expect(host.control.valid).toBeTrue();
  });

  it('is valid on exactly ten digits', () => {
    type('2135550134');

    expect(host.control.valid).toBeTrue();
  });

  it('raises maxlength to fit the formatted value', () => {
    // Several of these fields shipped with maxlength="12", which truncates '(213)-555-0134'.
    expect(input.maxLength).toBe(14);
  });
});
