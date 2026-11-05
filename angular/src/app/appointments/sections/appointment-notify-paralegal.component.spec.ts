import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppointmentNotifyParalegalComponent } from './appointment-notify-paralegal.component';
import { EMPTY_NOTIFY_PARALEGAL, type NotifyParalegalState } from '../shared/notify-paralegal.util';

describe('AppointmentNotifyParalegalComponent', () => {
  let fixture: ComponentFixture<AppointmentNotifyParalegalComponent>;
  let emitted: NotifyParalegalState[];

  function setup(state: NotifyParalegalState, showErrors = false): HTMLElement {
    fixture = TestBed.createComponent(AppointmentNotifyParalegalComponent);
    emitted = [];
    fixture.componentInstance.state = state;
    fixture.componentInstance.showErrors = showErrors;
    fixture.componentInstance.idPrefix = 'applicant-paralegal';
    fixture.componentInstance.stateChange.subscribe((s) => emitted.push(s));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() =>
    TestBed.configureTestingModule({ imports: [AppointmentNotifyParalegalComponent] }),
  );

  it('shows only the checkbox until it is ticked', () => {
    const el = setup(EMPTY_NOTIFY_PARALEGAL);

    expect(el.textContent).toContain('Send all notifications to my paralegal instead of me');
    expect(el.querySelector('#applicant-paralegal-email')).toBeNull();
  });

  it('shows name and email fields once ticked, flagging the invalid ones after a blocked Continue', () => {
    const el = setup({ ...EMPTY_NOTIFY_PARALEGAL, enabled: true }, true);

    expect(el.querySelector('#applicant-paralegal-first.is-invalid')).not.toBeNull();
    expect(el.querySelector('#applicant-paralegal-email.is-invalid')).not.toBeNull();
  });

  it('does not flag errors before the booker tried to continue', () => {
    const el = setup({ ...EMPTY_NOTIFY_PARALEGAL, enabled: true }, false);

    expect(el.querySelector('.is-invalid')).toBeNull();
  });

  it('emits the new state when the checkbox is ticked', () => {
    const el = setup(EMPTY_NOTIFY_PARALEGAL);
    const box = el.querySelector<HTMLInputElement>('#applicant-paralegal-enabled')!;

    box.click();

    expect(emitted.length).toBe(1);
    expect(emitted[0].enabled).toBeTrue();
  });
});
