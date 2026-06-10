---
feature: doctor-packet-pipeline-integration
date: 2026-06-04
status: draft
base-branch: main
related-issues: []
predecessor: docs/plans/2026-06-02-doctor-packet-html-template.md
---

## Goal

Wire the completed HTML->WeasyPrint fillable-PDF template into the application so the **Doctor
packet** is *generated* as a fillable PDF from our HTML template (replacing the DOCX -> Gotenberg
flat-PDF path), behind a feature flag with the existing pipeline as the instant fallback. Scope is
**generation only**: the Doctor packet stays download-only (it is not emailed today and that does not
change), and **round-trip ingestion is explicitly deferred** until separately requested. Design every
new piece to be packet-agnostic so the Patient and Attorney packets can migrate later with minimal
rework.

## Context

- **Predecessor delivered:** an 8-page fillable Doctor packet built by `build_doctor.py`
  (`.tmp-packet-inspect/doctor-template/`), rendered by WeasyPrint `--pdf-forms`, finalized by
  `post_process.py` (pikepdf). 1298 AcroForm fields named `packet.doctor.<section>.*`; 15 pre-fill
  tokens `##Group.Field##`; verified for fidelity, field-naming, token coverage, and a synthetic
  round-trip. The predecessor plan (Decision 3) deferred token wiring + the DOCX->HTML switch to "a
  later integration phase" -- this plan is that phase (generation half).
- **Current production pipeline (DOCX -> Gotenberg), as mapped:**
  1. Appointment **Approved** -> `AppointmentStatusChangedEto` -> `PacketGenerationOnApprovedHandler`
     enqueues a Hangfire job (deferred to `UoW.OnCompleted`).
  2. `GenerateAppointmentPacketJob` fans out to 3 kinds: **Patient, Doctor, AttorneyClaimExaminer**.
  3. Loads embedded DOCX via `EmbeddedTemplateResources.LoadTemplate(kind)` (`DoctorPacket.docx`).
  4. `DocxTemplateRenderer.Render(bytes, context)` regex-replaces `##Group.Field##`; context from
     `PacketTokenResolver.ResolveAsync(appointmentId)` (14 repositories).
  5. `GotenbergDocxToPdfConverter.ConvertAsync` -> `POST http://gotenberg:3000/forms/libreoffice/convert`
     (interface `IDocxToPdfConverter`; DI in `CaseEvaluationDomainModule.cs:96`; config `Gotenberg:Url`).
  6. Stored in MinIO: `{tenant}/{appointmentId}/packet/{kind}/{guid}.pdf`; row marked `Generated`
     (unique index `(TenantId, AppointmentId, Kind)`).
  7. Served: **Patient + Attorney packets are emailed**; the **Doctor packet is generate-store-download
     only** (`AppointmentPacketsAppService.DownloadByKindAsync`). The Doctor cutover therefore touches
     no email path -- lowest blast radius.
- **Research confirmed (favorable):** all 15 template tokens have data sources in `PacketTokenResolver`;
  the token MAP + resolver are **format-agnostic** (string replacement -- only the DOCX *walker* is
  format-specific), so HTML substitution is a plain regex replace and reusing the resolver guarantees
  pre-fill parity; Gotenberg is a Docker sidecar reached via a typed `HttpClient`, so a WeasyPrint
  sidecar mirrors it exactly.
- **Decisions taken (this plan):** Doctor first + design for all 3; **generation only -- no email
  changes, ingestion deferred**; feature-flag per-kind cutover with DOCX->Gotenberg as the default
  fallback.

## Approach

### A. Packet-renderer service (Python, HTTP sidecar)
Promote the current CLI (`docker run ... weasyprint`) to a small long-running HTTP service so the
.NET app can call it from the Hangfire job, mirroring how it calls Gotenberg:
- `POST /render` -- body = token-substituted HTML; returns a **fillable PDF** (runs
  `weasyprint --pdf-forms` then the existing `post_process.py` harvest/fix). Stateless.
- `GET /health` -- for the compose healthcheck.
- (A `/extract` endpoint for reading filled PDFs is **deferred** with ingestion.)
- Built from the existing `Dockerfile.weasyprint` (Debian + Pango + Carlito/Liberation fonts +
  weasyprint + pikepdf) plus a ~40-line Flask/WSGI wrapper and `post_process.py`. Relocate to
  `docker/packet-renderer/` (Dockerfile + `app.py` + `post_process.py`). Add a `packet-renderer`
  service to `docker-compose.yml` (port 5000, healthcheck `/health`); `api` depends on it
  `service_healthy`, parallel to `gotenberg`.
- The service is **template-agnostic**: it renders whatever HTML it is given. `build_doctor.py` is a
  *build-time* generator, NOT a runtime dependency of the service.

### B. .NET generation path (mirror the DOCX path; reuse the resolver)
- **New abstraction `IHtmlToPdfConverter`** (`ConvertAsync(string html) -> byte[]`), impl
  `WeasyPrintHtmlToPdfConverter` (typed `HttpClient`, base = `WeasyPrint:Url`, fallback
  `http://packet-renderer:5000`, POST `/render`). DI in `CaseEvaluationDomainModule` mirroring the
  Gotenberg registration. `IDocxToPdfConverter` untouched.
- **Shared token-map helper:** extract the `PacketTokenContext` -> `##Group.Field##` map builder out
  of `DocxTemplateRenderer` into a shared helper so HTML and DOCX renderers produce **identical**
  token values (same uppercasing, `MM/dd/yyyy` dates, multi-row InjuryDetails concatenation),
  guaranteeing pre-fill parity.
- **New `HtmlTemplateRenderer.Render(htmlTemplate, context)`** -- regex-replace tokens in the HTML
  string using the shared map (no XML walking; HTML is plain text).
- **Embedded template:** `DoctorPacket.html` becomes an embedded resource beside the DOCX in
  `AppointmentDocuments/Templates/Resources/`. The DWC logo is **inlined as a base64 data URI** by
  `build_doctor.py` so the HTML is self-contained (no file dependency at render time).
- **Job routing + feature flag:** `GenerateAppointmentPacketJob` chooses the pipeline per kind. With
  the flag set to `html`, the Doctor kind: load `DoctorPacket.html` -> `HtmlTemplateRenderer.Render`
  -> `IHtmlToPdfConverter.ConvertAsync` -> store at the **same** blob path / status / event. Flag
  `docx` (default) and the Patient/Attorney kinds: existing DOCX->Gotenberg, unchanged. Flag = config
  key `Packets:DoctorPipeline` (`docx` default | `html`).

### C. Template toolchain in the repo (retire the scratch dir)
- `build_doctor.py` + dev `render.sh` + `check_fields.py` -> `tools/packet-templates/doctor/`
  (build-time source of truth). `post_process.py` lives with the **service**
  (`docker/packet-renderer/`) and is referenced by dev tooling, so there is one copy.
- `build_doctor.py` change: inline the DWC logo as base64 in the generated HTML (self-contained).
- A **CI/build check** regenerates `DoctorPacket.html` and fails if it differs from the committed
  embedded resource (prevents generator/artifact drift).

### D. Email + ingestion -- explicitly out of scope (this phase)
- **No email changes.** The Doctor packet remains generate-store-download only. Patient/Attorney email
  handlers and routing are untouched; "send the correct packet in the email" is covered here purely as
  a **regression check** (those emails must still attach the correct packets after our changes).
- **Round-trip ingestion deferred.** The `packet.doctor.*` field names already enable it; when
  requested, a future plan adds the service `/extract` endpoint, an upload/return path, and
  persistence. Open questions (return mechanism, normalization depth) are parked until then.

### E. Extensibility for Patient / Attorney (design only, build later)
`IHtmlToPdfConverter`, `HtmlTemplateRenderer`, the shared token map, and the renderer service are
packet-agnostic. Migrating Patient/Attorney later = build their HTML generators, embed their HTML,
add their kinds to the flag (plus a per-packet fillable-vs-flat decision since those are emailed).
Out of scope to build here; the interfaces make it additive.

## Tasks

**Phase 0 -- foundations**
- **T1: Relocate the template toolchain into the repo.**
  - approach: code
  - files-touched: [tools/packet-templates/doctor/{build_doctor.py, render.sh, check_fields.py},
    docker/packet-renderer/post_process.py,
    src/.../AppointmentDocuments/Templates/Resources/DoctorPacket.html]
  - acceptance: `build_doctor.py` emits self-contained HTML (base64 logo); the embedded
    `DoctorPacket.html` renders to a fillable PDF byte-equivalent to the verified scratch `doctor.pdf`
    (1298 fields, page-1 radio fix intact); scratch `.tmp-packet-inspect/doctor-template` retired.

- **T2: Packet-renderer HTTP service (`/render` + `/health`).**
  - approach: test-after
  - files-touched: [docker/packet-renderer/{Dockerfile, app.py, post_process.py}, docker-compose.yml]
  - acceptance: `/render` returns a fillable PDF with the 1298 `packet.doctor.*` fields; `/health`
    green; `api` waits on `packet-renderer:service_healthy`; service starts in the compose stack.

**Phase 1 -- generation cutover (Doctor), behind a flag**
- **T3: Shared token-map helper + `HtmlTemplateRenderer`.**
  - approach: tdd  (pure string/token logic)
  - files-touched: [src/.../AppointmentDocuments/Templates/{TokenMapBuilder.cs (extracted),
    HtmlTemplateRenderer.cs, DocxTemplateRenderer.cs (refactor to use the shared builder)}]
  - acceptance: for a fixed `PacketTokenContext`, HTML and DOCX renderers produce identical token
    values; all 15 Doctor tokens substitute; unresolved tokens handled per existing DOCX behavior.

- **T4: `IHtmlToPdfConverter` + `WeasyPrintHtmlToPdfConverter` + DI/config.**
  - approach: test-after
  - files-touched: [src/.../AppointmentDocuments/Pdf/{IHtmlToPdfConverter.cs,
    WeasyPrintHtmlToPdfConverter.cs}, src/.../CaseEvaluationDomainModule.cs,
    appsettings + docker-compose env (`WeasyPrint__Url`)]
  - acceptance: converter posts HTML to `/render` and returns PDF bytes; URL from `WeasyPrint:Url`
    with the compose fallback; ~30s timeout; transport errors propagate for Hangfire retry.

- **T5: Route the Doctor kind through the HTML pipeline behind the flag.**
  - approach: code
  - files-touched: [src/.../AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs, config]
  - acceptance: flag `html` -> Doctor packet generated via HTML pipeline, stored at the same blob
    path/status/event; flag `docx` (default) -> unchanged; Patient/Attorney always DOCX->Gotenberg;
    both branches covered by a job-level test.

- **T6: End-to-end generation verification + staging flip.**
  - approach: code (manual verification gate)
  - acceptance: for sample appointments, the HTML Doctor PDF (a) resolves the same token values as the
    DOCX output, (b) is fillable in Acrobat + Edge, (c) stores + downloads via the existing API;
    **regression:** Patient + Attorney generation AND their emails are unchanged (correct packets
    still attached); flag flipped to `html` in staging with DOCX fallback proven by toggling back.

**Phase 2 -- round-trip ingestion: DEFERRED** (separate plan when requested).

**Phase 3 -- extension (design only)**
- **T7: Document the Patient/Attorney migration path** (HTML generators, embed + flag, fillable-vs-flat
  + email decision per packet). approach: code (docs).

## Risk / Rollback

- **Blast radius:** new sidecar + new code paths, but the Doctor kind is **not emailed**, so cutover
  cannot affect email flows; Patient/Attorney are untouched; flag default `docx` keeps production
  behavior identical until explicitly flipped. No inbound/PHI-ingestion surface added this phase.
- **Rollback:** set `Packets:DoctorPipeline=docx` -> instant return to DOCX->Gotenberg (pipelines
  coexist).
- **Known risks + mitigations:**
  - *New infra dependency (packet-renderer down):* healthcheck; stay on / fall back to the DOCX
    pipeline; Hangfire retry on transport errors.
  - *Token parity drift HTML vs DOCX:* both use the same extracted token-map builder (T3), verified
    equal in tests.
  - *Generator/artifact drift:* CI regenerates `DoctorPacket.html` and fails on mismatch.

## Verification

1. Field inventory of the generated Doctor PDF matches the `packet.doctor.*` scheme (1298 fields, no
   mangled names, page-1 radios clean).
2. Token parity: for the same appointment, HTML-pipeline token values equal DOCX-pipeline values.
3. Fillable + correct in Acrobat and Edge; stored at the existing blob path; downloadable via the API.
4. Feature flag toggles cleanly both ways.
5. **Email regression:** Patient and Attorney packets still generate and email with the correct
   attachments; the Doctor packet remains download-only (no email emitted).

## Decisions

**Resolved (2026-06-04):**
1. **Scope = Doctor first, design for all 3.** Build the Doctor generation cutover now; keep
   interfaces/service packet-agnostic so Patient/Attorney migrate later additively.
2. **Generation only -- no email changes.** The Doctor packet stays generate-store-download-only;
   Patient/Attorney email routing is untouched (verified as a regression).
3. **Round-trip ingestion is deferred** until separately requested; field names already enable it.
4. **Feature-flag per-kind cutover**, DOCX->Gotenberg remains the default/fallback until verified.
5. **Reuse the existing token resolver/map** (format-agnostic) via an extracted shared builder, to
   guarantee HTML/DOCX pre-fill parity.
6. **Renderer service owns PDF rendering** via WeasyPrint + pikepdf (`/render` now; `/extract` with
   the deferred ingestion phase); `build_doctor.py` stays a build-time generator.

**Open (parked with the deferred ingestion phase):** return path for filled PDFs; ingestion
normalization depth; Patient/Attorney fillable-vs-flat output on migration.
