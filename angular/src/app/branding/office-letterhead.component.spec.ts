import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { OfficeLetterheadComponent } from './office-letterhead.component';
import type { OfficeLetterheadDto } from '../proxy/branding/models';
import { OfficeLetterheadService } from '../proxy/branding/office-letterhead.service';

/**
 * The packet-letterhead card on the in-office branding page (walkthrough Q5). Rendered, so the
 * placeholders are checked as a user sees them: a field left blank shows the default that will
 * print. All names synthetic.
 */
describe('OfficeLetterheadComponent', () => {
  let service: { get: jasmine.Spy; update: jasmine.Spy };
  let rest: { request: jasmine.Spy };
  let toaster: { success: jasmine.Spy };

  const stored = (over: Partial<OfficeLetterheadDto> = {}): OfficeLetterheadDto => ({
    defaultPhysicianName: 'Dr. TEST-Ada TEST-Example',
    defaultLetterheadName: 'Dr. TEST-Ada TEST-Example',
    defaultPracticeName: 'TEST Office',
    ...over,
  });

  function render(getResult = of(stored()), officeId?: string) {
    service = {
      get: jasmine.createSpy('get').and.returnValue(getResult),
      update: jasmine.createSpy('update').and.callFake((input) => of(stored(input))),
    };
    rest = {
      request: jasmine
        .createSpy('request')
        .and.callFake((req: { body?: Partial<OfficeLetterheadDto> }) => of(stored(req.body ?? {}))),
    };
    toaster = { success: jasmine.createSpy('success') };
    TestBed.configureTestingModule({
      imports: [OfficeLetterheadComponent],
      providers: [
        { provide: OfficeLetterheadService, useValue: service },
        { provide: RestService, useValue: rest },
        { provide: ToasterService, useValue: toaster },
      ],
    });
    const fixture = TestBed.createComponent(OfficeLetterheadComponent);
    if (officeId) {
      fixture.componentRef.setInput('officeId', officeId);
    }
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
    expect(rest.request).not.toHaveBeenCalled();
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

  describe('on the host branding page (an office id)', () => {
    const OFFICE = 'a1b2c3d4-0000-4000-9000-0000000000aa';

    it('reads and saves that office through the host route, never the current-office one', () => {
      const fixture = render(of(stored()), OFFICE);
      const component = fixture.componentInstance as unknown as {
        form: { text: Record<string, string>; fee: string };
        save(): void;
      };

      const read = rest.request.calls.mostRecent().args;
      expect(read[0]).toEqual({
        method: 'GET',
        url: `/api/app/branding/offices/${OFFICE}/letterhead`,
      });
      expect(read[1]).toEqual({ apiName: 'Default' });
      expect(service.get).not.toHaveBeenCalled();

      component.form.text['practiceName'] = 'TEST Host Practice';
      component.save();

      const write = rest.request.calls.mostRecent().args[0];
      expect(write.method).toBe('PUT');
      expect(write.url).toBe(`/api/app/branding/offices/${OFFICE}/letterhead`);
      expect(write.body.practiceName).toBe('TEST Host Practice');
      expect(service.update).not.toHaveBeenCalled();
      expect(component.form.text['practiceName']).toBe('TEST Host Practice');
    });

    it('loads the other office when the page switches offices on the same card', () => {
      const fixture = render(of(stored()), OFFICE);
      expect(rest.request).toHaveBeenCalledTimes(1);

      fixture.componentRef.setInput('officeId', 'b2c3d4e5-0000-4000-9000-0000000000bb');
      fixture.detectChanges();

      expect(rest.request).toHaveBeenCalledTimes(2);
      expect(rest.request.calls.mostRecent().args[0].url).toBe(
        '/api/app/branding/offices/b2c3d4e5-0000-4000-9000-0000000000bb/letterhead',
      );
    });
  });

  it('stays usable when the letterhead cannot be loaded', () => {
    const fixture = render(throwError(() => ({ status: 500 })));
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('.ia-empty')).toBeNull();
    expect(input(root, 'ol-physician').placeholder).toBe('');
  });
});
