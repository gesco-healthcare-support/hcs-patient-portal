import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { UsDateAutoSlashDirective, formatPartialUsDate } from './us-date-auto-slash.directive';

@Component({
  standalone: true,
  imports: [UsDateAutoSlashDirective],
  template: `
    <input appUsDateAutoSlash />
  `,
})
class HostComponent {}

/**
 * The directive itself, on a real input. The formatter below is pure and pinned on its own; what
 * only the directive does is write the value back, park the caret at the end, and re-dispatch ONE
 * input event so the datepicker re-parses -- a re-dispatch that must stop after a single pass.
 */
describe('UsDateAutoSlashDirective', () => {
  let input: HTMLInputElement;
  let inputEvents: jasmine.Spy;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    input = fixture.nativeElement.querySelector('input');
    // Registered AFTER the directive's host listener, so it sees the re-dispatch first and the
    // original event second.
    inputEvents = jasmine.createSpy('input');
    input.addEventListener('input', inputEvents);
  });

  function type(value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  it('inserts the separators as the date is typed and parks the caret at the end', () => {
    type('06151985');

    expect(input.value).toBe('06/15/1985');
    expect(input.selectionStart).toBe(10);
    expect(input.selectionEnd).toBe(10);
  });

  it('re-dispatches exactly one input event so the datepicker re-parses, then stops', () => {
    type('0615');

    // The original event plus one re-dispatch; the second pass sees a formatted value and ends.
    expect(inputEvents).toHaveBeenCalledTimes(2);
  });

  it('leaves an already formatted value alone and re-dispatches nothing', () => {
    type('06/15');

    expect(input.value).toBe('06/15');
    expect(inputEvents).toHaveBeenCalledTimes(1);
  });
});

describe('formatPartialUsDate', () => {
  it('returns empty for empty input', () => {
    expect(formatPartialUsDate('')).toBe('');
  });

  it('inserts separators progressively as digits arrive', () => {
    expect(formatPartialUsDate('0')).toBe('0');
    expect(formatPartialUsDate('06')).toBe('06');
    expect(formatPartialUsDate('061')).toBe('06/1');
    expect(formatPartialUsDate('0615')).toBe('06/15');
    expect(formatPartialUsDate('06151')).toBe('06/15/1');
    expect(formatPartialUsDate('06151985')).toBe('06/15/1985');
  });

  it('truncates to 8 digits (MMDDYYYY)', () => {
    expect(formatPartialUsDate('0615198599')).toBe('06/15/1985');
  });

  it('is idempotent on an already-formatted value', () => {
    expect(formatPartialUsDate('06/15/1985')).toBe('06/15/1985');
  });

  it('strips non-digits (letters and stray separators) before regrouping', () => {
    expect(formatPartialUsDate('ab06cd15')).toBe('06/15');
    expect(formatPartialUsDate('06-15-1985')).toBe('06/15/1985');
    expect(formatPartialUsDate('06.15.1985')).toBe('06/15/1985');
  });

  it('drops a trailing separator when a group is deleted (backspace)', () => {
    expect(formatPartialUsDate('06/1')).toBe('06/1');
    expect(formatPartialUsDate('06/')).toBe('06');
    expect(formatPartialUsDate('06/15/')).toBe('06/15');
  });
});
