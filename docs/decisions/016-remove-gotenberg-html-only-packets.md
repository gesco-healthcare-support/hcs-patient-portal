# ADR-016: HTML-only packet rendering; remove Gotenberg + the DOCX path

- Status: Accepted
- Date: 2026-06-10
- Supersedes the DOCX-rendering portion of ADR-010 (PDF packets replace DOCX)

## Context

Appointment packets (Patient, Doctor, Attorney/Claim-Examiner) were originally
rendered by filling embedded `.docx` templates with OpenXml and converting them
to PDF via a **Gotenberg** sidecar (LibreOffice). A second pipeline -- HTML
templates rendered by the **WeasyPrint** `packet-renderer` sidecar -- was added
in a staged, per-kind, flag-gated cutover (`Packets:HtmlPipeline:*`).

By 2026-06-08 the cutover was complete: all three kinds defaulted to the HTML
pipeline in every environment. The DOCX path was left wired only as a fallback,
but it was both unused and inferior -- it never grew the PQME DWC-QME notice
branch, which exists only in the HTML templates.

Costs of keeping the dead path:

- The `gotenberg` image is 2.47 GB (LibreOffice + MS Core Fonts) -- by far the
  heaviest in the stack -- duplicated per worktree.
- `api` had `depends_on: gotenberg: service_healthy`, so every boot waited on it.
- Dead C# (`DocxTemplateRenderer`, `GotenbergDocxToPdfConverter`, embedded
  `.docx` templates) plus the `DocumentFormat.OpenXml` dependency, and the
  separately-dead cover-sheet/merge code (`CoverPageGenerator`,
  `PacketMergeService`) on `PdfSharp` / `PDFsharp-MigraDoc`.

## Decision

Make the WeasyPrint `packet-renderer` sidecar the **sole** packet renderer and
remove the DOCX path and Gotenberg entirely:

- `GenerateAppointmentPacketJob` renders HTML unconditionally (no flags).
- Delete the DOCX renderer/converter/templates and the `Packets:HtmlPipeline:*`
  flags; delete the dead cover-sheet/merge code.
- Remove the `DocumentFormat.OpenXml`, `PdfSharp`, and `PDFsharp-MigraDoc`
  packages.
- Remove the `gotenberg` compose service, its Dockerfile, and the api's
  dependency on it.

QuestPDF (report + demographics PDFs) is unaffected and retained.

## Alternatives considered

- **Keep Gotenberg as a fallback.** Rejected: it is unused, ships a wrong PQME
  notice, and carries 2.47 GB + a startup dependency for zero benefit.
- **Keep the code, drop only the container.** Rejected: the code defaults to the
  DOCX path, so the api would fail to render if a flag were ever flipped while
  the sidecar was gone.

## Consequences

- One-way: there is no DOCX fallback. Reverting requires reverting this change.
- Smaller image set + faster `compose up` (no 2.47 GB image, no gotenberg
  healthcheck gate). Leaner Domain assembly (no embedded `.docx`, fewer deps).
- The packet-renderer sidecar is now a hard single point of failure for packet
  generation; its transport failures still propagate to Hangfire for retry.
