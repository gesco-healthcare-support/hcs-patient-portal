---
feature: doctor-packet-html-template
date: 2026-06-02
status: draft
base-branch: main
related-issues: []
---

## Goal

Recreate the Doctor packet as a faithful, fillable PDF **template**, built page-by-page from a single HTML/CSS generator rendered by WeasyPrint (`--pdf-forms`) and finalized by a pikepdf post-process, with manual accuracy + regression verification after each page.

## Context

- The Doctor packet (`DoctorPacket.docx`, 42 tables / 2,748 cells / ~830 fillable spots) is the densest of the three packets. It must become a fillable PDF where pre-filled `##Group.Field##` tokens are locked and the blank fields are editable.
- Approach was chosen after a full evaluation (WeasyPrint vs. Acrobat vs. commercial engines) and proven on a POC: **page 1 (Physical Examination)** is complete and verified; **page 2 (Spinal Examination)** is a working first cut.
- The POC accumulated scratch files (`page1.html`, `page1_body.html`, `doctor.html`, …) in `.tmp-packet-inspect/spike/`. **A core requirement of this plan is a clean file structure with no proliferation of per-page HTML files.**
- Pre-fill (token substitution from `PacketTokenContext`) and round-trip data ingestion are later phases; this plan builds the template and designs field names so those phases drop in cleanly.
- This work does NOT touch the live DOCX → Gotenberg packet pipeline; it produces new template artifacts in parallel.

## Approach

**Pipeline (proven):** `build_doctor.py` (generator) → `doctor.html` → `weasyprint --pdf-forms` → `doctor.pdf` → `post_process.py` (pikepdf) → final fillable PDF.
- Renderer: WeasyPrint (BSD, $0) in a Docker sidecar (`Dockerfile.weasyprint`) with free metric-compatible fonts (Carlito ≡ Calibri, Liberation ≡ Arial/Times).
- Finalize: pikepdf post-process — radio "circle-the-choice" ring appearances, `NoToggleToOff` cleared (deselectable), checkbox marks.
- Rejected alternatives (with reasons): **Acrobat-authored PDFs** (manual placement + hand-naming of ~830 fields, binary/non-diffable, re-work on every change); **commercial engines** PDFreactor/Prince/iText/IronPDF (cost / AGPL); **raw Chromium/Gotenberg** (flattens forms). WeasyPrint is $0, automatable, version-controlled, and validated on the densest page.

**File organization (the anti-confusion rule):**
- **One generator = source of truth.** `build_doctor.py` contains **one function per page**; there are **no per-page `.html` files**. Page 1 gets folded INTO the generator (eliminating `page1.html` / `page1_body.html`).
- **One output HTML** (`doctor.html`) — a build artifact, regenerated, never hand-edited.
- **Shared tooling, reused across all three packets:** `post_process.py`, `Dockerfile.weasyprint`, `render.sh` (one command: generate → render → finalize), `check_fields.py` (inspector).
- Build in a **clean, untracked working directory** first — recommend `.tmp-packet-inspect/doctor-template/` — holding `build_doctor.py`, `doctor.html`, `doctor.pdf`, and a `shared/` for the reused tooling. The whole packet is built + verified here **before** any branch/commit; the feature branch is created and final artifacts are placed in the repo only after full verification (per decision 1).
- `.tmp-packet-inspect/spike/` is retired once page 1 is migrated; throwaway artifacts deleted.

**Fidelity rule (decision 2):** reproduce the original's structure and markers **exactly** — no shortcuts, no assumptions, no guessing. When a marker's meaning, options, or layout is unclear, **pause and ask** rather than approximate.

**Field-control conventions (clinically sensible):**
- **Text field:** measurements / numeric / free notes (ROM R-L, vitals, strength, inspection notes, write-in lines).
- **Checkbox (multi-select):** independent flags and findings that can co-occur — J-Tech, palpation `T/H/S`, observation/imaging/case-type boxes.
- **Circle-the-choice radio (single-select):** the ordinal "circle one" markers — orthopedic `+/−`, reflex `0–5`, sensation `↑/N/↓`, vascular `S/W/A`, neuro `+/−`. Options stay printed; pikepdf draws a ring around the selected one; deselectable. (Replaces the page-2 dropdowns.)
- **Static text:** labels, headers, normal `N` values, AMA refs, and resolved `##tokens##` (pre-filled → locked).

**Round-trip field naming:** hierarchical, stable, ASCII — `packet.doctor.<page>.<section>.<field>[.<index>][.<r|l>]` (e.g. `packet.doctor.spinal.cervical.rom.cervical_flexion.r`). Set as the HTML control `name`; WeasyPrint preserves it verbatim as the AcroForm field name.

**Token pre-fill marking (decision 3 — must not be lost):** every pre-fill spot keeps its **exact** `##Group.Field##` text wrapped in a distinct `<span class="tok">`, never paraphrased or dropped. The generator also emits a **token inventory** (`doctor-tokens.txt`: token → page/section) so the later pre-fill phase maps every token precisely.

**Page sequence** (logical pages; exact count confirmed in T1 — original renders to ~10 PDF pages):
1. Physical Examination — prototyped/verified
2. Spinal Examination — first cut (needs circle-the-choice + palpation-row fidelity fixes)
3. Upper Extremities (Shoulder/Elbow/Wrist/Fingers ROM-Strength-Orthopedic-Palpation, Inspection, Vascular Pulse)
4. Lower Extremities (Hip/Knee/Ankle, Inspection, Vascular Pulse)
5. Fee Ticket (CPT/RVS billing grid)
6. ORDERS (case-type + imaging checkboxes, Body Part / Comment / Claim Status write-ins)
7. DWC Physician's Return-to-Work & Voucher Report (restriction matrix + employee/claims/employer + signature)
8. Remaining static/instruction page(s) — confirm in T1

## Tasks

- **T1: Confirm page inventory + consolidate the toolchain.**
  - approach: code
  - files-touched: [.tmp-packet-inspect/doctor-template/build_doctor.py, .tmp-packet-inspect/doctor-template/shared/{post_process.py, Dockerfile.weasyprint, render.sh, check_fields.py}]
  - acceptance: render the original `DoctorPacket.pdf` and list every page + its sections; clean working dir created; page 1 folded into `build_doctor.py` (no `page1*.html`); `render.sh` produces `doctor.pdf` (page 1) identical to the verified POC; stray spike files removed.

- **T2: Page 1 — Physical Examination (finalize in generator).**
  - approach: code
  - files-touched: [build_doctor.py, doctor.html]
  - acceptance: page 1 renders identically to the verified POC; 97 fields intact; you re-confirm in Acrobat.

- **T3: Page 2 — Spinal Examination (finish).**
  - approach: code
  - files-touched: [build_doctor.py, shared/post_process.py]
  - acceptance: dropdowns converted to circle-the-choice (ring, deselectable, options visible); Costosternal/Sacroiliac/Sciatic rendered as a **separate `T` for Right and Left**, and Ribcage as **`T (P L A)` per side** — exact, no simplification; fits one page; you verify layout + every control type in Acrobat.

- **T4: Page 3 — Upper Extremities.** approach: code · files: [build_doctor.py] · acceptance: matches original; controls behave; one page; prior pages still correct; you verify.
- **T5: Page 4 — Lower Extremities.** approach: code · files: [build_doctor.py] · acceptance: as T4.
- **T6: Page 5 — Fee Ticket.** approach: code · files: [build_doctor.py] · acceptance: billing grid faithful; fields fill cells; one page; you verify.
- **T7: Page 6 — ORDERS.** approach: code · files: [build_doctor.py] · acceptance: checkboxes + write-ins faithful; you verify.
- **T8: Page 7+ — DWC Return-to-Work & Voucher Report (+ any static page).** approach: code · files: [build_doctor.py] · acceptance: restriction matrix + header + signature faithful; gov-form layout matches; you verify.
- **T9: Whole-document finalize.** approach: code · files: [build_doctor.py, doctor.html, doctor.pdf, doctor-tokens.txt] · acceptance: full multi-page `doctor.pdf` renders; **field-name inventory + token inventory** exported; you do a final end-to-end pass; then (per decision 1) we create the feature branch + place final artifacts in the repo.

**Per-page build/verify loop (applies to T3–T8):**
1. Study — render the original page + dump its tables (`pkspike table`) to map exact structure/fields.
2. Build — add/extend the page function in `build_doctor.py` per the conventions above.
3. Generate+render+finalize — `render.sh` → `doctor.pdf` (all pages so far).
4. Self-check — field count, no overflow (each logical page = one PDF page), names follow scheme, quick visual diff vs original.
5. Hand off — you open `doctor.pdf` in Acrobat/Edge: verify the NEW page (layout vs original + click every control type) AND regression-check all PRIOR pages.
6. Fix per feedback; re-render; re-verify.
7. Sign-off → next page.

## Risk / Rollback

- **Blast radius:** isolated to the new template artifacts + working directory. The live DOCX → Gotenberg packet pipeline is untouched, so nothing in production breaks during the build.
- **Per-page gate** prevents compounding errors: a page isn't accepted until you verify it AND all prior pages.
- **Rollback:** delete the working directory / discard the branch; the existing DOCX pipeline remains the source of truth until a separate, later integration phase explicitly switches the Doctor kind to the HTML template.
- **Known engine risks + mitigations:** WeasyPrint sizes form controls oddly in `px` → size in `pt`; weak flexbox → CSS tables; dense grids (Fee Ticket) → generator loops + `table-layout:fixed`; the DWC government form is the highest-fidelity-risk page → verify carefully (T8).

## Verification

After all pages: open the full `doctor.pdf` in **Adobe Acrobat and Microsoft Edge** and confirm, page by page against the original `DoctorPacket.pdf`:
1. **Fidelity** — layout, labels, normal values, AMA refs, section structure match (deviations limited to the flagged ones).
2. **Field behavior** — text fields fill their cells; checkboxes mark and print clearly; circle-the-choice radios circle the selected option, allow only one, and can be deselected; `##tokens##` render as locked static text.
3. **No overflow** — each logical page is exactly one PDF page.
4. **Round-trip** — exported field-name inventory matches the `packet.doctor.*` scheme (no mangled/auto names).
5. **Regression** — every previously-approved page still renders and behaves correctly in the assembled document.

## Decisions (resolved 2026-06-02)

1. **RESOLVED — Location/branch:** build + fully verify the whole packet in a clean **untracked working directory** first (recommend `.tmp-packet-inspect/doctor-template/`); create the feature branch and place final artifacts in the repo only **after** the entire template works.
2. **RESOLVED — Reproduce exactly, no shortcuts:** Costosternal / Sacroiliac / Sciatic = a separate `T` for Right and Left; Ribcage = `T (P L A)` per side. **Standing directive for the whole build: reproduce the original faithfully; never assume or guess — when anything is unclear, stop and ask.**
3. **RESOLVED — Out of scope (later phases):** token pre-fill wiring, round-trip ingestion, switching the app from DOCX to HTML. **Carried forward as a hard requirement:** mark every token pre-fill spot precisely + emit a token inventory (see Approach).
4. **RESOLVED — Stray yellow highlights vs. tokens:** the leftover Word highlights (pg 2 EAMS/WCAB, pg 5 "Yuri Falkinstein M.D.", pg 7 Claim-Examiner + ZipCode) are cosmetic and are **dropped** (no yellow background). This is independent of the `##Group.Field##` tokens underneath them: tokens inject data from booked appointments and MUST be replicated **exactly** -- a highlighted token loses only its background, never its `##...##` text.
5. **RESOLVED — DWC logo (pg 7):** reproduce the original DWC seal image (extract from the DOCX; user will extract manually if extraction is difficult).
6. **RESOLVED — Page 1 control size:** checkbox/radio reduced from 10pt to 9pt (max 1pt reduction); no other page-1 changes.
7. **RESOLVED — Circle-the-choice style + mechanism (page 2):** markers use a **colored highlight** (default soft yellow translucent ellipse), not a ring. Standard radios were ruled out (too bulky for the dense cells -- reflex/sensation wrap, T/H/S overflow). WeasyPrint's own form radios/checkboxes were ruled out too: they bake a default control box into the page content (CSS can't remove it) and WeasyPrint radio grouping is broken (Kozea/WeasyPrint #1831/#1918). **Chosen mechanism (link-harvest):** each option glyph is authored as a plain `<a href="cc:<fieldname>">` anchor (no form input -> no baked box); `post_process.harvest_circle_fields` reads each anchor's `cc:` link rectangle from the rendered PDF and creates an AcroForm checkbox widget there with a blank-off / highlight-on appearance; `NeedAppearances=False` so viewers honor it. **Trade-off:** single-choice markers (+/-, reflex, sensation, vascular) are independent checkboxes -- "circle one by convention", not hard-enforced -- matching the hand-circled paper form. Text fields and the J-Tech checkboxes remain native WeasyPrint `--pdf-forms` widgets.
