import { of, throwError } from 'rxjs';
import { EntityActionList, FormProp, FormPropList } from '@abp/ng.components/extensible';
import { ToasterService } from '@abp/ng.theme.shared';
import type { IdentityUserDto } from '@volo/abp.ng.identity/proxy';
import { AccountActivationService } from './account-activation.service';
import {
  IDENTITY_USER_ACTIVATION_OPTIONS,
  IDENTITY_USERS_COMPONENT_KEY,
  addResendActivationAction,
  ownerChoosesOnCreate,
  ownerChoosesOnEdit,
  resendActivationAction,
  unseenPassword,
} from './identity-user-activation.contributors';

function prop(name: string): FormProp<IdentityUserDto> {
  // 'string' is ePropType.String, a const enum that cannot be imported under isolated modules.
  return new FormProp<IdentityUserDto>({
    type: 'string' as FormProp['type'],
    name,
    displayName: name,
    validators: () => [() => ({ required: true })],
  });
}

function names(list: FormPropList<IdentityUserDto>): string[] {
  return list.toArray().map((p) => p.name);
}

describe('identity user activation contributors', () => {
  const stock = [
    'userName',
    'name',
    'password',
    'email',
    'emailConfirmed',
    'shouldChangePasswordOnNextLogin',
    'isActive',
  ];

  function stockList(): FormPropList<IdentityUserDto> {
    const list = new FormPropList<IdentityUserDto>();
    stock.forEach((n) => list.addTail(prop(n)));
    return list;
  }

  it('drops the confirmation and must-change fields from the create form', () => {
    const list = stockList();
    ownerChoosesOnCreate(list);
    expect(names(list)).not.toContain('emailConfirmed');
    expect(names(list)).not.toContain('shouldChangePasswordOnNextLogin');
  });

  it('keeps a password field for the framework contract, but hidden, unvalidated and pre-filled', () => {
    const list = stockList();
    ownerChoosesOnCreate(list);
    const password = list.toArray().find((p) => p.name === 'password');
    expect(password).toBeDefined();
    expect(password!.visible({} as never)).toBeFalse();
    expect(password!.validators({} as never)).toEqual([]);
    expect(String(password!.defaultValue).length).toBeGreaterThan(20);
  });

  it('drops only the confirmation box from the edit form', () => {
    const list = stockList();
    ownerChoosesOnEdit(list);
    expect(names(list)).toEqual(stock.filter((n) => n !== 'emailConfirmed'));
  });

  it('leaves a form without those fields untouched', () => {
    const list = new FormPropList<IdentityUserDto>();
    list.addTail(prop('userName'));
    ownerChoosesOnCreate(list);
    ownerChoosesOnEdit(list);
    expect(names(list)).toEqual(['userName']);
  });

  it('makes a password that differs every time and satisfies the policy', () => {
    const a = unseenPassword();
    const b = unseenPassword();
    expect(a).not.toEqual(b);
    expect(a).toMatch(/[A-Z]/);
    expect(a).toMatch(/[a-z]/);
    expect(a).toMatch(/[0-9]/);
    expect(a).toMatch(/[^A-Za-z0-9]/);
  });

  it('offers the resend action only for an active account that has not activated', () => {
    const data = (record: Partial<IdentityUserDto>) => ({ record }) as never;
    expect(
      resendActivationAction.visible(data({ emailConfirmed: false, isActive: true })),
    ).toBeTrue();
    expect(
      resendActivationAction.visible(data({ emailConfirmed: true, isActive: true })),
    ).toBeFalse();
    expect(
      resendActivationAction.visible(data({ emailConfirmed: false, isActive: false })),
    ).toBeFalse();
    expect(resendActivationAction.permission).toBe('AbpIdentity.Users.Update');
  });

  it('calls the resend endpoint for the row and reports success', () => {
    const service = jasmine.createSpyObj<AccountActivationService>('svc', ['resendToUser']);
    service.resendToUser.and.returnValue(of(undefined));
    const toaster = jasmine.createSpyObj<ToasterService>('toaster', ['success']);
    const injected = new Map<unknown, unknown>([
      [AccountActivationService, service],
      [ToasterService, toaster],
    ]);
    const data = {
      record: { id: 'u-1', email: 'person@example.test' },
      getInjected: (token: unknown) => injected.get(token),
    } as never;

    resendActivationAction.action(data);

    expect(service.resendToUser).toHaveBeenCalledOnceWith('u-1');
    expect(toaster.success).toHaveBeenCalledOnceWith(
      'Activation email sent to person@example.test.',
    );
  });

  it('shows no success toast when the server refuses', () => {
    const service = jasmine.createSpyObj<AccountActivationService>('svc', ['resendToUser']);
    service.resendToUser.and.returnValue(throwError(() => new Error('too soon')));
    const toaster = jasmine.createSpyObj<ToasterService>('toaster', ['success']);
    const injected = new Map<unknown, unknown>([
      [AccountActivationService, service],
      [ToasterService, toaster],
    ]);

    resendActivationAction.action({
      record: { id: 'u-1' },
      getInjected: (t: unknown) => injected.get(t),
    } as never);

    expect(toaster.success).not.toHaveBeenCalled();
  });

  it('registers all three contributors under the users component key', () => {
    const list = new EntityActionList<IdentityUserDto>();
    addResendActivationAction(list);
    expect(list.toArray()).toEqual([resendActivationAction]);

    const options = IDENTITY_USER_ACTIVATION_OPTIONS as Record<string, Record<string, unknown[]>>;
    expect(options['entityActionContributors'][IDENTITY_USERS_COMPONENT_KEY]).toEqual([
      addResendActivationAction,
    ]);
    expect(options['createFormPropContributors'][IDENTITY_USERS_COMPONENT_KEY]).toEqual([
      ownerChoosesOnCreate,
    ]);
    expect(options['editFormPropContributors'][IDENTITY_USERS_COMPONENT_KEY]).toEqual([
      ownerChoosesOnEdit,
    ]);
    expect(IDENTITY_USERS_COMPONENT_KEY).toBe('Identity.UsersComponent');
  });
});
