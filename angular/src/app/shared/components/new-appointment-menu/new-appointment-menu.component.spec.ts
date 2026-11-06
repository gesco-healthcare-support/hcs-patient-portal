import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  NewAppointmentMenuComponent,
  newAppointmentQueryParams,
} from './new-appointment-menu.component';

describe('NewAppointmentMenuComponent', () => {
  let fixture: ComponentFixture<NewAppointmentMenuComponent>;
  let navigate: jasmine.Spy;

  const el = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const trigger = (): HTMLButtonElement => el().querySelector('button[aria-controls]')!;

  beforeEach(() => {
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({
      imports: [NewAppointmentMenuComponent],
      providers: [{ provide: Router, useValue: { navigate } }],
    });
    fixture = TestBed.createComponent(NewAppointmentMenuComponent);
    fixture.detectChanges();
  });

  it('maps the two choices to the wizard mode key', () => {
    expect(newAppointmentQueryParams('new')).toEqual({ type: 1 });
    expect(newAppointmentQueryParams('reeval')).toEqual({ type: 2 });
  });

  it('opens two choices and navigates to type=2 for a re-evaluation', () => {
    expect(el().querySelector('#nam-choices')).toBeNull();
    expect(trigger().getAttribute('aria-expanded')).toBe('false');
    trigger().click();
    fixture.detectChanges();
    expect(trigger().getAttribute('aria-expanded')).toBe('true');
    expect(el().querySelectorAll('#nam-choices button')).toHaveSize(2);
    (el().querySelector('#nam-reeval') as HTMLButtonElement).click();
    expect(navigate).toHaveBeenCalledWith(['/appointments/request'], {
      queryParams: { type: 2 },
    });
  });

  it('navigates to type=1 for a new evaluation', () => {
    trigger().click();
    fixture.detectChanges();
    (el().querySelector('#nam-new') as HTMLButtonElement).click();
    expect(navigate).toHaveBeenCalledWith(['/appointments/request'], {
      queryParams: { type: 1 },
    });
  });

  it('is a disclosure, not an ARIA menu (no arrow-key promise)', () => {
    trigger().click();
    fixture.detectChanges();
    expect(el().querySelector('[role=menu], [role=menuitem]')).toBeNull();
    expect(trigger().hasAttribute('aria-haspopup')).toBeFalse();
  });

  it('closes on Escape and puts focus back on the trigger', () => {
    trigger().click();
    fixture.detectChanges();
    (el().querySelector('#nam-reeval') as HTMLButtonElement).focus();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(el().querySelector('#nam-choices')).toBeNull();
    expect(document.activeElement).toBe(trigger());
  });

  it('leaves focus alone on Escape when it is already closed', () => {
    const other = document.createElement('button');
    document.body.appendChild(other);
    other.focus();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(document.activeElement).toBe(other);
    other.remove();
  });
});
