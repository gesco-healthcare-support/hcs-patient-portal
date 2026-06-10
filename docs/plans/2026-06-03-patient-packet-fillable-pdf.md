---
feature: patient-packet-fillable-pdf
date: 2026-06-03
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Recreate the Patient packet (`PatientPacketNew.docx`, re-baselined 2026-06-03: 15 content pages) as a faithful, fillable PDF **template** -- built page-by-page from a single HTML/CSS generator rendered by WeasyPrint (`--pdf-forms`) and finalized by a pikepdf post-process -- where the 19 `##Group.Field##` token spots are preserved exactly (pre-fill + lock later) and the blank intake fields become editable controls, with manual fidelity + regression verification after each page.

## Context

- The Patient packet is a legal medical-evaluation document. Layout, wording, bilingual (EN/ES) content, and every token position carry legal/clinical meaning, so reproduction must match the original EXACTLY; when a marker's meaning, options, or layout is unclear, STOP and ASK rather than approximate.
- RE-BASELINE 2026-06-03: the user edited the template -- removed pages and changed the order. New structure (from a fresh Gotenberg render `original.pdf` + docx scan): 1393 paragraphs, 10 tables, 904 cells, 18 section breaks, 2 body-diagram images, **zero existing form controls**, **19 distinct tokens / 40 occurrences**, 5 cosmetic yellow highlights. The render is **16 pages = 15 content + 1 trailing EMPTY page** (a Word artifact the user cannot delete; dropped in the HTML). Changes from the prior template: cover letter moved to the FRONT (page 1); the Epworth Sleepiness Scale, QME Form 122 Declaration, and Service List pages were REMOVED.
- Verified structure (prior template, for reference): 1584 paragraphs, 13 tables, 956 cells, 19 section breaks, 44 distinct tokens / 81 occurrences.
- Control palette needed: text, multi-line text, checkbox, single-select "circle one" matrices, 0-10 circle-the-choice scales, plus one freeform body-diagram pain map. This matches the Doctor packet's palette, so the proven Doctor toolchain is reused.
- ADR-010 chose flat PDF for immutability. This task is a deliberate, scoped shift to a two-way fillable doc: pre-filled token data stays immutable by being rendered as **page text** (not a field), preserving the ADR's intent for the locked layer while making blanks editable.
- This work does NOT touch the live DOCX -> Gotenberg packet pipeline; it produces new template artifacts in parallel. Pre-fill wiring, round-trip ingestion, and switching the app to the new template are LATER phases (out of scope here); field names + token tracking are designed so they drop in cleanly.

## Approach

**Pipeline (proven on the Doctor packet):** `build_patient.py` (generator) -> `patient.html` -> `weasyprint --pdf-forms` -> `patient.pdf` -> `post_process.py` (pikepdf) -> final fillable PDF.

- **Renderer:** WeasyPrint (BSD, $0) in a Docker sidecar (`Dockerfile.weasyprint`) with free metric-compatible fonts (Carlito = Calibri, Liberation = Arial/Times).
- **Finalize:** `post_process.py` (pikepdf) -- circle-the-choice "highlight-the-choice" link-harvest, deselectable radios, checkbox marks (reused as-is).
- **Why this approach (confirmed by independent research):** it is the only **$0, automatable, version-controlled** path that emits a genuine fillable AcroForm from HTML. Field names come straight from the HTML `name` attribute (verbatim), and radio grouping is correct as of WeasyPrint v63+ -- ideal for round-tripping values back to the DB by field name. Source: WeasyPrint docs (doc.courtbouillon.org/weasyprint) + `anchors.py` (Kozea/WeasyPrint) -- HIGH.
- **Rejected alternatives (with reasons):**
  - *Chromium / Gotenberg-Chromium*: **flattens** HTML form fields into static content -- no AcroForm (Puppeteer #3646) -- HIGH.
  - *Overlay AcroForm widgets onto the existing flat PDF at coordinates*: perfect layout fidelity for free, but placing ~300 fields by coordinate across 18 pages is laborious and breaks on any template/data reflow; poor maintainability/version-control.
  - *DOCX-copy + Word form fields -> LibreOffice export*: reuses the current engine and original layout, but hand-adding hundreds of Word controls is non-diffable (binary), cannot cleanly express the 0-10 circle-the-choice scales, and LibreOffice's AcroForm export fidelity is unproven.
  - *PDFreactor / IronPDF / iText commercial*: do HTML->fillable AcroForm but cost money (~$2.9k / ~$749+ / quote) or are AGPL copyleft -- HIGH.

**File organization (anti-confusion rule):**
- **One generator = source of truth.** `build_patient.py` contains **one function per logical page**; there are **no per-page `.html` files**.
- **One output HTML** (`patient.html`) -- a build artifact, regenerated, never hand-edited.
- **Working directory:** build + fully verify in the clean, untracked `.tmp-packet-inspect/patient-template/` (already holds `PatientPacketNew.docx`, the structure inspector `inspect.py`, and the rendered original for comparison). The feature branch is created and final artifacts placed in the repo only AFTER the whole packet works.
- **Shared tooling:** COPY the Doctor effort's reusable tooling into `.tmp-packet-inspect/patient-template/shared/` (`post_process.py`, `Dockerfile.weasyprint`, `check_fields.py`, a `render.sh`) so the patient build is self-contained and the in-flight Doctor effort is untouched. Consolidation into one packet-common `shared/` is a follow-up when both packets land in the repo.

**Fidelity rule:** reproduce the original's structure, wording (EN + ES verbatim), and markers EXACTLY -- no shortcuts, no guessing. Pause and ask when unclear.

**Field-control conventions (clinically sensible):**
- **Text field:** blank lines -- Date/To, names, addresses, SSN, signatures (blank line), "Where?" lines, sum-score lines, emergency-contact, interpreter, free write-ins.
- **Multi-line text (textarea):** narrative boxes -- job duties, disability description, employee/employer comments (pages 12-14).
- **Checkbox (independent/multi):** Yes/No questions, "check one" doctor-selection, job-requires a-j, present-complaints Worse/Same/less.
- **Circle-the-choice (single-select ordinal; link-harvest soft-yellow highlight, deselectable):** the 0-10 AMA scales (pages 7-9), ADL 4-column matrix (page 1), Epworth 4-column (page 10), job-frequency matrix (page 13: Never/Occasionally/Frequently/Constantly), lifting/carrying matrix (page 14). Options stay printed; selection draws a translucent highlight. Reuses the Doctor mechanism for cross-packet consistency.
- **Static text:** labels, headers, all legal/privacy text (page 3 is fully static), and resolved `##tokens##` (pre-filled -> locked).
- **Body diagram (page 6):** the BACK/FRONT body images stay a **static canvas**; the document permits markup so the patient can draw the pain map as a **PDF ink annotation** (viewer-dependent, NOT read back to the DB -- accepted trade-off). The page's discrete controls (Yes/No boxes, "Where?" lines, the 0-10 scale, signature) DO become round-trippable fields.

**Pre-fill scope (resolved):** pre-fill ONLY the 44 tokenized spots. Untokenized blanks that happen to duplicate known data (CA DWC forms pages 11-15, Patient Information/Update page 15) stay blank + editable, exactly as the source -- no pre-fill, no lock, to preserve fidelity and avoid attesting state-form fields on the patient's behalf.

**Round-trip field naming:** hierarchical, stable, ASCII -- `packet.patient.<page>.<section>.<field>[.<index>]` (e.g. `packet.patient.adl.selfcare.dress_shoes`, `packet.patient.ama.pain.severity_now`). Set as the HTML control `name`; WeasyPrint preserves it verbatim as the AcroForm field name. This name set is a FROZEN CONTRACT from page 1; the later .NET ingestion phase reads it via PdfPig (Apache-2.0, read-only) or FDF/XFDF.

**Token pre-fill marking (must not be lost):** every pre-fill spot keeps its EXACT `##Group.Field##` text wrapped in `<span class="tok">`, never paraphrased or dropped. The generator emits a token inventory (`patient-tokens.txt`: token -> page/section) so the later pre-fill phase maps every token precisely. Cosmetic yellow Word highlights (page 16 City/State, etc.) are dropped (no background); the `##...##` token text underneath is preserved exactly.

**Locking design (for the later integration phase, designed-in now):** pre-filled tokens are immutable because they are rendered as page text, not fields. If stronger tamper-evidence is wanted later, wrap the finished PDF in a **DocMDP Level-2 certification** signature (form-fill allowed; structural edits invalidate it). Per-field `ReadOnly`/flatten remain available for any field that must lock post-fill. Out of scope to wire now.

**Page sequence (15 content pages, re-baselined order 2026-06-03):**
1. Falkinstein appointment cover letter (token-heavy, locked) -- MOVED to front
2. Activities of Daily Living Form (4-column difficulty matrix)
3. Release of Medical Records
4. Privacy Policy Statement (fully static)
5. Pregnancy/X-ray Acknowledgement (EN + ES) + Emergency Contact (EN + ES)
6. Acknowledgement of Receipt of Notice of Privacy Practices
7. Present Complaints (body-diagram pain map + pain Q&A, EN + ES)
8. AMA Guidelines 5th Ed -- Pain + Activity Limitation I-II (footer "Page 1 of 4")
9. AMA -- Activity Limitation cont. (footer "Page 2 of 4")
10. AMA -- Effect of Pain on Mood (footer "Page 3 of 3" -- inconsistent, see Decisions)
11. CA DWC Employee's Disability Questionnaire (barcode + identity blanks)
12. Disability Questionnaire cont. (claim numbers, doctor-selected check-one, narrative text areas)
13. CA DWC Description of Employee's Job Duties (activity-frequency matrix)
14. Job Duties cont. (lifting/carrying matrix, job-requires a-j, comments, signatures)
15. Patient Information/Update

REMOVED by the 2026-06-03 edit: Epworth Sleepiness Scale, QME Form 122 Declaration, Service List.
DROPPED in HTML: the trailing EMPTY 16th render page (Word artifact).

## Tasks

Per-page build/verify loop (applies to every page task T2-T16):
1. **Study** -- render the original page + dump its tables/structure (via `inspect.py` / `pkspike`-style) to map exact layout, wording (EN+ES), fields, and token positions.
2. **Build** -- add/extend the page function in `build_patient.py` per the conventions above.
3. **Generate+render+finalize** -- `render.sh` -> `patient.pdf` (all pages so far).
4. **Self-check** -- field count, no overflow (each logical page = exactly one PDF page), names follow `packet.patient.*`, tokens exact, quick visual diff vs original.
5. **Hand off** -- you open `patient.pdf` in Acrobat/Edge: verify the NEW page (layout vs original + click every control type) AND regression-check all PRIOR pages.
6. Fix per feedback; re-render; re-verify.
7. **Sign-off -> next page.** (STOP for approval after each page.)

- **T1: Confirm page inventory + stand up the toolchain.**
  - approach: code
  - files-touched: [.tmp-packet-inspect/patient-template/build_patient.py, .tmp-packet-inspect/patient-template/shared/{post_process.py, Dockerfile.weasyprint, render.sh, check_fields.py}]
  - acceptance: render the original `PatientPacketNew.pdf` and list every logical page + its sections + token positions; copy shared tooling into `patient-template/shared/`; confirm the WeasyPrint Docker image; extract the 2 body-diagram images (`image1`=FRONT, `image2`=BACK) from the docx; `render.sh` produces an empty-shell `patient.pdf`; emit `patient-tokens.txt` (all 44 tokens, page-mapped). No page content yet.
  - T1 FINDING (plan correction): the docx has only 2 raster images (the body diagrams). The page-11 **barcode is vector Code-39 art** and the **letterhead is "Footlight MT Light"** (proprietary serif, not in the renderer; no free equivalent) -- neither is extractable from the docx. DECISION: reproduce the barcode (p11) and letterhead (p4, p16) by cropping them from the original render as high-DPI static images (the only pixel-faithful option for "replicate exactly"); blank-line rules become CSS borders / field underlines. Tight crops are produced when those pages are built (T12, T17, T5).

- **T2: Page 1 -- Falkinstein appointment cover letter.** approach: code - files: [build_patient.py] - acceptance: letterhead reproduced as exact image crop; all 15 tokens verbatim + locked-as-text (City/State yellow-highlight backgrounds dropped); centred location block + footer + body wording verbatim; one PDF page; you verify. (BUILT -- awaiting verification.)
- **T3: Page 2 -- Activities of Daily Living.** approach: code - files: [build_patient.py] - acceptance: header tokens (NAME/ACCT/DATE) exact; 7 activity sections + all rows reproduced; 4-column difficulty matrix renders as one circle-the-choice (single-select) per row; one PDF page; you verify. (Generator already drafted as `page_adl`.)
- **T4: Page 3 -- Release of Medical Records.** approach: code - files: [build_patient.py] - acceptance: static West Coast Spine block exact; 5 tokens (name/DOB/DOI/SSN) exact + locked-as-text; Date/To/Signature blanks editable; you verify.
- **T5: Page 4 -- Privacy Policy Statement.** approach: code - files: [build_patient.py] - acceptance: fully static text reproduced verbatim; zero fields; one PDF page; you verify.
- **T6: Page 5 -- Pregnancy/X-ray Acknowledgement + Emergency Contact.** approach: code - files: [build_patient.py] - acceptance: EN + ES text verbatim; LMP/Signature/Date + emergency (Name/Relationship/Phone 1 & 2) editable; Falkinstein letterhead matched; you verify.
- **T7: Page 6 -- Acknowledgement of Receipt of Notice of Privacy Practices.** approach: code - files: [build_patient.py] - acceptance: name token + date token exact; patient/representative signature + relationship blanks editable; you verify.
- **T8: Page 7 -- Present Complaints (body diagram).** approach: code - files: [build_patient.py, shared/post_process.py] - acceptance: BACK/FRONT body images static; pain legend + 0-10 scale reproduced; markup permitted for the ink pain-map; numbered Q&A (Worse/Same/less checkboxes, Yes/No + "Where?" lines, write-ins) editable; EN + ES verbatim; signature/date editable; you verify the ink layer works in Acrobat.
- **T9: Page 8 -- AMA Pain + Activity Limitation I-II.** approach: code - files: [build_patient.py] - acceptance: every "circle a number" 0-10 row is a circle-the-choice; sum-score lines editable; "Page 1 of 4" footer (verbatim); one PDF page; you verify.
- **T10: Page 9 -- AMA Activity Limitation (cont.).** approach: code - files: [build_patient.py] - acceptance: 0-10 scales with anchor labels exact; sum-score; "Page 2 of 4" (verbatim); you verify.
- **T11: Page 10 -- AMA Effect of Pain on Mood.** approach: code - files: [build_patient.py] - acceptance: 0-10 scales; sum-score; name token + signature + date token; "Page 3 of 3" footer (verbatim -- inconsistent in source, see Decisions); you verify.
- **T12: Page 11 -- CA DWC Disability Questionnaire (identity).** approach: code - files: [build_patient.py] - acceptance: barcode reproduced as exact image crop + state header; First/Last/MI/SSN/addresses/City/State/Zip/DOB/DOI/Employer blanks editable (NOT pre-filled); gov-form layout matches; you verify.
- **T13: Page 12 -- Disability Questionnaire (cont.).** approach: code - files: [build_patient.py] - acceptance: Claim Number 1-5 lines; "check one" doctor-selection checkboxes; narrative textareas (job duties/disability/work-effect/describe) editable; date/signature; you verify.
- **T14: Page 13 -- CA DWC Description of Job Duties.** approach: code - files: [build_patient.py] - acceptance: employee/employer/job header blanks; description textarea; activity-frequency matrix (Never/Occasionally/Frequently/Constantly) as circle-the-choice per row incl. Hand-Use Right/Left; you verify.
- **T15: Page 14 -- Job Duties (cont.).** approach: code - files: [build_patient.py] - acceptance: lifting/carrying matrix (with Height/Distance text cells) exact; job-requires a-j Yes/No checkboxes + describe lines; employee/employer comment textareas; contact/signature/date cells; you verify.
- **T16: Page 15 -- Patient Information/Update.** approach: code - files: [build_patient.py] - acceptance: all blanks editable (date/name/DOB/SSN/phone/address/email, attorney firm/name/address/phone, emergency number/relationship, interpreter name/cert#); NOT pre-filled; you verify.
- **T17: Whole-document finalize.** approach: code - files: [build_patient.py, patient.html, patient.pdf, patient-tokens.txt] - acceptance: full 15-page `patient.pdf` renders (no trailing empty page); **field-name inventory + token inventory** exported and reviewed; final end-to-end pass in Acrobat + Edge; then create the feature branch + place final artifacts in the repo.

## Risk / Rollback

- **Blast radius:** isolated to the new template artifacts in `.tmp-packet-inspect/patient-template/`. The live DOCX -> Gotenberg pipeline is untouched, so nothing in production breaks during the build.
- **Per-page gate** prevents compounding errors: a page is not accepted until you verify it AND all prior pages.
- **Rollback:** delete the working directory / discard the branch; the existing DOCX pipeline remains the source of truth until a separate, later integration phase explicitly switches the Patient kind to the new template.
- **Known engine risks + mitigations:** WeasyPrint sizes form controls oddly in `px` -> size in `pt`; weak flexbox -> CSS tables; dense matrices (ADL, AMA 0-10, lifting/carrying) -> generator loops + `table-layout:fixed`; form-field rendering is reader-dependent (verify in Acrobat AND Edge); adding form fields can bloat file size via font embedding (WeasyPrint #2119) -> watch size. Highest-fidelity-risk pages: the CA DWC government forms (11-14) and the bilingual pages (4, 6, 10).
- **Accepted limitation:** the page-6 body-diagram pain map is captured as a viewer-dependent ink annotation, not structured DB data.

## Verification

After all pages, open the full `patient.pdf` in **Adobe Acrobat and Microsoft Edge** and confirm, page by page against the original `PatientPacketNew.pdf`:
1. **Fidelity** -- layout, labels, EN+ES wording, section structure, gov-form layout, images (body diagrams, barcode), and token positions match (deviations limited to agreed ones: dropped yellow highlight backgrounds).
2. **Field behavior** -- text fields fill their cells; checkboxes mark and print clearly; circle-the-choice highlights the selected option, allows one per row, and can be deselected; `##tokens##` render as static text.
3. **No overflow** -- each logical page is exactly one PDF page (no blank pages).
4. **Round-trip** -- exported field-name inventory matches the `packet.patient.*` scheme (no mangled/auto names); a sample read via PdfPig returns the expected name->value pairs.
5. **Body diagram** -- the Present-Complaints (page 7) body image accepts a freehand ink annotation in Acrobat.
6. **Regression** -- every previously-approved page still renders and behaves correctly in the assembled document.

## Decisions (resolved 2026-06-03)

1. **Approach:** WeasyPrint `--pdf-forms` + pikepdf, reusing the Doctor packet's shared toolchain. Confirmed best on the merits (only $0 + automatable + version-controlled + form-preserving HTML path) and gives cross-packet consistency.
2. **Pre-fill scope:** pre-fill ONLY the 19 tokenized spots; untokenized gov-form/intake blanks (pages 11-15) stay blank + editable, matching the original exactly.
3. **Body diagram (page 7) -- deep research 2026-06-04, RESOLVED to "faithful freehand canvas":** the front/back body outlines (user-supplied `Anterior_Body.png` / `Posterior_Body.png`) are reproduced exactly as a clean print-and-draw canvas, also freehand-inkable on screen (Acrobat/Edge ink annotations). The page's discrete data IS structured (0-10 scale = circle-the-choice; Q1/Q4/Q5/Q6 "Where?" = text fields; Q2/Q3 Worse/Same/less + Q6 Yes/No = checkboxes; signature/date = text). Body MARKS stay freehand (captured via the uploaded scan now; not structured DB data). Rationale (3-track research): (a) PDF has no region-constrained drawing field and ink read-back is raw coords + cross-viewer fragile; (b) region-hotspots are the only PDF-native structured option but deviate from the paper "draw symbols" form and are cramped on the ~172x257px image; (c) DECISIVE -- the repo workflow is email -> patient prints + hand-fills + uploads scan (no in-app PDF viewer/drawing exists; ADR-010). Structured body-map capture, if later wanted, is best done as a web canvas in the portal (per CHOIR/Navigate-Pain literature), flagged as a future enhancement -- not forced into the PDF now.
4. **Build location / branch:** build + fully verify in untracked `.tmp-packet-inspect/patient-template/`; create the feature branch + place final artifacts only after the entire template works.
5. **Standing directive:** reproduce the original faithfully (EN + ES verbatim, tokens exact); never assume or guess -- when anything is unclear, stop and ask.
6. **Out of scope (later phases):** token pre-fill wiring, round-trip ingestion (PdfPig/.NET), DocMDP certification, and switching the app from DOCX to the new template. Carried-forward hard requirements: mark every token spot precisely + emit a token inventory; treat `packet.patient.*` field names as a frozen contract.

## Re-baseline decisions (2026-06-03, after the template edit)

7. **Reorder + removals:** cover letter moved to page 1; Epworth Sleepiness Scale, QME Form 122 Declaration, and Service List REMOVED. 15 content pages remain. Token set: 19 distinct.
8. **Trailing empty page:** the source renders a 16th EMPTY page (a Word formatting artifact the user cannot delete without breaking layout). It is intentionally NOT reproduced in the HTML -- the template ends at page 15. (User-directed.)
9. **AMA footers (RESOLVED):** normalize to a consistent "Page 1 of 3", "Page 2 of 3", "Page 3 of 3" in the template (a deliberate, agreed deviation from the source's inconsistent "1 of 4 / 2 of 4 / 3 of 3"). Applied when the AMA pages (T9-T11) are built.
10. **Orphaned ADL title (RESOLVED):** use clean page breaks -- the cover letter is a self-contained page 1, and the "ACTIVITIES OF DAILY LIVING FORM" title stays with its table on page 2 (do NOT reproduce the LibreOffice orphan artifact).
11. **ADL selection control (RESOLVED, user feedback):** the 4 difficulty columns are wide, so the ADL uses a **tick-one radio group per row** (not the highlight): each row is ONE single-select field `packet.patient.adl.<section>.<activity>` whose value is the chosen column (nodiff|somediff|muchdiff|unable). Empty cells until ticked, centered checkmark when chosen, mutually exclusive, deselectable. Mechanism: `cc:<field>#<value>` anchors grouped by `harvest_circle_fields` into a radio field with a checkmark appearance (`/CCMK`). The translucent-highlight mechanism (`/CCHL`) remains available for other circle-the-choice spots.
