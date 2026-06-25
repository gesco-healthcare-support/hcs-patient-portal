import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { InviteExternalUserComponent } from './invite-external-user.component';

/**
 * F-005 regression: the admin invite form exposes a Firm Name field only for
 * the attorney roles (Applicant Attorney = 3, Defense Attorney = 4) and carries
 * it in the payload; non-attorney roles never send a firm name.
 */
describe('InviteExternalUserComponent firm field (F-005)', () => {
  let fixture: ComponentFixture<InviteExternalUserComponent>;
  let component: InviteExternalUserComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InviteExternalUserComponent],
      providers: [
        provideRouter([]),
        { provide: RestService, useValue: {} },
        { provide: ToasterService, useValue: { success: () => {} } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(InviteExternalUserComponent);
    component = fixture.componentInstance;
  });

  it('shows the firm field for Applicant and Defense Attorney', () => {
    component.form.controls.userType.setValue(3);
    expect(component.isAttorneySelected).toBeTrue();
    component.form.controls.userType.setValue(4);
    expect(component.isAttorneySelected).toBeTrue();
  });

  it('hides the firm field for Patient and Claim Examiner', () => {
    component.form.controls.userType.setValue(1);
    expect(component.isAttorneySelected).toBeFalse();
    component.form.controls.userType.setValue(2);
    expect(component.isAttorneySelected).toBeFalse();
  });
});
