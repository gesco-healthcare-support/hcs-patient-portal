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

    // AuthServer is on :44368; the external-signup API lives on the API host
    // (:44327). Preserve the subdomain so tenant resolution stays correct on
    // subdomain URLs like falkinstein.localhost:44368.
    if (window.location.port === '44368') {
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
    // OLD parity (P:\PatientPortalOld\.../user-add.component.html:14): "User Type"
    label.textContent = 'User Type';

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

  // OLD parity (P:\PatientPortalOld\.../user-add.component.html:23-32 +
  // 41-48): the register form has First Name + Last Name (two columns) and
  // Confirm Password fields that ABP's stock cshtml does not render. Inject
  // them client-side so the form layout, validation, and payload all line
  // up with OLD without forking the entire .cshtml page.
  //
  // ensureTextInput inserts a labeled text input AFTER the given anchor
  // node's parent (so e.g. an input next to the user-type select).
  // ensurePasswordInput is the same shape but type="password".
  function ensureTextInput(form, opts) {
    var existing = form.querySelector('#' + opts.id);
    if (existing) return existing;
    var container = document.createElement('div');
    container.className = 'form-floating mb-2';
    var input = document.createElement('input');
    input.type = opts.type || 'text';
    input.id = opts.id;
    input.name = opts.name;
    input.className = 'form-control';
    input.placeholder = opts.label;
    if (opts.autocomplete) input.setAttribute('autocomplete', opts.autocomplete);
    if (opts.maxLength) input.setAttribute('maxlength', String(opts.maxLength));
    var lab = document.createElement('label');
    lab.htmlFor = opts.id;
    lab.textContent = opts.label;
    container.appendChild(input);
    container.appendChild(lab);
    if (opts.afterContainer && opts.afterContainer.parentNode) {
      opts.afterContainer.parentNode.insertBefore(container, opts.afterContainer.nextSibling);
    } else {
      form.appendChild(container);
    }
    return input;
  }

  function ensureExtraRegisterFields(form) {
    // Anchor: the user-type select's container (always first after our
    // ensureUserTypeSelect inserts it). FirstName + LastName go right after
    // it so the visual order matches OLD: User Type \xb7 First Name \xb7 Last Name \xb7
    // [user name \xb7 email \xb7 password \xb7 confirm password].
    var selectContainer = form.querySelector('#external-user-type');
    selectContainer = selectContainer ? selectContainer.closest('.form-floating, .mb-2, .mb-3') : null;

    var lastNameInput = ensureTextInput(form, {
      id: 'external-last-name',
      name: 'LastName',
      label: 'Last Name',
      autocomplete: 'family-name',
      maxLength: 50,
      afterContainer: selectContainer,
    });

    var firstNameInput = ensureTextInput(form, {
      id: 'external-first-name',
      name: 'FirstName',
      label: 'First Name',
      autocomplete: 'given-name',
      maxLength: 50,
      afterContainer: selectContainer,
    });

    // OLD parity: Firm Name input shown for the two attorney roles
    // (Applicant Attorney = 3, Defense Attorney = 4). Hidden for the
    // other roles. Backend ValidateRegistrationInput requires FirmName
    // non-empty for attorney roles; submitting an attorney without a
    // Firm Name would throw BusinessException.
    var firmNameInput = ensureTextInput(form, {
      id: 'external-firm-name',
      name: 'FirmName',
      label: 'Firm Name',
      autocomplete: 'organization',
      maxLength: 50,
      afterContainer: selectContainer,
    });

    function applyRoleVisibility() {
      // OLD parity (PatientAppointment.DbEntities/Models/User.cs:64-85):
      // every role -- Patient, Adjuster (Claim Examiner), Patient Attorney
      // (Applicant Attorney), Defense Attorney -- has FirstName + LastName
      // columns and the OLD register form collects them for everyone. Only
      // FirmName is attorney-only. Earlier NEW behavior hid First/Last for
      // attorneys, leaving IdentityUser.Name + Surname null and breaking
      // the welcome banner, the booking-form pre-fill, and the attorney
      // lookup labels (all of which read those two fields).
      var select = form.querySelector('#external-user-type');
      var role = select ? Number(select.value || 1) : 1;
      var isAttorney = role === 3 || role === 4;
      var firstWrap = firstNameInput.closest('.form-floating, .mb-2, .mb-3');
      var lastWrap = lastNameInput.closest('.form-floating, .mb-2, .mb-3');
      var firmWrap = firmNameInput.closest('.form-floating, .mb-2, .mb-3');
      if (firstWrap) firstWrap.style.display = '';
      if (lastWrap) lastWrap.style.display = '';
      if (firmWrap) firmWrap.style.display = isAttorney ? '' : 'none';
    }

    var roleSelect = form.querySelector('#external-user-type');
    if (roleSelect && !roleSelect.dataset.roleVisibilityHook) {
      roleSelect.dataset.roleVisibilityHook = 'true';
      roleSelect.addEventListener('change', applyRoleVisibility);
    }
    applyRoleVisibility();

    // Confirm Password input after the existing password field. ABP renders
    // password as #password (LeptonX) or input[type="password"]. Anchor on
    // whichever is present.
    var passwordInput = form.querySelector('#password, input[name="Input.Password"], input[type="password"]');
    var passwordContainer = passwordInput
      ? passwordInput.closest('.form-floating, .mb-2, .mb-3, .input-group')
      : null;
    if (passwordContainer && !form.querySelector('#external-confirm-password')) {
      ensureTextInput(form, {
        id: 'external-confirm-password',
        name: 'ConfirmPassword',
        label: 'Confirm Password',
        type: 'password',
        autocomplete: 'new-password',
        afterContainer: passwordContainer,
      });
    }

    log('First/Last/Confirm Password injected.');
    return { firstNameInput: firstNameInput, lastNameInput: lastNameInput };
  }

  // OLD parity (P:\PatientPortalOld\.../user-add.component.html:50-53):
  // T&C paragraph below the submit button. Idempotent.
  function ensureTermsBlock(form) {
    if (form.querySelector('#external-signup-terms')) return;
    var btn = form.querySelector('button[type="submit"], #register, .register-btn');
    var anchor = btn ? (btn.closest('.form-floating, .mb-3, .mb-2') || btn) : null;
    var div = document.createElement('div');
    div.id = 'external-signup-terms';
    div.className = 'small text-muted mt-3';
    div.innerHTML =
      'By clicking "Sign Up", you agree to our '
      + '<a href="#" target="_blank" rel="noopener">terms of service and privacy policy</a>'
      + '. We’ll occasionally send you account related emails.';
    if (anchor && anchor.parentNode) {
      anchor.parentNode.insertBefore(div, anchor.nextSibling);
    } else {
      form.appendChild(div);
    }
  }

  // OLD parity: OLD's register form has only Email -- no separate User name.
  // ABP's stock cshtml renders Input.UserName as a required field. Our custom
  // submit endpoint (/api/public/external-signup/register) doesn't accept a
  // username (it derives it server-side from the email). Hiding the visible
  // field + dropping the client-side validation requirement is enough; the
  // hidden input still sits in the form for ABP's PageModel binding (which
  // we never reach, since the submit hooks short-circuit to fetch).
  function hideUserNameField(form) {
    var input = form.querySelector('#input-user-name, input[name="Input.UserName"]');
    if (!input) return;
    var container = input.closest('.form-floating, .mb-3, .mb-2');
    if (container) container.style.display = 'none';
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

  // 1.6 / W-REG-4 (2026-04-30, revised 2026-05-06): tenant resolution priority
  // on /Account/Register.
  //   1. ?__tenant=<TenantName> query string (highest -- invite links carry it)
  //   2. URL subdomain `<slug>.localhost` (mirrors server's DomainTenantResolver)
  //   3. __tenant cookie (set by login or prior tenant-switch)
  //   4. Nothing -> register is blocked with an inline error.
  // The query string and subdomain carry the tenant NAME; the cookie carries
  // the tenant ID (GUID). We need the GUID for the API call, so when only the
  // name is in hand we resolve via the public `resolve-tenant` endpoint
  // (case-insensitive exact match on Tenant.Name).
  function readTenantFromQuery() {
    try {
      var params = new URLSearchParams(window.location.search);
      var raw = params.get('__tenant');
      if (raw) {
        var trimmed = raw.trim();
        if (trimmed) return trimmed;
      }
      return null;
    } catch (_e) {
      return null;
    }
  }

  function readQueryParam(name) {
    try {
      var params = new URLSearchParams(window.location.search);
      var raw = params.get(name);
      return raw ? raw.trim() : null;
    } catch (_e) {
      return null;
    }
  }

  // ADR-006 -- mirror server-side DomainTenantResolveContributor on the client.
  // The server resolves tenant from the leftmost Host-header label (e.g.
  // `falkinstein.localhost` -> Falkinstein tenant). Without this helper, a user
  // who navigates directly to `http://<slug>.localhost:44368/Account/Register`
  // sees the tenant in the rendered page but the submit JS rejects with
  // "Tenant required" because it only inspects ?__tenant= and the cookie.
  // Returns the slug or null for bare `localhost`, IPs, and reserved labels.
  function readTenantFromSubdomain() {
    try {
      var host = window.location.hostname;
      if (!host) return null;
      // IPv4 / IPv6 -- no slug to extract.
      if (/^\d{1,3}(\.\d{1,3}){3}$/.test(host)) return null;
      if (host.indexOf(':') !== -1) return null;
      var parts = host.split('.');
      if (parts.length < 2) return null; // e.g. bare `localhost`
      var slug = parts[0].trim();
      if (!slug) return null;
      var reserved = ['www', 'localhost'];
      if (reserved.indexOf(slug.toLowerCase()) !== -1) return null;
      return slug;
    } catch (_e) {
      return null;
    }
  }

  // Resolves a tenant NAME to its GUID via the dedicated
  // /api/public/external-signup/resolve-tenant endpoint, which always runs in
  // host context (unlike GetTenantOptionsAsync which bails when the caller's
  // tenant cookie is set). Returns null on miss (HTTP 404).
  async function resolveTenantIdByName(tenantName) {
    if (!tenantName) return null;
    var lookupBase = externalSignupApiBaseUrl;
    var lookupUrl = new URL('/api/public/external-signup/resolve-tenant', lookupBase);
    lookupUrl.searchParams.set('name', tenantName);
    try {
      var response = await fetch(lookupUrl.toString(), {
        method: 'GET',
        credentials: 'include',
        mode: 'cors',
      });
      if (response.status === 404) {
        log('Tenant not found:', tenantName);
        return null;
      }
      if (!response.ok) {
        log('Tenant lookup failed:', response.status);
        return null;
      }
      var body = await response.json();
      return body && body.id ? { id: body.id, name: body.displayName } : null;
    } catch (e) {
      log('Tenant lookup error:', e);
      return null;
    }
  }

  // Cached tenant context for the page: { id, name }. Resolved once on init
  // (page load on /Account/Register) and reused on form submit so the Submit
  // path does not hit the network again.
  var tenantContextCache = null;

  async function resolveTenantContext() {
    if (tenantContextCache) return tenantContextCache;

    var tenantNameFromQuery = readTenantFromQuery();
    if (tenantNameFromQuery) {
      var resolved = await resolveTenantIdByName(tenantNameFromQuery);
      if (resolved) {
        tenantContextCache = resolved;
        return tenantContextCache;
      }
      // Query had a name but no tenant matched -- fall through to "blocked".
      // We deliberately do NOT fall back to the cookie in this case; the
      // invite link's intent overrides.
      tenantContextCache = { id: null, name: tenantNameFromQuery, invalid: true };
      return tenantContextCache;
    }

    // URL subdomain is as authoritative as ?__tenant= -- the server's
    // DomainTenantResolveContributor uses the same source. A stale cookie
    // from a different tenant must not override the current URL's slug.
    var tenantNameFromSubdomain = readTenantFromSubdomain();
    if (tenantNameFromSubdomain) {
      var resolvedSub = await resolveTenantIdByName(tenantNameFromSubdomain);
      if (resolvedSub) {
        tenantContextCache = resolvedSub;
        return tenantContextCache;
      }
      // Slug present but no tenant matched -- block (do not fall back to
      // cookie; the URL's intent must not be silently overridden).
      tenantContextCache = { id: null, name: tenantNameFromSubdomain, invalid: true };
      return tenantContextCache;
    }

    var cookieValue = readTenantCookie();
    if (cookieValue) {
      tenantContextCache = { id: cookieValue, name: null };
      return tenantContextCache;
    }

    tenantContextCache = null;
    return null;
  }

  function ensureExternalRegisterBanner(form, message, level) {
    var existing = form.querySelector('#external-register-banner');
    if (!existing) {
      existing = document.createElement('div');
      existing.id = 'external-register-banner';
      existing.setAttribute('role', 'note');
      form.prepend(existing);
    }
    var levelClass = level === 'danger' ? 'alert alert-danger' : 'alert alert-info';
    existing.className = levelClass + ' mt-2';
    existing.textContent = message;
    existing.style.display = '';
  }

  function setRegisterFormDisabled(form, disabled, hideForm) {
    var inputs = form.querySelectorAll('input, select, button, textarea');
    inputs.forEach(function (el) {
      // Leave the banner element alone (it has no name attribute and is at
      // the top), and the role-select dropdown stays interactive in the
      // success path; we only disable when blocking.
      if (disabled) {
        el.setAttribute('disabled', 'disabled');
      } else {
        el.removeAttribute('disabled');
      }
    });
    if (hideForm) {
      form.style.opacity = '0.4';
      form.style.pointerEvents = 'none';
    } else {
      form.style.opacity = '';
      form.style.pointerEvents = '';
    }
  }

  /**
   * Replace the register form's contents with a success banner + a manual
   * Sign In link. Called after the API register endpoint returns 204.
   * The user must click Sign In to continue -- no auto-redirect (matches
   * Adrian's "manual login" preference, Option B 2026-05-06). The Sign In
   * link pre-fills the username via ?username=<email> for convenience.
   */
  function showSignupSuccess(form, email) {
    var safeEmail = String(email || '').replace(/[<>&"']/g, function (ch) {
      switch (ch) {
        case '<': return '&lt;';
        case '>': return '&gt;';
        case '&': return '&amp;';
        case '"': return '&quot;';
        default: return '&#39;';
      }
    });
    var loginUrl = '/Account/Login?username=' + encodeURIComponent(email || '');
    form.innerHTML =
      '<div class="alert alert-success" role="status" style="margin-bottom:1rem;">' +
        '<strong>Account created.</strong>' +
        '<div style="margin-top:0.25rem;">' +
          'We created your account at <strong>' + safeEmail + '</strong>. ' +
          'Sign in below to verify your email and finish setup.' +
        '</div>' +
      '</div>' +
      '<a href="' + loginUrl + '" class="btn btn-primary" style="width:100%;">Sign In</a>';
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
    // ABP's LeptonX register form names the confirm-password field
    // Input.ConfirmPassword. Fall back to the second password-type input.
    // The server's ValidateRegistrationInput requires this field to be non-empty
    // and equal to password (OLD UserDomain.cs:88 parity).
    const allPasswordInputs = Array.from(form.querySelectorAll('input[type="password"]'));
    const confirmPassword = getFirstValue(form, ['input[name="Input.ConfirmPassword"]'])
      || (allPasswordInputs.length >= 2 ? (allPasswordInputs[1].value || '').trim() : '')
      || password;

    // OLD parity 2026-05-06: Username field is hidden and not part of the
    // submit payload (server derives username from email). Validate only
    // email + password.
    if (!email || !password) {
      notifyRegisterFailure(form, 'Please fill email and password.');
      log('Validation failed before API call.', { emailPresent: !!email, passwordPresent: !!password });
      return;
    }
    clearInlineRegisterError(form);

    const selectedRoleValue = Number((select && select.value) || 1);
    const userType = Number.isFinite(selectedRoleValue) && selectedRoleValue > 0 ? selectedRoleValue : 1;

    // 1.6 (2026-04-30): tenant resolution priority is query > cookie. Cached
    // on init() so this is a fast path; null means we never resolved one.
    const ctx = await resolveTenantContext();
    if (!ctx || !ctx.id || ctx.invalid) {
      notifyRegisterFailure(
        form,
        'Tenant required. Use your practice\'s portal link (the email you received) to register.'
      );
      log('Tenant context missing or invalid; submit blocked.', ctx);
      return;
    }
    const tenantId = ctx.id;

    // OLD parity (PatientAppointment.DbEntities/Models/User.cs:64-85):
    // First Name + Last Name are collected for every role; Firm Name is
    // additional for the two attorney roles (Applicant Attorney = 3,
    // Defense Attorney = 4). Always send First / Last so the
    // IdentityUser.Name / Surname columns never end up null -- the
    // welcome banner, booking-form pre-fill, and attorney lookup all
    // read those two fields.
    const firstName = getFirstValue(form, ['#external-first-name', 'input[name="FirstName"]']);
    const lastName = getFirstValue(form, ['#external-last-name', 'input[name="LastName"]']);
    const firmName = getFirstValue(form, ['#external-firm-name', 'input[name="FirmName"]']);
    const isAttorney = userType === 3 || userType === 4;

    const payload = {
      userType: userType,
      firstName: firstName || null,
      lastName: lastName || null,
      firmName: isAttorney ? (firmName || null) : null,
      email: email,
      password: password,
      confirmPassword: confirmPassword,
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

      // 2026-05-06 (Adrian, Option B): keep the user on the register page
      // and show an in-place success banner with a manual "Sign In" link,
      // instead of silently redirecting to /Account/Login. Reason: the
      // register POST returns 204 with no body, so without a banner the
      // user has no confirmation that anything happened. After clicking
      // Sign In, ABP's login flow detects EmailConfirmed=false and routes
      // to /Account/ConfirmUser, where the Verify button calls
      // /api/account/send-email-confirmation-token -> our
      // CaseEvaluationAccountEmailer override fires the email.
      log('Signup success. Showing in-place success banner.');
      showSignupSuccess(form, email);
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

  // 1.6 (2026-04-30): pre-fill role select from `?role=` query param. Accepts
  // either the label ("Patient", "Applicant Attorney") or the numeric enum
  // value ("1".."4"). Idempotent -- if the role-select cannot be located the
  // call is a no-op.
  function applyRolePrefill(select) {
    if (!select) return;
    var raw = readQueryParam('role');
    if (!raw) return;
    var resolvedValue = null;
    var asNumber = Number(raw);
    if (Number.isFinite(asNumber) && asNumber > 0) {
      var byNumber = externalUserRoles.find(function (r) { return r.value === asNumber; });
      if (byNumber) resolvedValue = byNumber.value;
    }
    if (!resolvedValue) {
      var byLabel = externalUserRoles.find(function (r) {
        return r.label.toLowerCase() === raw.toLowerCase();
      });
      if (byLabel) resolvedValue = byLabel.value;
    }
    if (!resolvedValue) {
      // Reject internal-role attempts silently. The dropdown only contains
      // external roles; an attacker pasting `?role=admin` cannot register as
      // admin because the role-select payload is overwritten on submit.
      log('applyRolePrefill: role param did not match an external role:', raw);
      return;
    }
    select.value = String(resolvedValue);
  }

  // 1.6 (2026-04-30): pre-fill email + username from `?email=` query param.
  // Username and email use the same value (the existing register form treats
  // username = email per S-1.3). Leaves both fields editable.
  function applyEmailPrefill(form) {
    var raw = readQueryParam('email');
    if (!raw) return;
    var emailFields = ['#input-email-address', 'input[name="Input.EmailAddress"]', 'input[type="email"]'];
    var usernameFields = ['#input-user-name', 'input[name="Input.UserName"]', 'input[name="Input.UserNameOrEmailAddress"]'];
    [emailFields, usernameFields].forEach(function (selectors) {
      for (var i = 0; i < selectors.length; i++) {
        var input = form.querySelector(selectors[i]);
        if (input && !input.value) {
          input.value = raw;
          break;
        }
      }
    });
  }

  async function applyTenantBanner(form) {
    var ctx = await resolveTenantContext();
    if (!ctx) {
      ensureExternalRegisterBanner(
        form,
        'Tenant required. To register, use the link from the email or page that brought you here. Each practice has its own portal link.',
        'danger');
      setRegisterFormDisabled(form, true, true);
      return;
    }
    if (ctx.invalid) {
      ensureExternalRegisterBanner(
        form,
        'The practice "' + ctx.name + '" was not found. Please use the original portal link from the email you received.',
        'danger');
      setRegisterFormDisabled(form, true, true);
      return;
    }
    // 2026-05-06 (Adrian): the informational "Registering for {practice}.
    // To register at a different practice, use that practice's portal link."
    // banner is removed -- the practice context is already obvious from the
    // subdomain in the URL bar, and OLD's register page has no equivalent
    // affordance. The danger-level banners above (tenant missing / invalid)
    // remain because they convey blocker state, not informational chrome.
    setRegisterFormDisabled(form, false, false);
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

    var select = ensureUserTypeSelect(form);
    ensureExtraRegisterFields(form);
    ensureTermsBlock(form);
    hideUserNameField(form);

    // 1.6 (2026-04-30): tenant banner + email/role pre-fill from query string.
    // Run after the role dropdown is in the DOM so applyRolePrefill can find
    // it. Banner application is async (network-bound) so we fire-and-forget;
    // submit is gated independently via resolveTenantContext().
    applyTenantBanner(form);
    applyEmailPrefill(form);
    applyRolePrefill(select);
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
