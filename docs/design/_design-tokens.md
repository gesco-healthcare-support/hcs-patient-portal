---
type: design-tokens
status: draft
audited: 2026-05-04
old-source-roots:
  - P:\PatientPortalOld\patientappointment-portal\src\index.html
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\bootstrap\bootstrap.min.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\vendor\app.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\site.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\
  - P:\PatientPortalOld\patientappointment-portal\src\assets\theme\css\demo.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\theme\css\demo-falkinstein.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\theme\css\demo-longacre.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\theme\css\demo-pelton.css
cross-reference:
  - docs/parity/_branding.md  # full brand-token surface + per-feature touchpoints (file not yet created)
strict-parity: true
---

# Design tokens -- OLD-anchored visual contract

> Purpose: Single source of truth for the runtime CSS-variable contract every Phase 1 feature consumes. Audience: frontend engineer. Last verified: 2026-06-01 vs main.

Single source of truth for the **runtime CSS-variable contract** every Phase 1
feature consumes. Captures OLD's literal color, typography, spacing,
border-radius, shadow, breakpoint, and motion values so per-feature design
docs can refer to one canonical set rather than re-derive them per page.

**Strict-parity directive:** internal pages keep OLD's exact visual
contract. External-user-facing pages parameterize the **brand layer**
(primary color, logo, clinic name, support contact) per
`docs/parity/_branding.md` Section A (file not yet created); everything else stays verbatim.

> Read order: this doc -> `_shell-layout.md` -> `_components.md` ->
> per-feature `<feature>-design.md`. Per-feature docs cite tokens by
> name (`--brand-primary`, `--space-md`) and only deviate from the
> contract here with an explicit "strict-parity exception" note.

## Tooling baseline (OLD)

| Layer | Version | OLD source |
|---|---|---|
| CSS framework | Bootstrap **4.0.0** | `assets/css/bootstrap/bootstrap.min.css` line 2 (`v4.0.0`) |
| Theme | "Lighthouse" (Material+Bootstrap hybrid) -- 3 per-doctor palette overrides | `assets/theme/css/demo.css` (1580 lines) + `demo-falkinstein.css` (372) + `demo-longacre.css` (395) + `demo-pelton.css` (364) |
| Body font | Roboto via Google Fonts (300/400/500/700/900 + italics) | `index.html`:10 |
| Icon set | Ionicons (`ion ion-md-*`) + Font Awesome (`fas fa-*`) | shell components, e.g. `top-bar.component.html`:75 (Ionicons), `side-bar.component.html`:7 (FA) |
| Control library | In-house "rx-control-design" mimicking Bootstrap 4 / jQuery UI | `assets/css/rx-control-design/` (10 files, 1843 LOC) |
| Login-page CSS | Hand-rolled vendor stylesheet | `assets/css/vendor/app.css` (77 lines) |

OLD shipped 3 per-doctor theme files (Falkinstein, Longacre, Pelton) that
diverge in <40 declarations each -- mostly minor color tweaks and
spacing adjustments per clinic. The base palette is consistent. NEW
parameterizes the few diverging values via `--brand-*` CSS variables;
the rest is shared.

## Color tokens

### Primary brand (per-tenant)

These three vary across OLD's three theme files. NEW exposes them as
`--brand-*` runtime variables so the same Angular bundle re-themes per
tenant without rebuild.

| Token | OLD value(s) | OLD source | NEW CSS var (deployed in `_brand.scss`) |
|---|---|---|---|
| Primary | `#06519f` (deep blue) -- all 3 themes (spec); deployed as `#055495` | `demo-falkinstein.css`:40-41, `demo-longacre.css`:42-43, `demo-pelton.css`:41-42 | `--brand-primary: #055495` |
| Primary hover | `#005494` | `demo-falkinstein.css`:323, 327, 331 | `--brand-primary-hover: #005494` |
| Accent | `#9dc13b` | form-field icons (OLD `styles.css`) | `--brand-accent: #9dc13b` |
| Active button state | `#006ffc` | active button OLD source | `--brand-button-active: #006ffc` |
| Inline link / edit | `#00a2dc` | OLD edit link color | `--brand-link: #00a2dc` |
| Error / delete | `#fe5a5b` | OLD delete action color | `--brand-error: #fe5a5b` |
| Primary focus shadow | `rgba(0, 84, 148, 0.3)` (inset) | `demo-falkinstein.css`:359-361 | spec only / not built (`--brand-primary-focus-shadow` not in `_brand.scss`) |
| Primary focus border | `rgba(0, 84, 148, 0.5)` | `demo-falkinstein.css`:359 | spec only / not built (`--brand-primary-focus-border` not in `_brand.scss`) |

Note on primary value: the OLD theme files use `#06519f`; `_brand.scss` (deployed)
uses `#055495` (a slightly darker value from `P:\PatientPortalOld\patientappointment-portal\src\styles.css`
verified 2026-05-03). The spec value `#06519f` was never deployed. `--brand-primary-text`
was renamed `--brand-primary-hover` in the implementation.

### Status / semantic colors (shared across all OLD themes)

| Token | OLD value | OLD source | NEW CSS var (deployed in `_brand.scss`) |
|---|---|---|---|
| Success | `#51a351` | `rx-control-design/toast.css`:66 | spec only / not built (`--color-success` not in `_brand.scss`) |
| Danger | `#bd362f` (toast) / `#e6514a` (button) | `toast.css`:95, `demo-falkinstein.css`:309 | spec only / not built (`--color-danger`, `--color-danger-button` not in `_brand.scss`) |
| Warning | `#f89406` | `toast.css`:103 | spec only / not built (`--color-warning` not in `_brand.scss`) |
| Info | `#2f96b4` | `toast.css`:110 | spec only / not built (`--color-info` not in `_brand.scss`) |

These four OLD values are accurate and must be implemented when toast/dialog components
are built. The planned CSS variable names (`--color-success` etc.) have not yet been added
to `_brand.scss`. Per-feature design docs SHOULD cite them as `--color-*` for Phase 19b
implementation; they are not yet available as runtime variables.

### Sidebar colors (deployed in `_brand.scss`)

| Token | Deployed value | Usage | NEW CSS var |
|---|---|---|---|
| Sidebar bg level 0 | `#0a4778` | Sidenav background | `--brand-sidebar-bg` |
| Sidebar bg level 1 | `#0a3354` | Sidenav nested level 1 | `--brand-sidebar-bg-2` |
| Sidebar bg level 2 | `#0b3a60` | Sidenav nested level 2 | `--brand-sidebar-bg-3` |

### Neutral grayscale (shared across all OLD themes)

Deployed names in `_brand.scss` differ from the original spec names. Both columns shown.

| Token | OLD value | Usage in OLD | Spec CSS var (not built) | Deployed CSS var (`_brand.scss`) |
|---|---|---|---|---|
| Body text / primary text | `#333` | `dialog.css`:41 (modal body) | `--text-body` | `--brand-text-primary: #333333` |
| Heading / dialog header | `#414c55` | `dialog.css`:7 | `--text-heading` | spec only / not built |
| Muted / secondary text | `#666` | `demo-falkinstein.css`:193, 306 | `--text-muted` | `--brand-text-secondary: #666666` |
| Disabled / placeholder text | `#999` | `vendor/app.css`:51, `datepicker.css`:27, 90 | `--text-disabled` | `--brand-text-muted: #969696` (nearest deployed; value differs) |
| Inactive nav text | `#a3a4a6` | `demo-falkinstein.css`:316, 320 | `--text-inactive` | spec only / not built |
| Code-snippet inline | `#c7254e` | `demo-falkinstein.css`:189 | `--text-code` | spec only / not built |
| Border / divider light | `#eee` | `demo-falkinstein.css`:274, 334-336, 338 | `--border-light` | `--brand-divider: #eeeeee` |
| Form input background | n/a (OLD inline) | form fields | n/a | `--brand-input-bg: #f4f4f4` |
| Form input border | `#ccc` (Bootstrap 4 default) | form fields | n/a | `--brand-input-border: #cccccc` |
| Background body | `#fff` | universal | `--bg-body` | spec only / not built |
| Background sidenav (light layout) | `#fff` | implied via theme `default-style` | `--bg-sidenav` | spec only / not built (sidebar uses `--brand-sidebar-bg` dark color) |
| Background card | `#fff` | `demo-falkinstein.css`:267, 303 | `--bg-card` | spec only / not built |
| Background hover light | `#f7f7f7` | `vendor/app.css`:7 (login form) | `--bg-hover-light` | spec only / not built |

### Accent secondaries (shared across all OLD themes -- NOT per-tenant)

These three appear in feature tiles / dashboard cards. NEW keeps them
shared. They are spec only / not built in `_brand.scss` (Phase 19b target).

| Token | OLD value | Usage | Spec CSS var (not built) |
|---|---|---|---|
| Slate | `#607d8b` | dashboard tile bg, `demo-falkinstein.css`:211, 300 | `--color-slate` |
| Teal | `#3ca99e` | dashboard tile bg, `demo-falkinstein.css`:218 | `--color-teal` |
| Gray | `#9e9e9e` | dashboard tile bg, `demo-falkinstein.css`:225 | `--color-gray` |

### Overlay / scrim

These are spec only / not built in `_brand.scss` (Phase 19b target).

| Token | OLD value | Usage | Spec CSS var (not built) |
|---|---|---|---|
| Modal scrim | `rgba(0,0,0,.5)` | `dialog.css`:13 | `--scrim-modal` |
| Image overlay (login) | `rgba(0,0,0,.25)` (`bg-dark opacity-25`) | `login.component.html`:3 | `--scrim-image` |
| Subtle inset (code blocks) | `rgba(0,0,0,.04)` | `demo-falkinstein.css`:186, 188 | `--scrim-inset` |

## Typography tokens

Deployed names in `_brand.scss` use `--brand-font-*` prefixes. Spec names (`--font-family-base`,
`--fw-*`, `--fs-*`, `--lh-*`) are not in `_brand.scss` and are spec only / not built.

| Token | OLD value | OLD source | Spec CSS var (not built) | Deployed CSS var (`_brand.scss`) |
|---|---|---|---|---|
| Font family base | `Roboto, "Segoe UI", "Lucida Grande", Verdana, Arial, Helvetica, sans-serif` | `index.html`:10 + `dialog.css`:8 | `--font-family-base` | `--brand-font-family: 'Roboto', 'Helvetica Neue', Arial, sans-serif` |
| Font weight light | `300` | Roboto link | `--fw-light` | spec only / not built |
| Font weight normal | `400` | Roboto link | `--fw-normal` | spec only / not built |
| Font weight medium | `500` | Roboto link, `vendor/app.css`:46 (sidebar nav-link) | `--fw-medium` | spec only / not built |
| Font weight bold | `700` | Roboto link, `app.css`:24 (.btn) | `--fw-bold` | spec only / not built |
| Font weight black | `900` | Roboto link | `--fw-black` | spec only / not built |
| Font size xs | `12px` | OLD form labels (small) | n/a | `--brand-font-size-xs: 12px` |
| Font size sm | `13px` | `dialog.css`:23 (dialog header) | `--fs-sm` / `--fs-dialog-header` | `--brand-font-size-sm: 13px` |
| Font size base | `14px` (labels and body) | OLD body copy | `--fs-base` (spec was 1rem/16px) | `--brand-font-size-base: 14px` |
| Font size md | `16px` | form fields (most pages) | n/a | `--brand-font-size-md: 16px` |
| Font size lg | `18px` | login form fields | `--fs-toast-title` (18px toast title shared) | `--brand-font-size-lg: 18px` |
| Font size button (login) | `22px` | primary login button OLD | `--fs-button-login` (spec was 15px -- value differs) | `--brand-font-size-button: 22px` |
| Font size socal-label | `.894rem` | `demo-falkinstein.css`:46 | `--fs-label` | spec only / not built |
| Font size app-brand-text | `1.1rem` | `demo-falkinstein.css`:71 | `--fs-app-brand` | spec only / not built |
| Font size dialog title | `20px/24px` | `dialog.css`:8 | `--fs-dialog-title`, `--lh-dialog-title` | spec only / not built |
| Font size toast message | `14px` / 18px title | `toast.css`:166 | `--fs-toast`, `--fs-toast-title` | spec only / not built |
| Line height tight | `1.2` (Bootstrap 4 default) | -- | `--lh-tight` | spec only / not built |
| Line height base | `1.5` (Bootstrap 4 default) | -- | `--lh-base` | spec only / not built |
| Login button line-height | `40px` | `demo-falkinstein.css`:20 | `--lh-button-pull-right` | spec only / not built |

OLD references the system fallback chain `'Segoe UI','Lucida Grande',Verdana`
inside `dialog.css`:8 only -- the rest of the app inherits the body's
Roboto. The deployed `--brand-font-family` uses a shorter fallback chain
(`'Helvetica Neue', Arial, sans-serif`) which matches Phase 19a scope.
Phase 19b should add the full dialog fallback chain if needed.

## Spacing tokens

OLD uses Bootstrap 4's `rem` scale for most components plus a handful of
custom values. These are spec only / not built in `_brand.scss` (Phase 19b target).
Use Bootstrap 4 utility classes or inline values until they are added.

Deployed layout dimensions (in `_brand.scss`): `--brand-sidebar-width: 282px`,
`--brand-sidebar-width-collapsed: 70px`, `--brand-navbar-height: 86px`.

| Token | OLD value | OLD source | Spec CSS var (not built) |
|---|---|---|---|
| Space xxs | `.25rem` (4px) | `demo-falkinstein.css`:128 | `--space-xxs` |
| Space xs | `.375rem` (6px) | `demo-falkinstein.css`:154 | `--space-xs` |
| Space sm | `.5rem` (8px) | `demo-falkinstein.css`:123 | `--space-sm` |
| Space md | `.9375rem` (15px) | `demo-falkinstein.css`:143 | `--space-md` |
| Space base | `1rem` (16px) | Bootstrap 4 default | `--space` |
| Space lg | `1.125rem` (18px) | `demo-falkinstein.css`:43 | `--space-lg` |
| Space xl | `1.875rem` (30px) | `demo-falkinstein.css`:138 | `--space-xl` |
| Space xxl | `5rem` (80px) | `demo-falkinstein.css`:148 | `--space-xxl` |
| Login form padding | `30px` | `vendor/app.css`:9 | `--space-login-form` |
| Container padding-y | `container-p-y` (Bootstrap 4 utility ~1.5rem) | `start/app.component.html`:14, 51 | `--space-container-y` |
| Toast padding | `18px 20px` | `toast.css`:137 | `--space-toast-y`, `--space-toast-x` |
| Card padding | `30px` | `demo-falkinstein.css`:264 | `--space-card` |

## Border-radius tokens

These are spec only / not built in `_brand.scss` (Phase 19b target).

| Token | OLD value | Usage | Spec CSS var (not built) |
|---|---|---|---|
| Radius sm | `2px` | form-control + btn (`vendor/app.css`:18), layout-example-block (`demo-falkinstein.css`:187) | `--radius-sm` |
| Radius md | `3px` | toast-card (`toast.css`:139), alertbox (`dialog.css`:36) | `--radius-md` |
| Radius lg | `5px` | socal-label (`demo-falkinstein.css`:44) | `--radius-lg` |
| Radius pill | `50%` | app-brand-logo (`demo-falkinstein.css`:56), avatar | `--radius-pill` |

OLD's button + form-control radius is `2px` -- noticeably tighter than
Bootstrap 4's default `0.25rem`. NEW must preserve OLD's value via
`--radius-sm` once Phase 19b adds it.

## Box-shadow tokens

These are spec only / not built in `_brand.scss` (Phase 19b target).

| Token | OLD value | Usage | Spec CSS var (not built) |
|---|---|---|---|
| Shadow login form | `0px 2px 2px 2px rgba(0, 0, 0, 0.3)` | `vendor/app.css`:8 | `--shadow-login-form` |
| Shadow modal | `0 4px 23px 5px rgba(0,0,0,.2), 0 2px 6px rgba(0,0,0,.15)` | `dialog.css`:40 | `--shadow-modal` |
| Shadow toast | `0 0 12px #999` | `toast.css`:142-143 | `--shadow-toast` |
| Shadow toast-card-soft | `0 4px 8px 0 rgba(0,0,0,0.2), 0 6px 20px 0 rgba(0,0,0,0.19)` (commented-out alt) | `toast.css`:115 (commented) | `--shadow-toast-soft` (deferred -- not in OLD live) |
| Shadow sidenav inset | `inset -1px 0 0 rgba(0,0,0,.1)` | `vendor/app.css`:32 | `--shadow-sidenav-inset` |
| Shadow navbar-brand inset | `inset -1px 0 0 rgba(0,0,0,.25)` | `vendor/app.css`:69 | `--shadow-navbar-brand-inset` |
| Shadow input focus | `0 1px 2px 0 rgba(0,84,148,0.3) inset` | `demo-falkinstein.css`:360-361 | `--shadow-input-focus` |

## Breakpoints (Bootstrap 4 -- shared)

OLD does not override Bootstrap 4's media-query map. NEW preserves the
exact values; per-feature docs cite breakpoint names. Note: `--bp-*` CSS
variables are spec only / not built in `_brand.scss` -- use SCSS `$breakpoint-*`
variables or Angular CDK breakpoints until Phase 19b adds them.

| Token | OLD value | Spec CSS var (not built) |
|---|---|---|
| Breakpoint sm | `576px` | `--bp-sm` |
| Breakpoint md | `768px` | `--bp-md` |
| Breakpoint lg | `992px` | `--bp-lg` |
| Breakpoint xl | `1200px` | `--bp-xl` |

OLD's responsive shell hides the side-nav below `lg` (sidenav-toggle
collapses); see `demo-falkinstein.css`:94-99 (`@media (min-width: 992px)`).

## Z-index tokens

These are spec only / not built in `_brand.scss` (Phase 19b target).

| Token | OLD value | Usage | Spec CSS var (not built) |
|---|---|---|---|
| z dialog | `9999` | `dialog.css`:24 | `--z-dialog` |
| z sidebar (in `vendor/app.css` shell) | `100` | `vendor/app.css`:31 | `--z-sidebar` |
| z toast | implied above dialog | -- | `--z-toast` (planned: `10000`) |
| z popup | implied below dialog | `popup.css` | `--z-popup` (planned: `9000`) |

## Motion tokens

These are spec only / not built in `_brand.scss` (Phase 19b target).

| Token | OLD value | Usage | Spec CSS var (not built) |
|---|---|---|---|
| Transition modal scale | `all 0.5s ease-in-out` | `dialog.css`:63 | `--motion-modal` |
| Transition opacity | `200ms opacity` | `dialog.css`:12 | `--motion-opacity` |
| Animation toast fade | `0.5s` (commented; OLD inactive) | `toast.css`:68-69 (commented) | `--motion-toast-fade` (deferred) |

## NEW implementation contract (`angular/src/styles/_brand.scss`)

`angular/src/styles/_brand.scss` is the runtime carrier (Phase 19a, verified 2026-06-01 vs main).
The block below is the ACTUAL deployed content. Variables in the "spec only / not built"
column of the tables above (e.g. `--color-success`, `--text-body`, `--fw-light`, `--space-*`,
`--radius-*`, `--shadow-*`, `--z-*`, `--motion-*`) are NOT in this file and will be added
in Phase 19b.

```scss
// Brand tokens -- ground truth from OLD Patient Portal at P:\PatientPortalOld
//
// Phase 19a (CSS variables + typography only).
// Phase 19b will map these onto LeptonX's --lpx-* variables and customize the
// AuthServer login layout.

:root {
  // -- Color palette (verbatim OLD hex values) ---------------------------------
  --brand-primary: #055495;           // links, primary actions, hover-active
  --brand-primary-hover: #005494;     // primary button hover
  --brand-accent: #9dc13b;            // form-field icons, success-ish accent
  --brand-button-active: #006ffc;     // active button state
  --brand-link: #00a2dc;              // edit / inline link color
  --brand-error: #fe5a5b;             // delete / error color

  --brand-sidebar-bg: #0a4778;        // sidenav background (level 0)
  --brand-sidebar-bg-2: #0a3354;      // sidenav nested level 1
  --brand-sidebar-bg-3: #0b3a60;      // sidenav nested level 2

  // -- Form control surfaces --------------------------------------------------
  --brand-input-bg: #f4f4f4;
  --brand-input-border: #cccccc;

  // -- Typography colors ------------------------------------------------------
  --brand-text-primary: #333333;
  --brand-text-secondary: #666666;
  --brand-text-muted: #969696;
  --brand-divider: #eeeeee;

  // -- Typography stack -------------------------------------------------------
  --brand-font-family: 'Roboto', 'Helvetica Neue', Arial, sans-serif;
  --brand-font-size-xs: 12px;
  --brand-font-size-sm: 13px;
  --brand-font-size-base: 14px;       // labels and body copy
  --brand-font-size-md: 16px;         // form fields (most pages)
  --brand-font-size-lg: 18px;         // login form fields
  --brand-font-size-button: 22px;     // primary login button

  // -- Layout dimensions ------------------------------------------------------
  --brand-sidebar-width: 282px;
  --brand-sidebar-width-collapsed: 70px;
  --brand-navbar-height: 86px;

  // -- Logo + asset slots -----------------------------------------------------
  --brand-logo-header-url: url('/assets/images/header-logo.png');
  --brand-logo-collapsed-url: url('/assets/images/fav-logo.png');
  --brand-logo-login-url: url('/assets/images/Doctor.png');
  --brand-login-bg-url: url('/assets/images/login-bg.jpg');

  // -- Tenant-overridable identity strings ------------------------------------
  --brand-clinic-name: 'Appointment Portal';
}
```

Per-feature design docs reference deployed variables by name. Example:

> "Approve button uses `background: var(--brand-primary)` and
> `color: #fff` per `demo-falkinstein.css`:41-42."

The spec variables (`--color-*`, `--text-body`, `--border-light`, `--bg-*`, `--space-*`,
`--radius-*`, `--shadow-*`, `--z-*`, `--motion-*`) listed in the tables above are
targeted for Phase 19b. Do not reference them in component SCSS until they are added
to `_brand.scss`.

## Strict-parity exceptions allowed

Three OLD patterns we explicitly do NOT replicate verbatim, with
rationale:

1. **Inline `style="background-image: url(...)"`** (login bg, OLD
   `login.component.html`:1). NEW moves the URL to a CSS variable
   `--brand-login-bg-url` so per-tenant override is one declaration,
   not an Angular template edit. The visible pixels stay the same.
2. **Vendor-prefix duplications** (`-webkit-`, `-moz-`, `-ms-`, `-o-`).
   OLD ships them across the rx-control-design CSS. NEW relies on
   Angular's CSS pipeline + autoprefixer instead of hand-rolled
   prefixes. Output is equivalent for any browser the QA matrix
   supports.
3. **`!important` overrides for theme deltas** (e.g.,
   `demo-falkinstein.css`:138-149, the `.demo-vertical-spacing` block).
   NEW prefers CSS-variable cascade over `!important`. When a strict
   override is needed, it is documented in the per-feature design doc.

## Token coverage check

Every concrete value extracted above was found in the OLD source files
listed in frontmatter. There is no invented token. If a per-feature
design doc surfaces a value not in this table, treat that as a gap and
extend this doc before the per-feature doc lands.

Deployment status summary (verified 2026-06-01 vs `angular/src/styles/_brand.scss`):

- **Deployed (Phase 19a):** `--brand-primary` (`#055495`), `--brand-primary-hover`,
  `--brand-accent`, `--brand-button-active`, `--brand-link`, `--brand-error`,
  `--brand-sidebar-bg` / `-2` / `-3`, `--brand-input-bg`, `--brand-input-border`,
  `--brand-text-primary`, `--brand-text-secondary`, `--brand-text-muted`,
  `--brand-divider`, `--brand-font-family`, `--brand-font-size-xs/sm/base/md/lg/button`,
  `--brand-sidebar-width` / `-collapsed`, `--brand-navbar-height`,
  `--brand-logo-*-url`, `--brand-login-bg-url`, `--brand-clinic-name`. (30 variables)
- **Spec only / Phase 19b target:** `--color-*`, `--text-*`, `--border-light`,
  `--bg-*`, `--scrim-*`, `--font-family-base`, `--fw-*`, `--fs-*`, `--lh-*`,
  `--space-*`, `--radius-*`, `--shadow-*`, `--bp-*`, `--z-*`, `--motion-*`.

## Doc template appendix (for per-feature `<feature>-design.md`)

```markdown
---
feature: <name>
status: draft
audited: YYYY-MM-DD
old-source:
  - <OLD html path>
  - <OLD ts path>
  - <OLD scss path>
parity-audit: docs/parity/<feature>.md
strict-parity: true
---

# <Feature> -- design

## Routes
- `<old route>` -> NEW `<new route>`

## Screen layout
- ASCII or annotated screenshot (link to `assets/`)

## Form fields
| Label | Field | Type | Validation | Default | Conditional visibility | Strict-parity citation |

## Tables / grids
| Column | Sortable | Filter | Visible to roles | OLD citation |

## Modals + interactions
| Trigger | Modal | OLD source | Notes |

## Buttons / actions
| Label | Permission gate | Success toast | Error toast | OLD citation |

## Role visibility matrix
| UI element | Patient | Adjuster | App. Atty | Def. Atty | Clinic Staff | Supervisor | IT Admin |

## Branding tokens used
- List of `--*` vars consumed (cross-link to `_design-tokens.md`)

## NEW current-state delta
- What `angular/src/app/<feature>/` already does vs the audit target
- Gaps + planned closes

## Strict-parity exceptions
- Each deviation with explicit rationale
```
