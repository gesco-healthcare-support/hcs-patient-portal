import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { OfficeLetterheadComponent } from './office-letterhead.component';
import { OfficeLetterheadDto, OfficeLetterheadService } from './office-letterhead.service';

/**
 * The packet-letterhead card on the in-office branding page (walkthrough Q5). Rendered, so the
 * placeholders are checked as a user sees them: a field left blank shows the default that will
 * print. All names synthetic.
 */
describe('OfficeLetterheadComponent', () => {
  let service: { get: jasmine.Spy; update: jasmine.Spy };
  let toaster: { success: jasmine.Spy };

  const stored = (over: Partial<OfficeLetterheadDto> = {}): OfficeLetterheadDto => ({
    defaultPhysicianName: 'Dr. TEST-Ada TEST-Example',
    defaultLetterheadName: 'Dr. TEST-Ada TEST-Example',
    defaultPracticeName: 'TEST Office',
    ...over,
  });

  function render(getResult = of(stored())) {
    service = {
      get: jasmine.createSpy('get').and.returnValue(getResult),
      update: jasmine.createSpy('update').and.callFake((input) => of(stored(input))),
    };
    toaster = { success: jasmine.createSpy('success') };
    TestBed.configureTestingModule({
      imports: [OfficeLetterheadComponent],
      providers: [
        { provide: OfficeLetterheadService, useValue: service },
        { provide: ToasterService, useValue: toaster },
      ],
    });
    const fixture = TestBed.createComponent(OfficeLetterheadComponent);
    fixture.detectChanges();
    return fixture;
  }

  const input = (root: HTMLElement, id: string) => root.querySelector<HTMLInputElement>(`#${id}`)!;
  const saveButton = (root: HTMLElement) =>
    Array.from(root.querySelectorAll<HTMLButtonElement>('button')).find((b) =>
      b.textContent!.includes('Save letterhead'),
    )!;

  it('shows the derived defaults as placeholders for a blank letterhead', async () => {
    const fixture = render();
    await fixture.whenStable();
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(input(root, 'ol-physician').placeholder).toBe('Dr. TEST-Ada TEST-Example');
    expect(input(root, 'ol-letterhead-name').placeholder).toBe('Dr. TEST-Ada TEST-Example');
    expect(input(root, 'ol-practice').placeholder).toBe('TEST Office');
    expect(root.querySelector('[data-testid="letterhead-preview"]')!.textContent).toContain(
      'Dr. TEST-Ada TEST-Example',
    );
  });

  it('labels every field so it has an accessible name', () => {
    const root = render().nativeElement as HTMLElement;
    const controls = root.querySelectorAll<HTMLElement>('input, textarea');
    expect(controls.length).toBe(13);
    controls.forEach((control) => {
      expect(root.querySelector(`label[for="${control.id}"]`))
        .withContext(control.id)
        .not.toBeNull();
    });
  });

  it('saves blanks as null and shows the saved values', () => {
    const fixture = render();
    const component = fixture.componentInstance as unknown as {
      form: { text: Record<string, string>; fee: string };
      save(): void;
    };
    component.form.text['practiceName'] = ' TEST Institute ';
    component.form.fee = '503.75';

    component.save();

    const sent = service.update.calls.mostRecent().args[0];
    expect(sent.practiceName).toBe('TEST Institute');
    expect(sent.phone).toBeNull();
    expect(sent.missedAppointmentFee).toBe(503.75);
    expect(toaster.success).toHaveBeenCalled();
    expect(component.form.text['practiceName']).toBe('TEST Institute');
  });

  it('blocks saving while the fee is not an amount', async () => {
    const fixture = render();
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;
    // Typed through the DOM: the component is OnPush, so the input event is what refreshes it.
    const fee = input(root, 'ol-fee');
    fee.value = 'abc';
    fee.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(saveButton(root).disabled).toBeTrue();
    expect(root.querySelector('[role="alert"]')).not.toBeNull();
    (fixture.componentInstance as unknown as { save(): void }).save();
    expect(service.update).not.toHaveBeenCalled();
  });

  it('stays usable when the letterhead cannot be loaded', () => {
    const fixture = render(throwError(() => ({ status: 500 })));
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('.ia-empty')).toBeNull();
    expect(input(root, 'ol-physician').placeholder).toBe('');
  });
});
