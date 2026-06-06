---
feature: packet-html-pipeline-integration
date: 2026-06-05
status: in-progress
base-branch: main
related-issues: []
supersedes: docs/plans/2026-06-04-doctor-packet-pipeline-integration.md
predecessor: docs/plans/2026-06-02-doctor-packet-html-template.md
---

## Design revision (2026-06-05): Option B -- sidecar owns the templates

To honor "single source of truth, no committed second copy" without putting Python in the
Python-free .NET SDK build images, **the packet-renderer sidecar now owns the HTML templates**:
its image bakes the generators (`tools/packet-templates/*/build_*.py`) and RUNs them at image
build, so the only HTML source is the `.py` generators (regenerated every image build -- no drift,
nothing committed). The `.NET side embeds NO HTML`: it builds the `##Group.Field##` token map
(`PacketTokenMap.Build`) and POSTs `{template, tokens}` to `/render`; the sidecar substitutes
(single-pass regex, unknown tokens left literal -- mirrors the DOCX path) and renders. This
supersedes the earlier "embed `.html` in Domain + in-process `HtmlTemplateRenderer`" approach from
sections B/C/D below: `HtmlTemplateRenderer`/`IHtmlTemplateRenderer`, the `Resources/*.html`
embeds + csproj `*.html` glob, and `EmbeddedTemplateResources.LoadHtmlTemplate` are all removed.
The converter is now `IHtmlPacketRenderer.RenderAsync(templateName, tokens)` /
`WeasyPrintPacketRenderer` (POSTs JSON); template names live in `Pdf/PacketTemplateNames`.

## Progress (2026-06-05, branch feat/packet-html-pipeline)

All built per the **Option B** revision above (sidecar owns templates; .NET embeds no HTML).

- **DONE Phase 0/1 (sidecar + templates):** generators at `tools/packet-templates/{doctor,patient,
  attorney,shared}/` (single source; `*.html`/`*.pdf` gitignored). `docker/packet-renderer/Dockerfile`
  COPYs the generators and RUNs them at image build (bakes 4 HTML); `app.py` loads them, `/render`
  takes `{template, tokens}` (single-pass regex sub, unknown tokens literal) -> WeasyPrint `--pdf-forms`
  + `post_process.finalize` (no-AcroForm guard); `/health` lists templates. `packet-renderer` compose
  service (3001, healthcheck) + `api depends_on` + `PacketRenderer__Url` + `Packets__HtmlPipeline__*`.
  Verified live: image builds (generators run), `/health` ok, `/render` -> doctor 1330 / patient 755 /
  attorney-ame 0 / attorney-pqme 0 fields, token substitution confirmed (value in, `##token##` gone),
  unknown-template + non-JSON -> 400.
- **DONE Phase 2 (tokens):** extracted `PacketTokenMap` (shared map+regex+sig-placeholder);
  `DocxTemplateRenderer` delegates to it; 2 interpreter tokens (`PacketTokenContext` +
  resolver `DeriveInterpreter` + `AppointmentLanguage` repo + map). `IHtmlTemplateRenderer`/
  `HtmlTemplateRenderer` NOT built (substitution is in the sidecar under Option B).
- **DONE Phase 3 (converter):** `IHtmlPacketRenderer.RenderAsync(templateName, tokens)` +
  `WeasyPrintPacketRenderer` (POSTs JSON `{template, tokens}`, never logs tokens/PDF) +
  `Pdf/PacketTemplateNames` + typed-HttpClient DI.
- **DONE Phase 4 (routing):** `GenerateKindAsync` branches on per-kind flag; `HtmlTemplateName(kind,
  context)` picks Panel QME -> `attorney-pqme`, else `attorney-ame`. Removed embedded HTML +
  `LoadHtmlTemplate` + csproj `*.html` glob. Tests: `PacketTokenUnitTests` (map + `DeriveInterpreter`)
  + 3 job routing tests (assert requested template name). 24 domain tests pass; full host build clean (0/0).
- **TODO:** T8 (Phase 5 -- end-to-end + email regression + staging flip, USER GATE); T9 (Phase 6 --
  decommission Gotenberg/DOCX, separate PR). No commits yet (awaiting user direction). A `.NET`-side
  template-name/token CI parity check vs the sidecar could replace the dropped T2 sync-check if wanted.

## Goal

Replace the DOCX -> Gotenberg packet pipeline with an HTML -> WeasyPrint pipeline for **all three**
appointment packets (Patient, Doctor, AttorneyClaimExaminer), so packets are generated from the
verified HTML templates: Doctor + Patient as **fillable** PDFs, AttorneyClaimExaminer as **flat**
notices. Cut over behind a feature flag with the existing pipeline as instant fallback; once verified
in staging, decommission Gotenberg, the `.docx` templates, and the DOCX renderer. Round-trip ingestion
of filled PDFs remains **deferred**.

## Context

- **Templates are done + user-verified** (in `.tmp-packet-inspect/`): `doctor-template/build_doctor.py`
  (8 pp, fillable, 1330 fields, 15 tokens); `patient-template/build_patient.py` (15 pp, fillable, 19
  tokens, PHI: SSN/DOB); `AttorneyCE-template/build_attorney.py` -> **two** flat notices `ame_ime.html`
  (2 pp) + `pqme.html` (3 pp), 0 fields. All render via `weasyprint --pdf-forms` + the shared
  `post_process.py` (cc-highlight harvest, radio fix, single-line text-field auto-size).
- **Current pipeline (verified in code):**
  - `GenerateAppointmentPacketJob.GenerateInsideTenantAsync` resolves one `PacketTokenContext`, then
    loops `kindsToGenerate = [Patient, Doctor, AttorneyClaimExaminer]` calling `GenerateKindAsync`.
  - `GenerateKindAsync` (`.../Jobs/GenerateAppointmentPacketJob.cs:145-157`):
    `EmbeddedTemplateResources.LoadTemplate(kind)` -> `_renderer.Render(bytes, context)` ->
    `_docxToPdfConverter.ConvertAsync(docx)` -> `_packetsContainer.SaveAsync(blobName, ms)` ->
    `_packetManager.MarkGeneratedAsync(...)`. Per-kind failures are caught + `MarkFailedAsync`; transport
    errors propagate for Hangfire retry.
  - `EmbeddedTemplateResources`: kind -> `.docx` filename switch; loaded via `GetManifestResourceStream`
    with prefix `HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates.Resources.<file>`;
    embedded by csproj glob `<EmbeddedResource Include="AppointmentDocuments\Templates\Resources\*.docx" />`.
  - `IDocxToPdfConverter.ConvertAsync(byte[], CT)` -> `GotenbergDocxToPdfConverter` POSTs
    `/forms/libreoffice/convert`. DI: `AddHttpClient<IDocxToPdfConverter, GotenbergDocxToPdfConverter>`
    with `Gotenberg:Url` (default `http://gotenberg:3000`), 60s timeout (`CaseEvaluationDomainModule.cs:94-100`).
  - `gotenberg` compose service builds `docker/Dockerfile.gotenberg-fonts`, port 3000, healthcheck;
    `api` has `Gotenberg__Url` env + `depends_on: gotenberg: service_healthy`.
- **Token engine reuse (verified):** `DocxTemplateRenderer.BuildTokenMap(PacketTokenContext)`
  (`DocxTemplateRenderer.cs:406-483`) is a hand-coded `##Group.Field## -> context.Property` dictionary
  (44 tokens); `TokenRegex` (`:45-47`) matches `##Group.Field##`. Both are format-agnostic; only the
  `<w:p>`/`<w:t>` walker is DOCX-specific. HTML substitution = `BuildTokenMap(ctx)` + `TokenRegex.Replace(html, ...)`.
- **Token coverage:** every template token is SUPPORTED except `##Patients.InterpreterRequired##` and
  `##Patients.InterpreterLanguage##` (new, on the PQME QME form). `##Appointments.Signature##` is
  image-only (`StampSignature`) and is NOT used by our HTML templates (signatures are handwritten),
  so no signature-image handling is needed for HTML.
- **Email + serving:** Patient + AttorneyClaimExaminer packets are emailed (patient fills the patient
  packet before the appointment); Doctor packet is download-only. Output is stored in MinIO at
  `{tenant}/{appointmentId}/packet/{kind}/{guid}.pdf`.
- **Conventions (from CLAUDE.md):** converter interface/impl live in `Domain/AppointmentDocuments/Pdf/`;
  DI in `CaseEvaluationDomainModule`; tests in `test/HealthcareSupport.CaseEvaluation.Domain.Tests/
  AppointmentDocuments/` (xUnit + Shouldly + NSubstitute, `JobFixture` pattern); synthetic data only (HIPAA).

## Approach

### A. Packet-renderer service (Python sidecar, HTTP)
A long-running service that owns rendering, mirroring how Gotenberg is run/called:
- `POST /render` -- body = token-substituted HTML; returns a PDF. Internally: write HTML to a temp file,
  run `weasyprint --pdf-forms`, then run `post_process.py` (cc-harvest + radio fix + text auto-size).
  The HTML having `<input>`/`<textarea>` yields a fillable PDF (Doctor/Patient); HTML with none yields a
  flat PDF (AttorneyCE). One endpoint serves all three.
- `GET /health` -- compose healthcheck.
- Built from `docker/Dockerfile.weasyprint` (relocated from the scratch `shared/Dockerfile.weasyprint`:
  Debian + Pango + Carlito/Liberation fonts + weasyprint + pikepdf) plus a ~50-line Flask/WSGI wrapper
  and `post_process.py`. New compose service `packet-renderer` (port 3001, healthcheck); `api`
  `depends_on: packet-renderer: service_healthy`; env `PacketRenderer__Url: http://packet-renderer:3001`.
- **`post_process.py` must tolerate a no-AcroForm (flat) PDF** -- guard every `pdf.Root.AcroForm`
  access so the AttorneyCE notices (0 fields) pass through cleanly.

### B. .NET converter (mirror the DOCX path)
- **`IHtmlToPdfConverter.ConvertAsync(string html, CancellationToken)`** + `WeasyPrintHtmlToPdfConverter`
  in `Domain/AppointmentDocuments/Pdf/` (same folder as `IDocxToPdfConverter`, per its own docstring
  guidance). Typed `HttpClient`, base = `PacketRenderer:Url` (fallback `http://packet-renderer:3001`),
  POST `/render`, ~60s timeout. Register in `CaseEvaluationDomainModule` immediately after the Gotenberg
  block. `IDocxToPdfConverter`/Gotenberg stay untouched until decommission (Phase 6).

### C. Token substitution reuse (parity guaranteed)
- Promote `DocxTemplateRenderer.BuildTokenMap` to `internal static` (or extract a `PacketTokenMap`
  helper) so an **`HtmlTemplateRenderer.Render(htmlTemplate, context)`** can build the same map and apply
  `TokenRegex.Replace(html, m => map.TryGetValue(m.Value, out var v) ? v : m.Value)`. Identical token
  values to the DOCX path (same uppercasing/date/multi-row formatting).
- **Add the 2 missing tokens:** add `InterpreterRequired` + `InterpreterLanguage` properties to
  `PacketTokenContext`; populate in `PacketTokenResolver.PopulatePatientAsync` (derive Yes/No from
  `Patient.AppointmentLanguageId`/`OthersLanguageName`/`InterpreterVendorName`; language from the
  `AppointmentLanguage` lookup, fallback `OthersLanguageName`); add the 2 map entries in `BuildTokenMap`.

### D. Embedded HTML templates + image inlining + AttorneyCE selection
- **Relocate the generators** to `tools/packet-templates/{doctor,patient,attorney}/` (build-time source
  of truth); `post_process.py` lives with the service (`docker/packet-renderer/`).
- **Generators base64-inline all images** (`DWC_Logo.jpg`, `Anterior_Body.png`, `Posterior_Body.png`,
  and any `patient-template/images/*`) as data URIs, so the emitted HTML is self-contained when POSTed
  to the sidecar (no path/volume coupling).
- **Embed the generated HTML** as resources beside the DOCX: `PatientPacket.html`, `DoctorPacket.html`,
  `AttorneyCE-AmeIme.html`, `AttorneyCE-Pqme.html`. Add csproj glob `*.html`; add
  `EmbeddedTemplateResources.LoadHtmlTemplate(...)` returning the UTF-8 string.
- **AttorneyCE notice selection:** the AttorneyClaimExaminer kind now chooses between two notices by
  **appointment type** -- AME/IME -> `AttorneyCE-AmeIme.html`; Panel QME -> `AttorneyCE-Pqme.html`.
  The exact `AppointmentType` -> notice mapping is OPEN Q1 below.
- **CI sync-check:** a build step regenerates each `.html` and fails if it differs from the committed
  embedded resource (prevents generator/artifact drift).

### E. Job routing + feature flag
- `GenerateKindAsync` branches: when the flag selects HTML for that kind, `LoadHtmlTemplate(kind[,type])`
  -> `_htmlRenderer.Render(html, context)` -> `_htmlToPdfConverter.ConvertAsync(html)` -> store at the
  **same** blob path/status/event. Otherwise the existing DOCX path. Inject `IHtmlToPdfConverter`,
  `IHtmlTemplateRenderer`, and the flag into the job constructor (ABP auto-resolves).
- **Flag:** per-kind config (e.g. `Packets:HtmlPipeline:Doctor|Patient|Attorney = true|false`), default
  all `false` (DOCX). Allows verifying one kind at a time and instant rollback. (OPEN Q2: single flag vs
  per-kind.)

### F. Decommission (after staging verification)
Remove the `gotenberg` compose service, `*.docx` templates + their glob, `DocxTemplateRenderer`,
`IDocxToPdfConverter`/`GotenbergDocxToPdfConverter`, and the per-kind flag (HTML becomes the only path).

## Tasks

**Phase 0 -- template toolchain into the repo**
- T1: relocate `build_{doctor,patient,attorney}.py` + dev `render.sh` to `tools/packet-templates/*`;
  `post_process.py` -> `docker/packet-renderer/`. approach: code.
- T2: generators base64-inline all images; emit self-contained `.html`; embed as resources +
  `LoadHtmlTemplate`; csproj `*.html` glob; CI sync-check. approach: code. acceptance: each embedded
  `.html` renders via the sidecar to a PDF matching the verified scratch PDF (field counts: doctor 1330,
  patient = its verified count, attorney 0).

**Phase 1 -- packet-renderer sidecar**
- T3: `docker/packet-renderer/{Dockerfile, app.py, post_process.py}` (`/render` + `/health`);
  `post_process.py` no-AcroForm guard; add `packet-renderer` to `docker-compose.yml`; `api depends_on`.
  approach: test-after. acceptance: `/render` returns fillable PDFs (doctor/patient) and a flat PDF
  (attorney, 0 fields); `/health` green; **service is internal-only (no public port mapping beyond
  127.0.0.1) and never logs HTML/PDF bodies (PHI)**.

**Phase 2 -- token substitution**
- T4: promote/extract `BuildTokenMap`; add `HtmlTemplateRenderer`. approach: tdd. acceptance: HTML and
  DOCX renderers produce identical token values for a fixed `PacketTokenContext`.
- T5: add `Patients.InterpreterRequired` + `Patients.InterpreterLanguage` (context + resolver + map).
  approach: tdd. acceptance: resolver derives Yes/No + language from a synthetic patient; both tokens
  substitute on the PQME notice.

**Phase 3 -- .NET converter**
- T6: `IHtmlToPdfConverter` + `WeasyPrintHtmlToPdfConverter` + DI (mirror Gotenberg). approach: test-after.

**Phase 4 -- job routing**
- T7: branch `GenerateKindAsync` on the per-kind flag; implement AttorneyCE notice-by-type selection;
  `LoadHtmlTemplate`. approach: code + job-level tests (flag on/off per kind; correct notice per type).

**Phase 5 -- verification + staging flip**
- T8: end-to-end for all three kinds: token parity vs DOCX output; Doctor/Patient fillable in Acrobat +
  Edge; AttorneyCE flat + correct notice per appointment type; stored + downloadable; **email regression**
  -- Patient + Attorney emails still attach the correct packet. Flip flags to HTML in staging; prove
  rollback by toggling back.

**Phase 6 -- decommission** (separate PR, after Phase 5 sign-off)
- T9: remove Gotenberg service + `.docx` templates + `DocxTemplateRenderer`/`IDocxToPdfConverter`/
  `GotenbergDocxToPdfConverter` + the flag. approach: code.

**Round-trip ingestion: DEFERRED** (separate future plan; field names already enable it).

## Risk / Rollback

- **Blast radius:** new sidecar + new code paths; per-kind flag defaults to DOCX so production is
  unchanged until flipped. Rollback = set the flag back to DOCX (both pipelines coexist through Phase 5).
- **HIPAA / PHI:** the patient packet carries SSN/DOB; token-substituted HTML (PHI) is POSTed to the
  sidecar over the internal compose network. Mitigations: bind `packet-renderer` to `127.0.0.1` only,
  no auth-less public exposure; the sidecar and the converter MUST NOT log HTML/PDF content; PHI stays
  within the existing MinIO/DB boundaries. No NEW external egress vs the Gotenberg path.
- **Known risks + mitigations:**
  - *Token parity drift* -> HTML and DOCX share `BuildTokenMap` (verified in T4).
  - *Generator/artifact drift* -> CI regenerates `.html` and fails on mismatch (T2).
  - *Flat-PDF post_process crash* -> no-AcroForm guard (T3).
  - *AttorneyCE wrong notice* -> covered by per-type tests (T7) once OPEN Q1 is resolved.
  - *Sidecar down* -> healthcheck + Hangfire retry; flag fallback to DOCX during transition.

## Verification

1. Field inventories match the verified templates (Doctor 1330 `packet.doctor.*`; Patient `packet.patient.*`;
   AttorneyCE 0 fields).
2. Token parity: HTML-pipeline values equal DOCX-pipeline values for the same appointment; the 2 new
   interpreter tokens resolve correctly.
3. Doctor + Patient fillable (auto-size/multiline behave) in Acrobat + Edge; AttorneyCE flat with the
   correct notice per appointment type.
4. Stored at the existing blob path + downloadable; Patient/Attorney emails attach the correct packet.
5. Per-kind flag toggles cleanly both ways; DOCX path unaffected while any kind is still on DOCX.

## Decisions

**Resolved:**
1. **All three packets migrate to HTML->WeasyPrint** in one effort (templates are ready); Doctor/Patient
   fillable, AttorneyCE flat. Single sidecar; Gotenberg removed at the end.
2. **Reuse the token engine** (`BuildTokenMap` + regex) for HTML -- no re-implementation; guarantees parity.
3. **Round-trip ingestion deferred.**
4. **Generation only -- email routing unchanged** (regression-checked); patient packet is fillable +
   emailed (patient fills it pre-appointment), Doctor download-only, Attorney emailed flat.

5. **AttorneyCE notice by appointment type:** AME + IME -> `AttorneyCE-AmeIme.html`; Panel QME ->
   `AttorneyCE-Pqme.html`; any other appointment type that still generates an attorney packet defaults
   to `AttorneyCE-AmeIme.html`. (In T7, confirm the canonical `AppointmentType` names/ids: match the
   Panel-QME type -> pqme, otherwise -> ame_ime.)
6. **Per-kind feature flags** `Packets:HtmlPipeline:{Doctor,Patient,Attorney}`, default DOCX -- flip and
   verify one kind at a time, instant per-kind rollback.
7. **Patient email = the fillable PDF** (the patient completes it digitally before the appointment).

**Still open:** none blocking. The exact `AppointmentType` string values for the AME/IME vs Panel QME
split are looked up during T7.
