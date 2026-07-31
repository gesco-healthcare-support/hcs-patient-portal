import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { StateMessageComponent } from './state-message.component';

describe('StateMessageComponent', () => {
  let fixture: ComponentFixture<StateMessageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StateMessageComponent],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(StateMessageComponent);
  });

  function setInputs(inputs: Record<string, unknown>): void {
    for (const [key, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(key, value);
    }
    fixture.detectChanges();
  }

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the tone class, icon, title and lead', () => {
    setInputs({
      tone: 'amber',
      icon: 'clock',
      title: 'Your session has expired',
      lead: 'Please sign in again.',
    });

    expect(el().querySelector('.pp-ic')?.classList.contains('amber')).toBe(true);
    expect(el().querySelector('.pp-card h1')?.textContent).toContain('Your session has expired');
    expect(el().querySelector('.pp-card .lead')?.textContent).toContain('Please sign in again.');
    // The app-icon child renders an <svg> shell inside the badge.
    expect(el().querySelector('.pp-ic svg')).toBeTruthy();
  });

  it('centers a single action and invokes its click handler', () => {
    const spy = jasmine.createSpy('click');
    setInputs({
      icon: 'alert',
      title: 'Something went wrong',
      lead: 'Try again shortly.',
      actions: [{ label: 'Try again', icon: 'refresh', click: spy }],
    });

    expect(el().querySelector('.pp-actions')?.classList.contains('pp-actions--single')).toBe(true);
    const button = el().querySelector('button.ap-btn') as HTMLButtonElement;
    expect(button.textContent).toContain('Try again');

    button.click();
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('renders a routerLink action as an anchor', () => {
    setInputs({
      icon: 'search',
      title: 'Page not found',
      lead: 'This page does not exist.',
      actions: [{ label: 'Back to home', icon: 'home', routerLink: '/' }],
    });

    const anchor = el().querySelector('a.ap-btn') as HTMLAnchorElement;
    expect(anchor).toBeTruthy();
    expect(anchor.getAttribute('href')).toBe('/');
  });

  it('falls back to the generic support footer', () => {
    setInputs({ icon: 'alert', title: 'x', lead: 'y' });
    expect(el().querySelector('.pp-foot')?.textContent).toContain('Contact your clinic');
  });
});
