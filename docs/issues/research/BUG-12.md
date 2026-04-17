[Home](../../INDEX.md) > [Issues](../) > Research > BUG-12

# BUG-12: Page Title Shows "MyProjectName" Placeholder -- Research

**Severity**: Low
**Status**: **Partially resolved** (verified 2026-04-17)
**Source files**:
- `angular/src/index.html` (already fixed)
- `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/**/*.cshtml` (needs audit)
- `src/.../Domain.Shared/Localization/CaseEvaluation/en.json` (needs audit)

---

## Current state (verified 2026-04-17)

**Partial resolution confirmed:**
- `angular/src/index.html` line 5 now shows `<title>CaseEvaluation</title>` -- fixed.
- Grep of `MyProjectName` across repo: only hits in `docs/` and `.claude/worktrees/` -- no source-code matches in `src/` or `angular/src/`.

**What might still be open:**
- The 2026-04-16 Docker E2E report described "alternating CaseEvaluation and MyProjectName" which on ABP 8+ is typically driven by **server-rendered AuthServer Razor pages (`.cshtml`)** or `Title.setTitle()` calls that read from the default-resource JSON key -- not from `index.html`.
- ABP support #8134 (cited below) traced an identical symptom to a localization key in `en.json` whose value was literally `"MyProjectName"`. The HTML fix was cosmetic only.

Until AuthServer Razor pages and `en.json` are audited, the resolution remains partial.

---

## Official documentation

- [Angular `Title` service](https://angular.dev/api/platform-browser/Title) -- `getTitle()` / `setTitle(newTitle)`. Used when an Angular app can't bind to `<html><title>` directly because it bootstraps below it.
- [ABP LeptonX theming (MVC)](https://abp.io/docs/latest/ui-themes/lepton-x/mvc) + [ABP MVC Page Header](https://abp.io/docs/latest/framework/ui/mvc-razor-pages/page-header) -- `IPageLayout.Content.Title` flows into HTML `<title>` in MVC/Razor; branding (app name) flows from `IBrandingProvider`.
- [ABP branding (`IBrandingProvider`)](https://abp.io/docs/latest/framework/ui/mvc-razor-pages/branding) -- override `AppName` in a custom `IBrandingProvider`; default reads from startup template constants that often still say `MyProjectName`.
- [ABP Suite -- editing templates](https://abp.io/docs/latest/suite/editing-templates) -- ABP Suite renames `MyCompanyName.MyProjectName` at generate time; manual renames after the fact don't always update every file.

## Community findings

- [ABP support #8134 -- Tab title shows MyProjectName instead of ABCD](https://abp.io/support/questions/8134/Tab-title-in-browser-is-displayed-as-MyProjectName-instead-of-ABCD) -- root cause was NOT the HTML `<title>`; browser tab title was driven by a key in `en.json`. Fix was updating the localization file, not markup. Likely culprit for alternating-title symptom.
- [DeepWiki -- Project Creation and Templates](https://deepwiki.com/abpframework/abp/3.2-project-creation-and-templates) -- catalogues placeholders ABP templates use (`MyCompanyName`, `MyProjectName`).
- [abpframework/abp #13003 -- Add CLI rename option](https://github.com/abpframework/abp/issues/13003) -- no first-class post-creation rename tool exists; leftovers after manual rename are a known class of bug.
- [abpframework/abp #19823 -- Blazor Page Title problem](https://github.com/abpframework/abp/issues/19823) -- confirms page-title pipeline runs through `IPageLayout`/branding, not index HTML.

## Recommended approach

1. Grep entire repo (excluding `docs/`, `.claude/worktrees/`) for `MyProjectName`; also grep `en.json` for any default-resource key whose value is literally `MyProjectName` (most likely `"AppName"` or similar). Update the value -- support thread #8134 shows this as the canonical fix.
2. Audit AuthServer + HttpApi.Host Razor pages (`*.cshtml`) and any `IBrandingProvider` implementation for hardcoded `MyProjectName`.
3. If any Angular component calls `Title.setTitle()` (none found in grep), ensure it reads from ABP localization, not a hardcoded string.

## Gotchas / blockers

- HTML `<title>` tag in `index.html` is only the initial SSR/first-paint value. ABP Angular shell overwrites it on route change via its own title strategy; if that strategy pulls from a stale localization key, the HTML correction is cosmetic.
- LeptonX theme may compose title as `"{PageTitle} | {AppName}"` -- both pieces need updating.
- `appsettings.json` / `appsettings.secrets.json` occasionally carry `MyProjectName` in OIDC client display names or email templates. Grep everywhere.
- `Title.setTitle` in Angular doesn't trigger update in server-rendered meta tags for crawlers -- not critical for an internal portal.

## Open questions

- Which file(s) in `src/.../AuthServer/Pages/**/*.cshtml` still render `MyProjectName`, if any? Explicit grep after login/consent E2E will catch them.
- Does `en.json` contain a default-resource key whose value is literally `MyProjectName`? Single JSON value is probably the sole remaining source.
- Is the product's canonical display name "CaseEvaluation" or something more user-friendly ("HCS Case Evaluation Portal")? Align with marketing before fixing to avoid a second round of edits.

## Related

- [ARC-07](ARC-07.md) -- same `en.json` surface
- [docs/issues/BUGS.md#bug-12](../BUGS.md#bug-12-page-title-shows-myprojectname-placeholder)
