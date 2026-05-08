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
  - docs/parity/_branding.md  # full brand-token surface + per-feature touchpoints
strict-parity: true
---

# Design tokens -- OLD-anchored visual contract

Single source of truth for the **runtime CSS-variable contract** every Phase 1
feature consumes. Captures OLD's literal color, typography, spacing,
border-radius, shadow, breakpoint, and motion values so per-feature design
docs can refer to one canonical set rather than re-derive them per page.

**Strict-parity directive:** internal pages keep OLD's exact visual
contract. External-user-facing pages parameterize the **brand layer**
(primary color, logo, clinic name, support contact) per
`docs/parity/_branding.md` Section A; everything else stays verbatim.

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

| Token | OLD value(s) | OLD source | NEW CSS var |
|---|---|---|---|
| Primary | `#06519f` (deep blue) -- all 3 themes | `demo-falkinstein.css`:40-41, `demo-longacre.css`:42-43, `demo-pelton.css`:41-42 | `--brand-primary` |
| Primary text-on-light | `#005494` | `demo-falkinstein.css`:323, 327, 331; `demo-pelton.css`:325, 329, 333 | `--brand-primary-text` |
| Primary focus shadow | `rgba(0, 84, 148, 0.3)` (inset) | `demo-falkinstein.css`:359-361 | `--brand-primary-focus-shadow` |
| Primary focus border | `rgba(0, 84, 148, 0.5)` | `demo-falkinstein.css`:359 | `--brand-primary-focus-border` |

OLD's `Longacre` theme overrides `text-primary` to `#06519f` (matches the
primary), while `Falkinstein` and `Pelton` use `#005494` -- a 6%-darker
hue. NEW collapses both to `--brand-primary-text` and lets each tenant
override.

### Status / semantic colors (shared across all OLD themes)

| Token | OLD value | OLD source | NEW CSS var |
|---|---|---|---|
| Success | `#51a351` | `rx-control-design/toast.css`:66 | `--color-success` |
| Danger | `#bd362f` (toast) / `#e6514a` (button) | `toast.css`:95, `demo-falkinstein.css`:309 | `--color-danger`, `--color-danger-button` |
| Warning | `#f89406` | `toast.css`:103 | `--color-warning` |
| Info | `#2f96b4` | `toast.css`:110 | `--color-info` |

These four are HTML-attribute baked across all rx-toast / rx-dialog
flows. Per-feature design docs cite them as `--color-*`.

### Neutral grayscale (shared across all OLD themes)

| Token | OLD value | Usage in OLD | NEW CSS var |
|---|---|---|---|
| Body text | `#333` | `dialog.css`:41 (modal body) | `--text-body` |
| Heading / dialog header | `#414c55` | `dialog.css`:7 | `--text-heading` |
| Muted text | `#666` | `demo-falkinstein.css`:193, 306 | `--text-muted` |
| Disabled / placeholder text | `#999` | `vendor/app.css`:51, datepicker arrows `datepicker.css`:27, 90 | `--text-disabled` |
| Inactive nav text | `#a3a4a6` | `demo-falkinstein.css`:316, 320 | `--text-inactive` |
| Code-snippet inline | `#c7254e` | `demo-falkinstein.css`:189 | `--text-code` |
| Border light | `#eee` | `demo-falkinstein.css`:274, 334-336, 338 | `--border-light` |
| Background body | `#fff` | universal | `--bg-body` |
| Background sidenav (light layout) | `#fff` | implied via theme `default-style` | `--bg-sidenav` |
| Background card | `#fff` | `demo-falkinstein.css`:267, 303 | `--bg-card` |
| Background hover light | `#f7f7f7` | `vendor/app.css`:7 (login form) | `--bg-hover-light` |

### Accent secondaries (shared across all OLD themes -- NOT per-tenant)

These three appear in feature tiles / dashboard cards. NEW keeps them
shared.

| Token | OLD value | Usage | NEW CSS var |
|---|---|---|---|
| Slate | `#607d8b` | dashboard tile bg, `demo-falkinstein.css`:211, 300 | `--color-slate` |
| Teal | `#3ca99e` | dashboard tile bg, `demo-falkinstein.css`:218 | `--color-teal` |
| Gray | `#9e9e9e` | dashboard tile bg, `demo-falkinstein.css`:225 | `--color-gray` |

### Overlay / scrim

| Token | OLD value | Usage | NEW CSS var |
|---|---|---|---|
| Modal scrim | `rgba(0,0,0,.5)` | `dialog.css`:13 | `--scrim-modal` |
| Image overlay (login) | `rgba(0,0,0,.25)` (`bg-dark opacity-25`) | `login.component.html`:3 | `--scrim-image` |
| Subtle inset (code blocks) | `rgba(0,0,0,.04)` | `demo-falkinstein.css`:186, 188 | `--scrim-inset` |

## Typography tokens

| Token | OLD value | OLD source | NEW CSS var |
|---|---|---|---|
| Font family base | `Roboto, "Segoe UI", "Lucida Grande", Verdana, Arial, Helvetica, sans-serif` | `index.html`:10 + `dialog.css`:8 | `--font-family-base` |
| Font weight light | `300` | Roboto link | `--fw-light` |
| Font weight normal | `400` | Roboto link | `--fw-normal` |
| Font weight medium | `500` | Roboto link, `vendor/app.css`:46 (sidebar nav-link) | `--fw-medium` |
| Font weight bold | `700` | Roboto link, `app.css`:24 (.btn) | `--fw-bold` |
| Font weight black | `900` | Roboto link | `--fw-black` |
| Font size base | `1rem` (16px -- Bootstrap 4 default) | not overridden | `--fs-base` |
| Font size sm (sidebar heading) | `.75rem` (uppercase) | `vendor/app.css`:64 | `--fs-sm` |
| Font size dialog header | `13px` | `dialog.css`:23 | `--fs-dialog-header` |
| Font size login button | `15px` | `vendor/app.css`:23 | `--fs-button-login` |
| Font size socal-label | `.894rem` | `demo-falkinstein.css`:46 | `--fs-label` |
| Font size app-brand-text | `1.1rem` | `demo-falkinstein.css`:71 | `--fs-app-brand` |
| Font size dialog title | `20px/24px` | `dialog.css`:8 | `--fs-dialog-title`, `--lh-dialog-title` |
| Font size toast message | `14px` (default) / 18px title | `toast.css`:166 | `--fs-toast`, `--fs-toast-title` |
| Line height tight | `1.2` (Bootstrap 4 default) | -- | `--lh-tight` |
| Line height base | `1.5` (Bootstrap 4 default) | -- | `--lh-base` |
| Login button line-height (forced override) | `40px` | `demo-falkinstein.css`:20 | `--lh-button-pull-right` |

OLD references the system fallback chain `'Segoe UI','Lucida Grande',Verdana`
inside `dialog.css`:8 only -- the rest of the app inherits the body's
Roboto. NEW keeps the system fallback chain in `--font-family-base` so
dialogs render with the same fallback if Roboto fails to load.

## Spacing tokens

OLD uses Bootstrap 4's `rem` scale for most components plus a handful of
custom values. NEW exposes the seen values as a token scale; per-feature
docs cite by name.

| Token | OLD value | OLD source | NEW CSS var |
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

| Token | OLD value | Usage | NEW CSS var |
|---|---|---|---|
| Radius sm | `2px` | form-control + btn (`vendor/app.css`:18), layout-example-block (`demo-falkinstein.css`:187) | `--radius-sm` |
| Radius md | `3px` | toast-card (`toast.css`:139), alertbox (`dialog.css`:36) | `--radius-md` |
| Radius lg | `5px` | socal-label (`demo-falkinstein.css`:44) | `--radius-lg` |
| Radius pill | `50%` | app-brand-logo (`demo-falkinstein.css`:56), avatar | `--radius-pill` |

OLD's button + form-control radius is `2px` -- noticeably tighter than
Bootstrap 4's default `0.25rem`. NEW preserves OLD's value via
`--radius-sm` so external pages keep the OLD look.

## Box-shadow tokens

| Token | OLD value | Usage | NEW CSS var |
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
exact values; per-feature docs cite breakpoint names.

| Token | OLD value | NEW CSS var |
|---|---|---|
| Breakpoint sm | `576px` | `--bp-sm` |
| Breakpoint md | `768px` | `--bp-md` |
| Breakpoint lg | `992px` | `--bp-lg` |
| Breakpoint xl | `1200px` | `--bp-xl` |

OLD's responsive shell hides the side-nav below `lg` (sidenav-toggle
collapses); see `demo-falkinstein.css`:94-99 (`@media (min-width: 992px)`).

## Z-index tokens

| Token | OLD value | Usage | NEW CSS var |
|---|---|---|---|
| z dialog | `9999` | `dialog.css`:24 | `--z-dialog` |
| z sidebar (in `vendor/app.css` shell) | `100` | `vendor/app.css`:31 | `--z-sidebar` |
| z toast | implied above dialog | -- | `--z-toast` (NEW: `10000`) |
| z popup | implied below dialog | `popup.css` | `--z-popup` (NEW: `9000`) |

## Motion tokens

| Token | OLD value | Usage | NEW CSS var |
|---|---|---|---|
| Transition modal scale | `all 0.5s ease-in-out` | `dialog.css`:63 | `--motion-modal` |
| Transition opacity | `200ms opacity` | `dialog.css`:12 | `--motion-opacity` |
| Animation toast fade | `0.5s` (commented; OLD inactive) | `toast.css`:68-69 (commented) | `--motion-toast-fade` (deferred) |

## NEW implementation contract (`angular/src/styles/_brand.scss`)

The same `_brand.scss` file referenced from `_branding.md` Section
"Brand-token surface model" is the runtime carrier. Concrete
declaration template:

```scss
:root {
  /* ----- Brand layer (per-tenant override) ----- */
  --brand-primary: #06519f;
  --brand-primary-text: #005494;
  --brand-primary-focus-shadow: rgba(0, 84, 148, 0.3);
  --brand-primary-focus-border: rgba(0, 84, 148, 0.5);
  --brand-logo-url: url('/assets/branding/default/logo.png');
  --brand-fav-url: url('/assets/branding/default/fav.png');
  --brand-clinic-name: 'Clinic';

  /* ----- Status colors (shared) ----- */
  --color-success: #51a351;
  --color-danger: #bd362f;
  --color-danger-button: #e6514a;
  --color-warning: #f89406;
  --color-info: #2f96b4;

  /* ----- Neutrals + accents (shared) ----- */
  --text-body: #333;
  --text-heading: #414c55;
  --text-muted: #666;
  --text-disabled: #999;
  --text-inactive: #a3a4a6;
  --text-code: #c7254e;
  --border-light: #eee;
  --bg-body: #fff;
  --bg-sidenav: #fff;
  --bg-card: #fff;
  --bg-hover-light: #f7f7f7;
  --color-slate: #607d8b;
  --color-teal: #3ca99e;
  --color-gray: #9e9e9e;
  --scrim-modal: rgba(0, 0, 0, 0.5);
  --scrim-image: rgba(0, 0, 0, 0.25);
  --scrim-inset: rgba(0, 0, 0, 0.04);

  /* ----- Typography (shared) ----- */
  --font-family-base: 'Roboto', 'Segoe UI', 'Lucida Grande', Verdana, Arial, Helvetica, sans-serif;
  --fw-light: 300;
  --fw-normal: 400;
  --fw-medium: 500;
  --fw-bold: 700;
  --fw-black: 900;
  --fs-base: 1rem;
  --fs-sm: 0.75rem;
  --fs-label: 0.894rem;
  --fs-app-brand: 1.1rem;
  --fs-dialog-header: 13px;
  --fs-dialog-title: 20px;
  --lh-dialog-title: 24px;
  --fs-button-login: 15px;
  --fs-toast: 14px;
  --fs-toast-title: 18px;
  --lh-tight: 1.2;
  --lh-base: 1.5;

  /* ----- Spacing (shared) ----- */
  --space-xxs: 0.25rem;
  --space-xs: 0.375rem;
  --space-sm: 0.5rem;
  --space-md: 0.9375rem;
  --space: 1rem;
  --space-lg: 1.125rem;
  --space-xl: 1.875rem;
  --space-xxl: 5rem;
  --space-login-form: 30px;
  --space-card: 30px;

  /* ----- Radius (shared) ----- */
  --radius-sm: 2px;
  --radius-md: 3px;
  --radius-lg: 5px;
  --radius-pill: 50%;

  /* ----- Shadows (shared) ----- */
  --shadow-login-form: 0px 2px 2px 2px rgba(0, 0, 0, 0.3);
  --shadow-modal: 0 4px 23px 5px rgba(0, 0, 0, 0.2), 0 2px 6px rgba(0, 0, 0, 0.15);
  --shadow-toast: 0 0 12px #999;
  --shadow-sidenav-inset: inset -1px 0 0 rgba(0, 0, 0, 0.1);
  --shadow-input-focus: 0 1px 2px 0 rgba(0, 84, 148, 0.3) inset;

  /* ----- Z-index (shared) ----- */
  --z-popup: 9000;
  --z-dialog: 9999;
  --z-toast: 10000;

  /* ----- Motion (shared) ----- */
  --motion-modal: all 0.5s ease-in-out;
  --motion-opacity: 200ms opacity;
}

/* Per-tenant override example */
[data-tenant='falkinstein'] {
  --brand-primary: #06519f;
  --brand-primary-text: #005494;
  --brand-logo-url: url('/assets/branding/falkinstein/logo.png');
  --brand-clinic-name: 'Dr. Yuri Falkinstein, MD';
}
```

Per-feature design docs reference variables by name. Example:

> "Approve button uses `background: var(--brand-primary)` and
> `color: #fff` per `demo-falkinstein.css`:41-42."

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
