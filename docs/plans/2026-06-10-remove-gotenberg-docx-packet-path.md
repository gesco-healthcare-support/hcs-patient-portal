---
status: in-progress
branch: chore/remove-gotenberg-docx-path
date: 2026-06-10
author: Adrian
---

# Remove Gotenberg sidecar + the DOCX packet-template path

Permanently delete the Gotenberg sidecar and the entire DOCX->PDF packet
pipeline. HTML->WeasyPrint becomes the one and only packet renderer. This
is a one-way removal -- no DOCX fallback is retained.

Folded in (approved 2026-06-10): also delete the now-dead cover-sheet +
PDF-merge code (`CoverPageGenerator` / `PacketMergeService`) and the three
PDF packages they were the last consumers of -- `DocumentFormat.OpenXml`,
`PdfSharp`, `PDFsharp-MigraDoc`. QuestPDF STAYS (it renders the live report +
demographics PDFs).

## Why

- **Already unused.** The running `api` has `Packets__HtmlPipeline__{Patient,
  Doctor,Attorney}=true`; all three kinds render via WeasyPrint. The
  `docker-compose.yml` comment (2026-06-08) states the DOCX->Gotenberg
  cutover is complete.
- **Heaviest image.** `main-gotenberg:latest` = 2.47 GB (vs api 649 MB,
  authserver 712 MB, packet-renderer 491 MB, angular 104 MB). A second copy
  exists for the replicate-old-app stack (~5 GB total on disk).
- **Blocks startup.** `api.depends_on.gotenberg: service_healthy` makes every
  boot wait on Gotenberg.
- **Inferior output.** The DOCX path lacks the PQME DWC-QME notice branch
  (that logic lives only in the HTML pipeline), so the "fallback" produces a
  wrong attorney notice for Panel QME. It is a liability, not a safety net.

## Pre-conditions verified (research)

- `DocumentFormat.OpenXml` is referenced in code ONLY by
  `DocxTemplateRenderer.cs` (all other hits are doc-comments or
  auto-generated `packages.lock.json`). Removable after the renderer is
  deleted.
- No `Gotenberg` / `Packets:HtmlPipeline` keys in any `appsettings*.json`
  (config is compose-env-only).
- No Gotenberg references in `.github/` (CI) or in `HttpApi.Host`
  health-check registrations. The only health coupling is the compose
  `depends_on`.
- `PacketTokenMap`, `PacketTokenContext`, `IPacketTokenResolver`,
  `PacketTemplateNames`, `IHtmlPacketRenderer`, `WeasyPrintPacketRenderer`
  are shared/HTML-side -- KEEP all of them.
- `CoverPageGenerator` (MigraDoc) + `PacketMergeService` (PdfSharp, nested
  `MergeInput`) are the ONLY PdfSharp/MigraDoc consumers. Both are
  `ITransientDependency` but have ZERO callers, ZERO injection sites, ZERO
  tests (verified repo-wide via grep on all `*.cs`). Safe to delete outright.
- `QuestPDF` is LIVE (report + demographics PDFs in Application; plus the
  `QuestPDF.Settings.License` line in `CaseEvaluationDomainModule`). KEEP it
  in both Domain.csproj and Application.csproj.

## Change inventory (exhaustive)

### A. Backend code (C#)

1. `Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs`
   - Remove ctor params + fields: `IDocxTemplateRenderer _renderer`,
     `IDocxToPdfConverter _docxToPdfConverter`, `IConfiguration _configuration`.
   - Delete methods `UseHtmlPipeline(...)` and `RenderDocxPacketAsync(...)`.
   - `GenerateKindAsync` always calls `RenderHtmlPacketAsync` (drop the
     `useHtml` branch); simplify the "generated via {Pipeline}" log to HTML.
   - Update the blobName comment (lines ~143-145) that references Gotenberg.
   - Drop now-unused `using`s (Configuration; Templates if only
     EmbeddedTemplateResources used it -- PacketTokenMap keeps Templates alive).
2. `Domain/CaseEvaluationDomainModule.cs`
   - Remove the `AddHttpClient<IDocxToPdfConverter, GotenbergDocxToPdfConverter>`
     registration + the `gotenbergUrl` (`Gotenberg:Url`) read + its comment.
   - Keep the `AddHttpClient<IHtmlPacketRenderer, WeasyPrintPacketRenderer>`
     block (fix the stray `\ Phase 1` comment typo if present).

### B. Delete files

3. `Domain/AppointmentDocuments/Pdf/GotenbergDocxToPdfConverter.cs`
4. `Domain/AppointmentDocuments/Pdf/IDocxToPdfConverter.cs`
5. `Domain/AppointmentDocuments/Templates/DocxTemplateRenderer.cs`
6. `Domain/AppointmentDocuments/Templates/IDocxTemplateRenderer.cs`
7. `Domain/AppointmentDocuments/Templates/EmbeddedTemplateResources.cs`
8. `Domain/AppointmentDocuments/Templates/Resources/PatientPacketNew.docx`
9. `Domain/AppointmentDocuments/Templates/Resources/DoctorPacket.docx`
10. `Domain/AppointmentDocuments/Templates/Resources/AttorneyClaimExaminerPacket.docx`

### C. Project + dependency

11. `Domain/HealthcareSupport.CaseEvaluation.Domain.csproj`
    - Remove `<EmbeddedResource Include="AppointmentDocuments\Templates\Resources\*.docx" />`
      + its comment.
    - Remove `<PackageReference Include="DocumentFormat.OpenXml" Version="3.3.0" />`.
12. `packages.lock.json` (Domain + Application + EntityFrameworkCore +
    HttpApi.Host + AuthServer + DbMigrator) -- regenerate via restore; commit
    the OpenXml removal. (Mechanical, auto-generated.)

### D. Tests

13. `test/.../Domain.Tests/AppointmentDocuments/GenerateAppointmentPacketJobTests.cs`
    - Drop `Renderer` (IDocxTemplateRenderer), `Converter`
      (IDocxToPdfConverter), `Configuration` from `JobFixture` + the job
      ctor args + the `htmlPatient/Doctor/Attorney` flag params.
    - Delete `DoctorFlagOn_RoutesDoctorThroughHtml_OthersThroughDocx`
      (premise removed).
    - Rewrite the 3 lifecycle tests (concurrency-caught, renderer-throws,
      all-succeed) to drive `IHtmlPacketRenderer.RenderAsync` instead of the
      DOCX renderer/converter.
    - Keep + simplify `AttorneyFlagOn_PanelQmeType_RequestsPqmeTemplate` and
      `..._NonPanelType_RequestsAmeImeTemplate` (drop the flag arg; HTML is
      unconditional).

### E. Docker / infra

14. `docker-compose.yml`
    - Delete the `gotenberg:` service block.
    - Remove `api.depends_on.gotenberg`.
    - Remove `api` env: `Gotenberg__Url` (+ comment) and the three
      `Packets__HtmlPipeline__*` lines (+ comment) -- now dead.
15. Delete `docker/Dockerfile.gotenberg-fonts`.
16. `.env.example` -- remove the commented `#GOTENBERG_PORT=...` line.
17. `scripts/worktrees/add-worktree.sh` -- remove `GOTENBERG_PORT=$GOTENBERG`
    (line ~95) and the `$GOTENBERG` port-base computation above it.

### F. Docs

18. `Domain/AppointmentDocuments/CLAUDE.md` -- update the file table +
    "PDF replaces DOCX" section + gotenberg gotcha to describe the
    HTML/WeasyPrint sidecar path.
19. `docs/runbooks/DOCKER-DEV.md` -- "nine containers" -> "eight"; remove the
    `gotenberg` service-table row.
20. Add `docs/decisions/016-remove-gotenberg-html-only-packets.md` (ADR:
    context, decision, alternatives, consequences).
21. This plan doc.
22. Leave dated historical docs (other `docs/plans/2026-06-0x-*`,
    `docs/feedback-research/`, `docs/parity-research/`, runbook findings)
    as-is -- point-in-time records.

### G. Dead cover-sheet / PDF-merge code (PdfSharp + MigraDoc)

23. Delete `Domain/AppointmentDocuments/CoverPageGenerator.cs` (MigraDoc).
24. Delete `Domain/AppointmentDocuments/PacketMergeService.cs` (PdfSharp;
    includes the nested `MergeInput` class -- no separate file).
25. `Domain.csproj` -- remove `<PackageReference Include="PdfSharp" .../>`
    and `<PackageReference Include="PDFsharp-MigraDoc" .../>`.
26. `Domain.Shared/AppointmentDocuments/PacketGenerationStatus.cs` -- the
    `Failed` doc-comment says "caught a PdfSharp / IO exception"; reword to
    "caught a render / IO exception" (drop the stale PdfSharp reference).
27. `GenerateAppointmentPacketJob.cs` docstring -- the "cover-sheet PDF
    generator and PdfSharp merge service are kept" prose is removed as part
    of commit 1's docstring rewrite.

## Atomic commits (ordered; each compiles + tests pass)

1. **refactor(packets): render packets via HTML only, drop DOCX path**
   Inventory A, B, C, D + F#18 (CLAUDE.md). The job docstring rewrite here
   also drops the stale cover-sheet/merge prose (#27).
   Ordering: must land before the docker commit so the code stops defaulting
   to DOCX before the sidecar/flags are removed.
2. **chore(packets): remove dead cover-sheet + PDF-merge code**
   Inventory G (CoverPageGenerator, PacketMergeService, PdfSharp +
   PDFsharp-MigraDoc, PacketGenerationStatus comment). Independent of
   commit 1; both build standalone.
3. **chore(docker): remove the gotenberg sidecar**
   Inventory E + F#19 (DOCKER-DEV.md).
4. **docs(packets): record HTML-only packet decision**
   Inventory F#20 (ADR 016) + F#21 (this plan).

Lock-file note: commits 1 and 2 each remove NuGet packages, so each must
regenerate `packages.lock.json` (Domain + Application + EntityFrameworkCore +
HttpApi.Host + AuthServer + DbMigrator) via `dotnet restore --force-evaluate`
before committing -- locked-mode restore fails on a stale lock.

## Verification (before PR)

1. `docker compose build --no-cache api` -> exit 0; confirm publish RAN and
   the build no longer references OpenXml/Gotenberg.
2. `dotnet test --filter GenerateAppointmentPacketJobTests` (plain
   xUnit/NSubstitute, no stack needed) -> green.
3. `docker compose up -d` -> NO gotenberg container; `api` reaches healthy
   without a gotenberg dependency.
4. Live: regenerate A00016 (or approve a fresh PQME) -> all 3 packets
   generate "via HTML", no errors, no Gotenberg network calls.
5. `grep -riE 'gotenberg|PdfSharp|MigraDoc|DocumentFormat.OpenXml|IDocxToPdf|DocxTemplateRenderer|CoverPageGenerator|PacketMergeService' src docker docker-compose.yml .env.example scripts`
   -> no live references remain (only historical dated docs).
6. Confirm `dotnet restore --force-evaluate` produced clean lock diffs and
   the `--no-cache` api build (step 1) succeeded with the new lock files --
   that build is the backstop for a stale or wrong lock.

## Rollback

Revert the 3 commits (or the squash-merge commit). Re-adds the gotenberg
service + the DOCX path. Intentionally one-way per the go-live decision.

## Risks / notes

- OpenXml + PdfSharp + MigraDoc removal touches the `packages.lock.json` of
  Domain + 5 dependent projects, across two commits. Regenerate with
  `dotnet restore --force-evaluate` per package-removal commit; review the
  lock diffs as mechanical.
- `replicate-old-app` worktree still references gotenberg in ITS own
  `docker-compose.yml` (separate branch) -- out of scope; it inherits this
  change when that branch takes main. Its 2.47 GB image can be pruned then.
- Local `docker/fonts/` (gitignored) + the two `*-gotenberg:latest` images
  (~5 GB) can be pruned after merge -- local cleanup, not a repo change.

## Out of scope

- `QuestPDF` -- LIVE (report + demographics PDFs). Not touched.
- `replicate-old-app` worktree's own `docker-compose.yml` (separate branch)
  -- inherits this change when it next takes main; its 2.47 GB gotenberg
  image can be pruned then.
