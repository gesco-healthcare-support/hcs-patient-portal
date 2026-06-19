---
feature: notification-templates-wysiwyg
date: 2026-06-19
status: in-progress
base-branch: feat/frontend-rework
lane: Session A
backlog-item: 6
protocol: 2026-06-19-parallel-build-protocol.md
related-issues: []
---

## Goal

Give IT Admin a WYSIWYG editor for the notification-template email body, sanitize the
submitted HTML server-side before persist, and paginate the templates list -- with no schema
change.

## Context

Backlog #6. The notification-template backend is complete: `NotificationTemplate` (entity),
`NotificationTemplatesAppService` (Read + Update only; manual `NotificationTemplatesController`;
`[RemoteService(IsEnabled=false)]`), the Angular proxy, and the `/admin/templates` admin hub
all exist. The Old-App audit logged "Notification template editor screen for IT Admin" as a
known missing UI; the screen exists today but edits the body in a **plain textarea**.

Migration-free is CONFIRMED, not assumed:
- `BodyEmail` maps to `nvarchar(max)` (`IsRequired()`, no `HasMaxLength`) -- verbose WYSIWYG
  HTML cannot overflow it (CaseEvaluationDbContext.cs:586).
- `NotificationDispatcher` already sends `BodyEmail` with `IsBodyHtml = true` hardcoded
  (NotificationDispatcher.cs:131-132,163-164). The field is *already* HTML at send time (the
  seed bodies are the OLD HTML templates). Swapping the textarea for a WYSIWYG that emits HTML
  changes nothing downstream. There is no `IsBodyHtml` column and none is needed.

Locked decisions (do not relitigate): editor = `ngx-quill` (Quill BSD-3 + ngx-quill MIT, no
commercial license; Angular-native); scope = `BodyEmail` WYSIWYG only -- `BodySms` stays a
plain textarea (SMS is plain text). Session B owns the EF migration lane; this item touches
none of it. Proxy regen is NOT required -- the `UpdateDto` contract is unchanged and
sanitization is internal to the AppService.

## Approach

- **Sanitize on write, in `UpdateAsync`.** Add an injectable `IEmailBodySanitizer` (Ganss
  `HtmlSanitizer`, NuGet `HtmlSanitizer` 9.0.892, MIT, namespace `Ganss.Xss`) to the
  Application project and call it on `input.BodyEmail` beside the existing `ValidateBodies` /
  `ValidateSubjectLength` guards. Sanitize once at save, not on every send -- the renderer
  (`NotificationTemplateRenderer`) returns bodies verbatim by design, so the stored value must
  already be safe. `BodySms` is NOT HTML-sanitized (it is plain text edited via a textarea).
- **Preserve `##Var##` placeholder URLs.** Ganss validates `href`/`src` against allowed
  schemes, which would strip a templated link like `<a href="##ResetUrl##">`. Configure the
  sanitizer's URL filter (`FilterUrl` event) to pass through any URL that is exactly a
  `##...##` token, so existing templated links survive. Token *text* (e.g. `##PatientName##`
  in body copy) is untouched by the sanitizer regardless (it operates on tags/attributes/CSS,
  not text nodes).
- **WYSIWYG via ngx-quill** on the `BodyEmail` field only; the existing `##Token##` chip
  palette is retargeted to insert at the Quill cursor. The editor's rendered view doubles as
  the live preview (the current plain-text preview pane is dropped for email).
- **Pagination** is a front-end-only addition: `GetListAsync` already returns a real
  `PagedResultDto` with `totalCount`; wire `skipCount`/`maxResultCount` (page size 20) and a
  pager control to the list.

Alternatives rejected:
- **TipTap** -- MIT core but no first-class Angular wrapper (more glue) and some paid
  extensions; no licensing gain over Quill.
- **Enhanced plain textarea** -- zero new dependency, but not a true WYSIWYG; fails the
  backlog's explicit "rich-text editor" ask.
- **Sanitize on send (in the renderer)** -- re-sanitizes on every dispatch and spreads the
  security boundary across all handlers; rejected for sanitize-once-at-the-write-boundary.
- **New `IsHtml`/sanitization column** -- unnecessary; `BodyEmail` is already HTML.

## Tasks

- T1: Backend HTML sanitizer service.
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/HealthcareSupport.CaseEvaluation.Application.csproj, src/HealthcareSupport.CaseEvaluation.Application/NotificationTemplates/EmailBodySanitizer.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/NotificationTemplates/IEmailBodySanitizer.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/NotificationTemplates/EmailBodySanitizerTests.cs]
  - detail: add the `HtmlSanitizer` package ref; define `IEmailBodySanitizer.Sanitize(string html) -> string` + `EmailBodySanitizer` (`ITransientDependency`) wrapping a configured `Ganss.Xss.HtmlSanitizer`. Allowlist = common email formatting (p, br, b/strong, i/em, u, ul/ol/li, a, h1-h4, blockquote, span, div, table family, img) + safe inline styles; disallow script/style/iframe/object and all `on*` handlers. Add the `FilterUrl` handler that returns the original URL unchanged when it matches `^##[A-Za-z0-9_]+##$`.
  - acceptance: tests prove `<script>` and `onclick` are stripped; `<b>`/`<a href="https://...">` survive; `<a href="##ResetUrl##">x</a>` keeps its href; literal `##PatientName##` body text is unchanged.

- T2: Wire the sanitizer into `UpdateAsync`.
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/NotificationTemplates/NotificationTemplatesAppService.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/NotificationTemplates/NotificationTemplatesAppServiceTests.cs]
  - detail: inject `IEmailBodySanitizer`; after `ValidateBodies(input)`, set `input.BodyEmail = _sanitizer.Sanitize(input.BodyEmail)` before `ApplyUpdate`. Leave `BodySms` as-is. Keep the existing `internal static` test-seam style where practical.
  - acceptance: an `UpdateAsync` whose `BodyEmail` contains a `<script>` persists a body with the script removed and benign HTML intact; existing template tests still pass.

- T3: Front-end WYSIWYG editor on `BodyEmail`.
  - approach: test-after
  - files-touched: [angular/package.json, angular/package-lock.json, angular/angular.json, angular/src/app/admin/internal-admin-hub.component.ts, angular/src/app/admin/internal-admin-hub.component.html, angular/src/styles/(admin scss)]
  - detail: pin `ngx-quill` + `quill` (resolve the exact Angular-20-compatible version via `npm view ngx-quill peerDependencies` at install); register Quill's snow CSS (angular.json styles or an scss import); replace the `BodyEmail` textarea with `<quill-editor>` two-way bound to the draft signal; retarget the `##Token##` insert action to insert at the Quill selection. `BodySms` textarea unchanged. If the component exceeds the 250-line Angular cap, extract a `template-body-editor` child component.
  - acceptance: editing an email body shows formatted output live; bold/italic/list/link work; the variable button inserts `##Token##` into the body; save round-trips; component lints clean (eslint --max-warnings=0).

- T4: Front-end list pagination.
  - approach: code
  - files-touched: [angular/src/app/admin/internal-admin-hub.component.ts, angular/src/app/admin/internal-admin-hub.component.html, angular/src/styles/(admin scss)]
  - detail: page size 20; pass `skipCount`/`maxResultCount` to the list service call; render a pager driven by the response `totalCount`; reuse the existing `ia-table` / pagination pattern from the internal list pages.
  - acceptance: the templates list shows a pager; paging changes the visible rows; filters reset to page 1.

- T5: Live verification on Falkinstein.
  - approach: code
  - files-touched: []
  - detail: coordinate the shared angular + api container restart with Session B (T3 needs a fresh `npm install`; T1/T2 need an api rebuild). Login stafsuper1@gesco.com.
  - acceptance: screenshots of -- (a) the Quill editor formatting an email body, (b) a `##Var##` inserted via the button, (c) save succeeds and persists, (d) a pasted `<script>` is gone after save (sanitization), (e) a templated `##...Url##` link's href survives, (f) the list pager paging. Capture computed styles where layout matters.

## Risk / Rollback

- Blast radius: one new backend service + one line in `UpdateAsync`; the `/admin/templates`
  editor + list UI; two FE deps. No schema change, no proxy regen, no Session B file overlap
  (B is on appointments/attorneys). Email send path is unchanged (already HTML).
- Process risks: adding `ngx-quill`/`quill` changes `package.json` + lockfile -> `npm install`
  + a shared angular-container rebuild (coordinate with Session B before restarting). The
  `HtmlSanitizer` NuGet ref is a `.csproj` change the pre-push working-tree build compiles.
- Security: sanitization narrows what admins can store; over-strict allowlist could drop
  legitimate markup from a seed template -- T1 tests pin the allowlist against real template
  constructs (links, lists, tables).
- Rollback: revert the commits; the sanitizer is additive and the field type is unchanged, so
  no data migration is needed to roll back.

## Verification

End-to-end on Falkinstein as Staff Supervisor (or an IT-Admin role with
`NotificationTemplates.Edit`): open `/admin/templates`, pick an email template, format the body
in the WYSIWYG, insert a `##Var##` via the button, paste a `<script>alert(1)</script>` and an
`onclick` attribute, Save. Reload and confirm the stored body has the script/handler removed
and the formatting + `##Var##` + any templated link href intact. Run Send-test and confirm the
email renders as HTML. Page through the templates list with the new pager. Backend: `dotnet
test` for the sanitizer + AppService tests; api compiles clean on container restart.
