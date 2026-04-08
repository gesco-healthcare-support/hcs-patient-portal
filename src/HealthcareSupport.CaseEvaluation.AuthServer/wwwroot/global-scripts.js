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
  const tenantOptionsEndpoint = new URL('/api/public/external-signup/tenant-options', externalSignupApiBaseUrl).toString();
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
  let tenantOptionsLoaded = false;
  let tenantOptions = [];

  function log() {
    if (!debug || typeof console === 'undefined' || typeof console.log !== 'function') {
      return;
    }

    const args = Array.prototype.slice.call(arguments);
    args.unshift('[ExternalSignup]');
    console.log.apply(console, args);
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

  function ensureTenantSelect(form) {
    let select = form.querySelector('#external-tenant-id');
    if (select) {
      return select;
    }

    const firstField = form.querySelector('.form-floating, .mb-3');
    const container = document.createElement('div');
    container.className = 'form-floating mb-2';

    select = document.createElement('select');
    select.id = 'external-tenant-id';
    select.name = 'ExternalTenantId';
    select.className = 'form-control';

    const defaultOption = document.createElement('option');
    defaultOption.value = '';
    defaultOption.textContent = 'Select Tenant';
    select.appendChild(defaultOption);

    const label = document.createElement('label');
    label.htmlFor = 'external-tenant-id';
    label.textContent = 'Tenant';

    container.appendChild(select);
    container.appendChild(label);

    if (firstField && firstField.parentNode) {
      firstField.parentNode.insertBefore(container, firstField);
    } else {
      form.prepend(container);
    }

    log('Tenant dropdown added.');
    return select;
  }

  function populateTenantSelect(form) {
    const select = ensureTenantSelect(form);
    while (select.options.length > 1) {
      select.remove(1);
    }

    tenantOptions.forEach(function (tenant) {
      const option = document.createElement('option');
      option.value = tenant.id;
      option.textContent = tenant.displayName;
      select.appendChild(option);
    });

    // Hide tenant select if current tenant is already resolved (live/subdomain mode).
    const container = select.closest('.form-floating, .mb-2');
    if (container) {
      container.style.display = tenantOptions.length > 0 ? '' : 'none';
    }

    log('Tenant options loaded. Count:', tenantOptions.length);
  }

  async function loadTenantOptions(form) {
    if (tenantOptionsLoaded) {
      populateTenantSelect(form);
      return;
    }

    tenantOptionsLoaded = true;
    try {
      const response = await fetch(tenantOptionsEndpoint, {
        method: 'GET',
        credentials: 'include',
        mode: 'cors',
      });

      if (!response.ok) {
        log('Tenant options request failed with status:', response.status);
        return;
      }

      const result = await response.json();
      tenantOptions = (result && Array.isArray(result.items) ? result.items : []).filter(function (x) {
        return x && x.id && x.displayName;
      });
      populateTenantSelect(form);
    } catch (error) {
      log('Tenant options fetch failed:', error);
    }
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
      alert('Please fill username, email and password.');
      log('Validation failed before API call.', { usernamePresent: !!username, emailPresent: !!email, passwordPresent: !!password });
      return;
    }

    const parts = username.split(/\s+/).filter(Boolean);
    const firstName = parts[0] || username;
    const lastName = parts.slice(1).join(' ') || 'User';
    const selectedRoleValue = Number((select && select.value) || 1);
    const userType = Number.isFinite(selectedRoleValue) && selectedRoleValue > 0 ? selectedRoleValue : 1;
    const tenantSelect = form.querySelector('#external-tenant-id');
    const tenantId = tenantSelect && tenantSelect.value ? tenantSelect.value : null;

    if (tenantOptions.length > 0 && !tenantId) {
      alert('Please select a tenant.');
      log('Tenant selection is required on host context.');
      return;
    }

    const payload = {
      userType: userType,
      firstName: firstName,
      lastName: lastName,
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

        alert(message);
        return;
      }

      // Do not forward stale OIDC query params after signup.
      // Reusing previous state/nonce can break Angular OAuth flow.
      const loginUrl = '/Account/Login';
      log('Signup success. Redirecting to:', loginUrl);
      window.location.assign(loginUrl);
    } catch (error) {
      log('Fetch threw error:', error);
      alert('Unable to register now. Please try again.');
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

    const submitButton = target.closest('button[type="submit"], input[type="submit"], .register-btn');
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

  function init() {
    if (!isRegisterPage()) {
      return;
    }

    const form = getRegisterForm();
    if (!form) {
      log('Register form not found yet.');
      return;
    }

    ensureUserTypeSelect(form);
    ensureTenantSelect(form);
    loadTenantOptions(form);
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
