/* Your Global Scripts */

(function () {
  const registerPath = '/Account/Register';
  function resolveExternalSignupApiBaseUrl() {
    const fromWindow = typeof window.externalSignupApiBaseUrl === 'string' ? window.externalSignupApiBaseUrl.trim() : '';
    if (fromWindow) {
      return fromWindow;
    }

    const meta = document.querySelector('meta[name="external-signup-api-base-url"]');
    const fromMeta = meta && typeof meta.getAttribute === 'function' ? (meta.getAttribute('content') || '').trim() : '';
    if (fromMeta) {
      return fromMeta;
    }

    const fromStorage =
      window.localStorage && typeof window.localStorage.getItem === 'function'
        ? (window.localStorage.getItem('externalSignupApiBaseUrl') || '').trim()
        : '';
    if (fromStorage) {
      return fromStorage;
    }

    if (window.location.hostname === 'localhost' && window.location.port === '44368') {
      return window.location.protocol + '//' + window.location.hostname + ':44327';
    }

    return window.location.origin;
  }

  const externalSignupApiBaseUrl = resolveExternalSignupApiBaseUrl();
  const externalSignupEndpoint = new URL('/api/public/external-signup/register', externalSignupApiBaseUrl).toString();
  const debug = window.localStorage && window.localStorage.getItem('externalSignupDebug') === 'true';
  const externalUserRoles = [
    { value: 1, label: 'Patient' },
    { value: 2, label: 'Claim Examiner' },
    { value: 3, label: 'Applicant Attorney' },
    { value: 4, label: 'Defense Attorney' },
  ];
  let isSubmitting = false;
  let submitHookAttached = false;
  let clickHookAttached = false;
  let keydownHookAttached = false;
  let nativeSubmitPatched = false;

  function log() {
    if (!debug || typeof console === 'undefined' || typeof console.log !== 'function') {
      return;
    }

    const args = Array.prototype.slice.call(arguments);
    args.unshift('[ExternalSignup]');
    console.log.apply(console, args);
  }

  // W-B-1 (2026-04-30): inline error surface so the user sees registration
  // failures even when the browser auto-dismisses alert() (Playwright
  // headless, some browser hardening modes). Always render alongside alert.
  function showInlineRegisterError(form, message) {
    if (!form || !message) return;
    var existing = form.querySelector('#external-signup-error');
    if (!existing) {
      existing = document.createElement('div');
      existing.id = 'external-signup-error';
      existing.className = 'alert alert-danger mt-2';
      existing.setAttribute('role', 'alert');
      form.prepend(existing);
    }
    existing.textContent = message;
    existing.style.display = '';
  }

  function clearInlineRegisterError(form) {
    if (!form) return;
    var existing = form.querySelector('#external-signup-error');
    if (existing) {
      existing.style.display = 'none';
      existing.textContent = '';
    }
  }

  function notifyRegisterFailure(form, message) {
    showInlineRegisterError(form, message);
    try { alert(message); } catch (_e) { /* alert may be suppressed; inline error already shown */ }
  }

  function isRegisterPage() {
    return window.location.pathname.toLowerCase().startsWith(registerPath.toLowerCase());
  }

  function getFirstValue(form, selectors) {
    for (const selector of selectors) {
      const input = form.querySelector(selector);
      if (input && typeof input.value === 'string') {
        return input.value.trim();
      }
    }

    return '';
  }

  function getRegisterForm() {
    const forms = Array.from(document.querySelectorAll('form'));
    return forms.find(function (form) {
      return !!form.querySelector(
        '#input-user-name, input[name="Input.UserName"], input[name="Input.EmailAddress"], #input-email-address'
      );
    });
  }

  // Adrian (2026-04-30): the register form is intentionally minimal --
  // username, email, password, role. Names are NOT collected here; they are
  // gathered later (booking form's patient/AA section). Tenant is preselected
  // via the existing top-of-page "switch" link, which sets the __tenant
  // cookie on the AuthServer domain. The form does not contain a tenant
  // dropdown.
  function ensureUserTypeSelect(form) {
    let select = form.querySelector('#external-user-type');
    if (select) {
      return select;
    }

    const firstField = form.querySelector('.form-floating, .mb-3');
    const container = document.createElement('div');
    container.className = 'form-floating mb-2';

    select = document.createElement('select');
    select.id = 'external-user-type';
    select.name = 'ExternalUserType';
    select.className = 'form-control';

    externalUserRoles.forEach(function (role) {
      const option = document.createElement('option');
      option.value = String(role.value);
      option.textContent = role.label;
      select.appendChild(option);
    });

    const label = document.createElement('label');
    label.htmlFor = 'external-user-type';
    label.textContent = 'External User Role';

    container.appendChild(select);
    container.appendChild(label);

    if (firstField && firstField.parentNode) {
      firstField.parentNode.insertBefore(container, firstField);
    } else {
      form.prepend(container);
    }

    log('User type dropdown added.');
    return select;
  }

  // Read the AuthServer's `__tenant` cookie. ABP's tenant resolver writes this
  // cookie when the user clicks the top-of-page "switch" link and picks a
  // tenant. The cookie's value is the tenant id (GUID). Returns null if not
  // set or empty.
  function readTenantCookie() {
    var match = document.cookie.match(/(?:^|;\s*)__tenant=([^;]+)/);
    if (!match) return null;
    var value = decodeURIComponent(match[1]).trim();
    return value || null;
  }

  async function submitExternalSignup(form) {
    if (isSubmitting) {
      log('Submit ignored. Request is already in-flight.');
      return;
    }

    const select = ensureUserTypeSelect(form);
    const username = getFirstValue(form, [
      '#input-user-name',
      'input[name="Input.UserName"]',
      'input[name="Input.UserNameOrEmailAddress"]',
    ]);
    const email = getFirstValue(form, [
      '#input-email-address',
      'input[name="Input.EmailAddress"]',
      'input[type="email"]',
    ]);
    const password = getFirstValue(form, ['#password', 'input[name="Input.Password"]', 'input[type="password"]']);

    if (!username || !email || !password) {
      notifyRegisterFailure(form, 'Please fill username, email and password.');
      log('Validation failed before API call.', { usernamePresent: !!username, emailPresent: !!email, passwordPresent: !!password });
      return;
    }
    clearInlineRegisterError(form);

    const selectedRoleValue = Number((select && select.value) || 1);
    const userType = Number.isFinite(selectedRoleValue) && selectedRoleValue > 0 ? selectedRoleValue : 1;
    const tenantId = readTenantCookie();

    if (!tenantId) {
      notifyRegisterFailure(
        form,
        'Please select a tenant before registering. Use the "switch" link at the top of the page.'
      );
      log('Tenant cookie missing; user must switch to a tenant first.');
      return;
    }

    // Names are not collected on the register form. They are captured later
    // on the booking form's patient/AA/DA/CE section. Submit null so the
    // server stores nullable defaults rather than falsy fallbacks.
    const payload = {
      userType: userType,
      firstName: null,
      lastName: null,
      email: email,
      password: password,
      tenantId: tenantId,
    };

    isSubmitting = true;
    log('Calling signup endpoint:', externalSignupEndpoint, payload);

    try {
      const response = await fetch(externalSignupEndpoint, {
        method: 'POST',
        credentials: 'include',
        mode: 'cors',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      });

      log('Signup response status:', response.status);

      if (!response.ok) {
        let message = 'Registration failed.';
        try {
          const errorResult = await response.json();
          message =
            (errorResult && errorResult.error && errorResult.error.message) ||
            errorResult.message ||
            message;
          log('Signup error response body:', errorResult);
        } catch (_e) {
          try {
            const rawText = await response.text();
            if (rawText) {
              message = rawText;
            }
            log('Signup error raw response body:', rawText);
          } catch (_e2) {
            log('Signup error body parse failed.');
          }
        }

        notifyRegisterFailure(form, message);
        return;
      }

      // Do not forward stale OIDC query params after signup.
      // Reusing previous state/nonce can break Angular OAuth flow.
      const loginUrl = '/Account/Login';
      log('Signup success. Redirecting to:', loginUrl);
      window.location.assign(loginUrl);
    } catch (error) {
      log('Fetch threw error:', error);
      notifyRegisterFailure(form, 'Unable to register now. Please try again.');
    } finally {
      isSubmitting = false;
    }
  }

  function onDocumentSubmit(event) {
    if (!isRegisterPage()) {
      return;
    }

    const form = event.target && event.target.closest ? event.target.closest('form') : null;
    const registerForm = getRegisterForm();
    if (!form || !registerForm || form !== registerForm) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (typeof event.stopImmediatePropagation === 'function') {
      event.stopImmediatePropagation();
    }

    log('Register form submit intercepted.');
    submitExternalSignup(form);
  }

  function onDocumentClick(event) {
    if (!isRegisterPage()) {
      return;
    }

    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    // ABP's register page renders the Register button with type="button" and
    // wires submission through a JS click handler. Include #register so the
    // hijack catches it; fall back to the standard submit selectors for any
    // other variant.
    const submitButton = target.closest(
      'button[type="submit"], input[type="submit"], .register-btn, #register'
    );
    if (!submitButton) {
      return;
    }

    const form = submitButton.closest('form') || getRegisterForm();
    const registerForm = getRegisterForm();
    if (!form || !registerForm || form !== registerForm) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (typeof event.stopImmediatePropagation === 'function') {
      event.stopImmediatePropagation();
    }

    log('Register button click intercepted.');
    submitExternalSignup(form);
  }

  function onDocumentKeyDown(event) {
    if (!isRegisterPage()) {
      return;
    }

    if (event.key !== 'Enter') {
      return;
    }

    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const form = target.closest('form');
    const registerForm = getRegisterForm();
    if (!form || !registerForm || form !== registerForm) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (typeof event.stopImmediatePropagation === 'function') {
      event.stopImmediatePropagation();
    }

    log('Enter key submit intercepted.');
    submitExternalSignup(form);
  }

  function patchNativeFormSubmit() {
    if (nativeSubmitPatched || !window.HTMLFormElement || !window.HTMLFormElement.prototype) {
      return;
    }

    nativeSubmitPatched = true;

    const originalSubmit = window.HTMLFormElement.prototype.submit;
    const originalRequestSubmit = window.HTMLFormElement.prototype.requestSubmit;

    window.HTMLFormElement.prototype.submit = function () {
      try {
        if (isRegisterPage()) {
          const registerForm = getRegisterForm();
          if (registerForm && this === registerForm) {
            log('Native form.submit() intercepted.');
            submitExternalSignup(registerForm);
            return;
          }
        }
      } catch (e) {
        log('submit() patch fallback to original due to error.', e);
      }

      return originalSubmit.apply(this, arguments);
    };

    if (typeof originalRequestSubmit === 'function') {
      window.HTMLFormElement.prototype.requestSubmit = function () {
        try {
          if (isRegisterPage()) {
            const registerForm = getRegisterForm();
            if (registerForm && this === registerForm) {
              log('Native form.requestSubmit() intercepted.');
              submitExternalSignup(registerForm);
              return;
            }
          }
        } catch (e) {
          log('requestSubmit() patch fallback to original due to error.', e);
        }

        return originalRequestSubmit.apply(this, arguments);
      };
    }

    log('Native submit methods patched.');
  }

  function attachGlobalHooks() {
    // W-B-1 (2026-04-30): attach the capture-phase hooks unconditionally and
    // immediately on script load. The previous code attached them inside
    // init() only after getRegisterForm() returned truthy. If the form had not
    // rendered yet (or was found by a different selector after a layout
    // shift), the hooks never wired and a user click went through to the
    // stock ABP register handler -- which doesn't understand the custom
    // payload shape, silently no-ops, and re-renders the form blank. The
    // capture-phase hooks themselves are gated on isRegisterPage() and
    // getRegisterForm() defensively, so attaching early is harmless on other
    // pages.
    patchNativeFormSubmit();

    if (!submitHookAttached) {
      submitHookAttached = true;
      document.addEventListener('submit', onDocumentSubmit, true);
      log('Global submit capture hook attached.');
    }

    if (!clickHookAttached) {
      clickHookAttached = true;
      document.addEventListener('click', onDocumentClick, true);
      log('Global click capture hook attached.');
    }

    if (!keydownHookAttached) {
      keydownHookAttached = true;
      document.addEventListener('keydown', onDocumentKeyDown, true);
      log('Global keydown capture hook attached.');
    }
  }

  function init() {
    // Hooks are attached regardless; this only runs when on the register page
    // to inject the role (External User Role) dropdown into the form.
    if (!isRegisterPage()) {
      return;
    }

    const form = getRegisterForm();
    if (!form) {
      log('Register form not found yet.');
      return;
    }

    ensureUserTypeSelect(form);
  }

  attachGlobalHooks();

  log('Script loaded at path:', window.location.pathname);

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  // Keep trying briefly because account page can render form late.
  let retries = 0;
  const timer = setInterval(function () {
    retries += 1;
    init();
    if (retries >= 30) {
      clearInterval(timer);
      log('Retry window finished.');
    }
  }, 500);
})();
