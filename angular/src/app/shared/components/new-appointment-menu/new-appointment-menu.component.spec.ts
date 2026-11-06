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
  const trigger = (): HTMLButtonElement => el().querySelector('button[aria-haspopup]')!;

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

  it('opens a two-item menu and navigates to type=2 for a re-evaluation', () => {
    expect(el().querySelector('[role=menu]')).toBeNull();
    trigger().click();
    fixture.detectChanges();
    expect(el().querySelectorAll('[role=menuitem]').length).toBe(2);
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

  it('closes on Escape', () => {
    trigger().click();
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(el().querySelector('[role=menu]')).toBeNull();
  });
});
