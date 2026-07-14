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
