import { ToasterService } from '@abp/ng.theme.shared';
import {
  EntityAction,
  EntityActionList,
  FormProp,
  FormPropList,
} from '@abp/ng.components/extensible';
import type { IdentityConfigOptions } from '@volo/abp.ng.identity';
import type { IdentityUserDto } from '@volo/abp.ng.identity/proxy';
import { AccountActivationService } from '../proxy/external-account/account-activation.service';

/**
 * How an account made on the identity Users screen (office or host) is switched on, 2026-10-23.
 *
 * The person the account is for chooses their own password from an activation email, and choosing
 * it confirms their email. So the screen no longer offers the two fields that let whoever fills in
 * the form decide those things: no typed password and no "Email confirmed" box (a confirmed email
 * grants a party access to appointments, so a tick box on someone else's address would hand them
 * that access). The server enforces both regardless; this keeps the form honest about it. A row
 * action resends the activation email to an account that has not activated yet.
 *
 * The key is the string value of `eIdentityComponents.Users`, which is a `const enum` and cannot be
 * imported under isolated modules.
 */
export const IDENTITY_USERS_COMPONENT_KEY = 'Identity.UsersComponent';

/** Form fields the person filling in the form must not decide. */
const OWNER_DECIDES = ['emailConfirmed', 'shouldChangePasswordOnNextLogin'];

/**
 * A strong random value for the framework's required `password` field, which is never shown. The
 * server replaces whatever arrives with a value nobody sees, so this only has to satisfy the
 * create contract and the password policy.
 */
export function unseenPassword(): string {
  const bytes = new Uint8Array(24);
  crypto.getRandomValues(bytes);
  return 'Aa1!' + Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}

export const resendActivationAction = new EntityAction<IdentityUserDto>({
  text: 'Resend activation email',
  icon: 'fa fa-envelope',
  permission: 'AbpIdentity.Users.Update',
  visible: (data) =>
    !!data?.record && !data.record.emailConfirmed && data.record.isActive !== false,
  action: (data) => {
    const service = data.getInjected(AccountActivationService);
    const toaster = data.getInjected(ToasterService);
    service.resendToUser(data.record.id).subscribe({
      next: () =>
        toaster.success('Activation email sent to ' + (data.record.email ?? 'the user') + '.'),
      // A refusal (already active, switched off, sent a minute ago) is shown by the global
      // error handler with the server's message.
      error: () => undefined,
    });
  },
});

export function addResendActivationAction(list: EntityActionList<IdentityUserDto>): void {
  list.addTail(resendActivationAction);
}

function dropByName(
  list: FormPropList<IdentityUserDto>,
  name: string,
): FormProp<IdentityUserDto> | undefined {
  const node = list.find((listNode) => listNode.value.name === name);
  if (!node) {
    return undefined;
  }
  list.dropByValue(name, (prop, value) => prop.name === value);
  return node.value;
}

/** Create form: no typed password, no "Email confirmed", no "must change password". */
export function ownerChoosesOnCreate(list: FormPropList<IdentityUserDto>): void {
  OWNER_DECIDES.forEach((name) => dropByName(list, name));
  const password = dropByName(list, 'password');
  if (password) {
    list.addTail(
      new FormProp<IdentityUserDto>({
        ...password,
        visible: () => false,
        validators: () => [],
        asyncValidators: () => [],
        defaultValue: unseenPassword(),
      }),
    );
  }
}

/** Edit form: no "Email confirmed" (an edit never confirms; a changed address unconfirms). */
export function ownerChoosesOnEdit(list: FormPropList<IdentityUserDto>): void {
  dropByName(list, 'emailConfirmed');
}

export const IDENTITY_USER_ACTIVATION_OPTIONS: IdentityConfigOptions = {
  entityActionContributors: { [IDENTITY_USERS_COMPONENT_KEY]: [addResendActivationAction] },
  createFormPropContributors: { [IDENTITY_USERS_COMPONENT_KEY]: [ownerChoosesOnCreate] },
  editFormPropContributors: { [IDENTITY_USERS_COMPONENT_KEY]: [ownerChoosesOnEdit] },
} as IdentityConfigOptions;
