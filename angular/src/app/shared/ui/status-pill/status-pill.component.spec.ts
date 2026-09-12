import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatusPillComponent } from './status-pill.component';

describe('StatusPillComponent', () => {
  let fixture: ComponentFixture<StatusPillComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StatusPillComponent] }).compileComponents();
    fixture = TestBed.createComponent(StatusPillComponent);
  });

  function pill(): HTMLElement {
    return fixture.nativeElement.querySelector('.app-status-pill');
  }

  it('maps InfoRequested to the purple tone with the "Info Requested" label', () => {
    fixture.componentRef.setInput('status', 'InfoRequested');
    fixture.detectChanges();
    expect(pill().classList).toContain('app-status-pill--purple');
    expect(pill().textContent?.trim()).toBe('Info Requested');
  });

  it('maps Cancelled to the neutral (grey) tone -- not red', () => {
    fixture.componentRef.setInput('status', 'Cancelled');
    fixture.detectChanges();
    expect(pill().classList).toContain('app-status-pill--neutral');
    expect(pill().classList).not.toContain('app-status-pill--rejected');
  });

  it('maps Rescheduled to the info (blue) tone', () => {
    fixture.componentRef.setInput('status', 'Rescheduled');
    fixture.detectChanges();
    expect(pill().classList).toContain('app-status-pill--info');
  });

  // Phase 4c (2026-08-05): the two in-flight pills must render as IN PROGRESS. Reusing their
  // terminal pill's tone was the original defect -- info blue and neutral grey both read as done.
  it('renders RescheduleRequested as in-progress amber, never as the blue Rescheduled pill', () => {
    fixture.componentRef.setInput('status', 'RescheduleRequested');
    fixture.detectChanges();
    expect(pill().classList).toContain('app-status-pill--pending');
    expect(pill().classList).not.toContain('app-status-pill--info');
    expect(pill().textContent?.trim()).toBe('Reschedule Requested');
  });

  it('renders CancellationRequested as in-progress amber, never as the grey Cancelled pill', () => {
    fixture.componentRef.setInput('status', 'CancellationRequested');
    fixture.detectChanges();
    expect(pill().classList).toContain('app-status-pill--pending');
    expect(pill().classList).not.toContain('app-status-pill--neutral');
    expect(pill().textContent?.trim()).toBe('Cancellation Requested');
  });

  // Phase 5 (2026-08-07): the business's own long-standing names, verbatim. Adrian:
  // "I do not want to invent new names, these are long used names throughout the
  // business". Neutral grey rather than 4c's amber -- these ARE settled outcomes.
  it('renders NoShow with the business label and the neutral tone', () => {
    fixture.componentRef.setInput('status', 'NoShow');
    fixture.detectChanges();
    expect(pill().classList).toContain('app-status-pill--neutral');
    expect(pill().textContent?.trim()).toBe('No Show');
  });

  it('renders NotSeen with the business label and the neutral tone', () => {
    fixture.componentRef.setInput('status', 'NotSeen');
    fixture.detectChanges();
    expect(pill().classList).toContain('app-status-pill--neutral');
    expect(pill().textContent?.trim()).toBe('Not Seen');
  });

  it('does not label either attendance outcome "Cancelled"', () => {
    for (const status of ['NoShow', 'NotSeen']) {
      fixture.componentRef.setInput('status', status);
      fixture.detectChanges();
      expect(pill().textContent?.trim()).not.toBe('Cancelled');
    }
  });

  it('always renders a dot and text (never color-alone)', () => {
    fixture.componentRef.setInput('status', 'Approved');
    fixture.detectChanges();
    expect(pill().querySelector('.app-status-pill__dot')).toBeTruthy();
    expect(pill().textContent?.trim()).toBe('Approved');
  });

  it('lets a caller override the label', () => {
    fixture.componentRef.setInput('status', 'Rescheduled');
    fixture.componentRef.setInput('label', 'Rescheduled (pending confirmation)');
    fixture.detectChanges();
    expect(pill().classList).toContain('app-status-pill--info');
    expect(pill().textContent?.trim()).toBe('Rescheduled (pending confirmation)');
  });
});
