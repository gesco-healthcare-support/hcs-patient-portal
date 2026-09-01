"""Patient packet fillable-PDF template generator -- single source of truth.

One function per logical page; assembled into ONE patient.html (no per-page HTML
files). Rendered by WeasyPrint --pdf-forms, finalized by shared/post_process.py.

Conventions:
- Tokens kept EXACT in <span class="tok">##Group.Field##</span> (never paraphrased).
  .tok carries NO visual change -- the template must look exactly like the original;
  it is only a marker the later pre-fill phase locates and substitutes.
- Field names: packet.patient.<page>.<section>.<field>[.<index>]  (round-trip contract).
- Controls: text inputs (blanks/notes), checkboxes (multi-select), circle-the-choice
  (single-select ordinal markers, authored as <a href="cc:NAME"> anchors that
  shared/post_process.py harvests into highlight widgets), static text + tokens.

Non-ASCII glyphs in the ORIGINAL document (e.g. the en-dash U+2013 in "Example -")
are emitted via \\u escapes so this source stays ASCII while the output matches exactly.

Pages are added one at a time (one per gate); each is a function appended to PAGES.
"""

import base64
import mimetypes
import os
import re

EN_DASH = "\u2013"


def _inline_images(html):
    """Embed local <img src="..."> files as base64 data URIs so the generated HTML is
    self-contained -- it is POSTed to the renderer service, which has no access to the
    image files. Remote/data URIs and missing files are left untouched."""
    base = os.path.dirname(os.path.abspath(__file__))
    def repl(m):
        src = m.group(1)
        if src.startswith(("data:", "http:", "https:")):
            return m.group(0)
        path = os.path.join(base, src)
        if not os.path.isfile(path):
            return m.group(0)
        mime = mimetypes.guess_type(path)[0] or "image/png"
        with open(path, "rb") as fh:
            b64 = base64.b64encode(fh.read()).decode("ascii")
        return f'src="data:{mime};base64,{b64}"'
    return re.sub(r'src="([^"]+)"', repl, html)


def _tok(name):
    """Wrap a ##Group.Field## token verbatim as a tracked, visually-neutral marker."""
    return f'<span class="tok">{name}</span>'

# ----------------------------------------------------------------------------- CSS
CSS = r"""
  /* free metric-compatible fonts: Carlito=Calibri, Liberation Sans/Serif=Arial/Times */
  @page { size: Letter; margin: 0.5in 0.5in 0.4in 0.5in; }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body { font-family: "Carlito","Calibri","Liberation Sans","Arial",sans-serif;
         font-size: 10pt; color: #000; line-height: 1.15; }

  /* each logical page -> its own PDF page */
  .page { break-after: page; position: relative; }
  .page:last-child { break-after: auto; }

  .tok { }   /* pre-fill substitution point: NO visual change (exact-fidelity marker) */

  /* text fields: borderless, fill the cell / sit on an underline rule */
  input[type="text"] { width: 100%; border: none; outline: none; background: transparent;
        font: inherit; padding: 0 2px; margin: 0; }
  .underline { border-bottom: 1px solid #000; }
  textarea { width: 100%; border: none; outline: none; background: transparent;
        font: inherit; resize: none; }

  input[type="checkbox"] { width: 10pt; height: 10pt; box-sizing: content-box;
        margin: 0 4px 0 0; vertical-align: middle; }

  /* circle-the-choice marker: a plain anchor glyph (NO form input, so WeasyPrint
     bakes no control box). post_process harvests each anchor's 'cc:' link rectangle
     into a highlight checkbox widget (blank off / translucent highlight on). */
  a.cc-opt { display: inline-block; text-align: center; text-decoration: none; color: #000; }
  a.cc-cell { display: block; width: 100%; height: 17px; text-decoration: none; }

  /* ---------- Activities of Daily Living (clean page 2) ---------- */
  @page adl { size: Letter; margin: 0.36in 0.51in 0.19in 0.44in; }
  #p-adl { page: adl; font-family: "Liberation Sans","Arial",sans-serif; }
  .adl-title { text-align: center; font-weight: bold; text-decoration: underline;
               font-size: 14pt; margin: 0 0 1px; }
  .adl-sub   { text-align: center; font-size: 9pt; margin: 0 0 4px; }
  .adl-id    { text-align: left; font-size: 11pt; margin: 0 0 2px; line-height: 1.2; }
  .adl-id .lab { font-weight: bold; }
  .adl-id .dategap { margin-left: 1.9in; }

  table.adl { border-collapse: collapse; width: 100%; table-layout: fixed; font-size: 11pt; }
  table.adl th, table.adl td { border: 1px solid #000; padding: 0 3px; height: 17px;
        vertical-align: middle; line-height: 1.0; overflow: hidden; }
  table.adl th { font-weight: bold; text-align: left; }
  table.adl th.h-right { text-align: right; }
  table.adl .gray { background: #F1F1F1; }
  table.adl td.lbl { text-align: left; font-weight: normal; white-space: nowrap; }
  table.adl td.ans, table.adl td.sliver { padding: 0; }
  table.adl tr.sect td { text-align: left; font-weight: normal; background: #F1F1F1; }
  table.adl tr.spacer td { border-left: none; border-right: none; height: 6px; }

  /* ---------- Release of Medical Records (page 3). Fonts mirror the render:
     Tahoma -> Noto Sans (title/body/labels), Arial (Date/To), Times New Roman
     (address block + token values). ---------- */
  @page release { size: Letter; margin: 0.45in 0.61in 0.2in 0.56in; }
  #p-release { page: release; font-family: "Noto Sans","Liberation Sans",sans-serif; }
  #p-release .rel-title { text-align: center; font-weight: bold; text-decoration: underline;
        font-size: 19.5pt; margin: 0 0 30pt; }
  #p-release .r-line { font-family: "Arial","Liberation Sans",sans-serif; font-size: 16pt;
        line-height: 1.5; white-space: nowrap; }
  #p-release .r-key { display: inline-block; width: 0.6in; }   /* Date/To label column -> underlines align */
  #p-release input.fld { display: inline-block; vertical-align: baseline; border: none;
        border-bottom: 1px solid #000; background: transparent; font: inherit; outline: none;
        height: 1.05em; padding: 0; }
  #p-release input.f-line { width: 3.85in; }     /* Date/To/recipient underlines (aligned start+width) */
  #p-release .r-sign { font-size: 12pt; margin: 26pt 0 0; white-space: nowrap; }
  #p-release .r-sign .lab { font-weight: bold; }
  #p-release input.f-sign { width: 3.85in; }
  #p-release .r-please { font-size: 18pt; line-height: 1.25; margin: 72pt 0 0; }
  /* West Coast + FAX sit in a narrow indented band (docx ind left=3474 right=3015
     twips ~= 2.83in) which wraps "West Coast Spine Institute" onto two lines. */
  #p-release .r-inst { text-align: center; font-weight: bold; font-size: 18pt; line-height: 1.12;
        width: 2.83in; margin: 28pt 0 0 2.41in; }
  #p-release .r-addr { font-family: "Times New Roman","Liberation Serif",serif; text-align: center;
        font-size: 16pt; line-height: 1.2; margin: 0; }
  #p-release .r-phone { font-family: "Times New Roman","Liberation Serif",serif; text-align: center;
        font-size: 16pt; line-height: 1.2; margin: 24pt 0 0; }
  #p-release .r-fax { text-align: center; font-size: 16pt; line-height: 1.2;
        width: 2.85in; margin: 0 0 0 2.41in; }
  #p-release .r-field { font-size: 12pt; margin: 0 0 17pt; }
  #p-release .r-field.r-gaptop { margin-top: 30pt; }   /* gap above the Name/DOB/SSN block */
  #p-release .r-field .lab { font-weight: bold; display: inline-block; min-width: 1.95in; }
  #p-release .tok-ul { font-family: "Times New Roman","Liberation Serif",serif;
        text-decoration: underline; }

  /* ---------- Privacy Policy Statement (page 4; section margins top=800 left=1040
     right=1320 bottom=280 twips). All Tahoma->Noto Sans 10.5pt, justified. ---------- */
  @page privacy { size: Letter; margin: 0.56in 0.92in 0.19in 0.72in; }
  #p-privacy { page: privacy; font-family: "Noto Sans","Liberation Sans",sans-serif;
               font-size: 10.5pt; }
  #p-privacy .pp-h { font-weight: bold; text-align: left; margin: 0 0 12pt; }
  #p-privacy .pp-p { text-align: justify; line-height: 1.32; margin: 0 0 12pt; }

  /* ---------- Pregnancy/X-ray Acknowledgement + Emergency Contact (page 5; bilingual;
     section margins top=1300 left=1320 right=1320 bottom=280 twips). Letterhead
     Footlight MT Light -> image; NAME/ACCT/DATE Arial 14pt; body Tahoma->Noto Sans 12pt;
     footer Arial 9pt anchored at page bottom. ---------- */
  @page pregnancy { size: Letter; margin: 0.9in 0.92in 0.19in 0.92in; }
  #p-pregnancy { page: pregnancy;
                 font-family: "Noto Sans","Liberation Sans",sans-serif; font-size: 12pt; }
  #p-pregnancy .pg-head { display: block; width: 4.44in; height: auto; margin: 0; }
  #p-pregnancy .pg-idblock { margin: 14pt 0 0; }
  #p-pregnancy .pg-id { font-family: "Arial","Liberation Sans",sans-serif; font-weight: bold;
        font-size: 14pt; line-height: 1.32; margin: 0; white-space: nowrap; }
  #p-pregnancy .pg-id .vr { font-weight: normal; }
  #p-pregnancy .pg-ack { line-height: 1.3; margin: 22pt 0 0; }
  #p-pregnancy .pg-line { white-space: nowrap; margin: 18pt 0 0; }
  #p-pregnancy input.fld { display: inline-block; vertical-align: baseline; border: none;
        border-bottom: 1px solid #000; background: transparent; font: inherit; outline: none;
        height: 1.05em; padding: 0; }
  #p-pregnancy input.f-lmp { width: 3.5in; }
  #p-pregnancy input.f-sig { width: 2.9in; }
  #p-pregnancy input.f-date { width: 1.9in; }
  #p-pregnancy input.f-nm { width: 2.4in; }
  #p-pregnancy input.f-rel { width: 2.2in; }
  #p-pregnancy input.f-ph { width: 1.7in; }
  #p-pregnancy .pg-rule { border: none; border-top: 1px solid #8a8a8a; margin: 28pt 0 0; }
  #p-pregnancy .pg-emh { margin: 20pt 0 0; }
  #p-pregnancy .pg-foot-wrap { margin: 40pt 0 0;
        font-family: "Arial","Liberation Sans",sans-serif; text-align: center; }
  #p-pregnancy .pg-foot-wrap .b { font-weight: bold; font-size: 9pt; margin: 0; }
  #p-pregnancy .pg-foot-wrap .r { font-size: 9pt; margin: 4pt 0 0; }

  /* ---------- Acknowledgement of Receipt of Notice of Privacy Practices (page 6;
     all Arial; section margins top=360 left=440 right=700 bottom=280 twips). The
     original leaves the lower ~27% blank -- match that, do not fill. ---------- */
  @page privacyack { size: Letter; margin: 0.25in 0.49in 0.19in 0.31in; }
  #p-privacyack { page: privacyack; font-family: "Arial","Liberation Sans",sans-serif; font-size: 14pt; }
  #p-privacyack .pa-title { text-align: center; font-weight: bold; font-size: 16pt;
        line-height: 1.25; margin: 0 0 20pt; }
  #p-privacyack .pa-p { margin: 0 0 14pt; line-height: 1.25; }
  #p-privacyack .pa-wcs { font-weight: bold; margin: 0; }
  #p-privacyack .pa-namerule { display: inline-block; white-space: nowrap; min-width: 3.4in;
        border-bottom: 1px solid #000; margin: 18pt 0 0; padding-right: 6pt; }
  #p-privacyack .pa-name { font-weight: bold; font-size: 11pt; }
  #p-privacyack .pa-lbl { line-height: 1.2; margin: 2pt 0 0; }
  #p-privacyack .pa-sigblock { margin: 42pt 0 0; }
  #p-privacyack input.pa-sig { display: block; width: 3.0in; border: none;
        border-bottom: 1px solid #000; background: transparent; font: inherit; outline: none;
        height: 1.2em; padding: 0; }
  #p-privacyack .pa-date { margin: 42pt 0 0; }
  #p-privacyack .pa-datetok { text-decoration: underline; }

  /* ---------- Present Complaints (page 7; dense; Arial; section margins
     top=270 left=340 right=580 bottom=0 twips). Body images = freehand draw
     canvas; 0-10 scale = circle-the-choice (highlight); Worse/Same/less + No/Yes
     = tick-one radios over the box glyph; write-ins = text fields. ---------- */
  @page complaints { size: Letter; margin: 0.19in 0.4in 0.1in 0.24in; }
  #p-complaints { page: complaints; font-family: "Arial","Liberation Sans",sans-serif;
                  font-size: 9pt; line-height: 1.12; }
  #p-complaints .pc-title { text-align: center; font-weight: bold; font-variant: small-caps;
        text-decoration: underline; font-size: 14pt; margin: 0; }
  #p-complaints .pc-id { font-weight: bold; font-size: 11pt; margin: 3pt 0 0; }
  #p-complaints .pc-id .vr { font-weight: normal; }
  #p-complaints .pc-instr { font-size: 8pt; line-height: 1.08; margin: 3pt 0 0; }
  /* pain legend (6 columns) */
  #p-complaints table.pc-legend { width: 100%; border-collapse: collapse; table-layout: fixed;
        text-align: center; margin: 5pt 0 0; }
  #p-complaints table.pc-legend td { font-weight: bold; font-size: 10pt; padding: 0; line-height: 1.1; }
  #p-complaints table.pc-legend .sym { font-weight: normal; font-size: 11pt; }
  /* bodies row */
  /* bodies row as a 6-cell table [Left | BACK | Right | Right | FRONT | Left] so
     the images cannot overlap the side labels; boxes vertically centred on the body. */
  #p-complaints table.pc-bodies { width: 100%; table-layout: auto; border-collapse: collapse;
        margin: 8pt 0 0; }
  #p-complaints table.pc-bodies td { padding: 0; text-align: center; vertical-align: middle; }
  #p-complaints table.pc-bodies td.bodycell { vertical-align: top; }
  #p-complaints table.pc-bodies .pc-bf-lbl { font-weight: bold; font-size: 10pt; line-height: 1.1; }
  #p-complaints table.pc-bodies img { height: 3.15in; margin-top: 2pt; }
  #p-complaints .pc-lrbox { display: inline-block; border: 1px solid #000; padding: 2pt 5pt;
        font-family: "Caladea","Cambria",serif; font-size: 12pt; text-align: center;
        line-height: 1.05; white-space: nowrap; margin-top: 0.7in; }
  /* 0-10 intensity scale */
  #p-complaints .pc-scale { margin: 9pt 0 0; }
  #p-complaints .pc-scale .row { display: flex; }
  #p-complaints .pc-scale .row > div { flex: 1; text-align: left; font-size: 11pt; }
  #p-complaints .pc-scale .nums a { font-size: 11pt; }
  #p-complaints .pc-scale .labels { display: flex; font-size: 10pt; }
  #p-complaints .pc-scale .labels .l0 { flex: 0 0 0; }
  #p-complaints .pc-scale .labels .lmid { flex: 1; text-align: center; }
  #p-complaints .pc-scale .labels .lend { flex: 1; text-align: right; }
  #p-complaints .pc-scale .lsub { font-size: 9pt; }
  /* questions */
  #p-complaints .pc-q { font-size: 9pt; line-height: 1.15; margin: 6pt 0 0; }
  #p-complaints input.pc-fld { display: inline-block; vertical-align: baseline; border: none;
        border-bottom: 1px solid #000; background: transparent; font: inherit; outline: none;
        height: 1.0em; padding: 0; width: 2.5in; }
  #p-complaints input.w-full { display: block; width: 100%; }   /* own-line full-width underline */
  #p-complaints input.w-q1 { width: 2.6in; }
  #p-complaints input.w-where { width: 2.7in; }
  #p-complaints input.w-details { width: 5.7in; }
  #p-complaints input.w-sig { width: 2.6in; }
  #p-complaints input.w-date { width: 1.5in; }
  #p-complaints .pc-q6 { font-size: 9pt; line-height: 1.2; margin: 3pt 0 0; white-space: nowrap; }
  #p-complaints .pc-q6 .sx { display: inline-block; min-width: 1.6in; }
  #p-complaints a.cc-box { text-decoration: none; color: #000; font-size: 11pt; }

  /* ---------- AMA Guidelines Pain Questionnaire p.1 (page 8; Arial; section margins
     top=180 left=260 right=400 bottom=280 twips). 0-10 scales = circle-the-choice
     radio (highlight). Footer normalized to "Page 1 of 3" (decision 9). ---------- */
  @page ama { size: Letter; margin: 0.13in 0.28in 0.19in 0.18in; }
  .amapg { page: ama; font-family: "Arial","Liberation Sans",sans-serif; font-size: 11pt; }
  .amapg .ama-title { text-align: center; font-weight: bold; font-size: 12pt; line-height: 1.2; margin: 0; }
  .amapg .ama-sub { font-size: 10pt; margin: 8pt 0 0; }
  .amapg .ama-sec { font-weight: bold; margin: 14pt 0 5pt; padding-left: 0.35in; }
  .amapg table.ama { width: 100%; border-collapse: collapse; table-layout: fixed; }
  .amapg table.ama td { border: 1px solid #000; padding: 7pt 6pt; vertical-align: top; }
  .amapg .amaqt { font-size: 11pt; line-height: 1.15; }
  /* NOT flex items: WeasyPrint emits TWO link annotations per flex-item <a>, which
     doubled every 0-10 option into 2 stacked widgets. inline-block 1/11 spacing keeps
     the even layout with one widget per option. */
  .amapg .amascale { margin-top: 6pt; font-size: 0; }
  .amapg .amascale a { display: inline-block; width: 9.0909%; text-align: center;
        font-weight: bold; font-size: 12pt; text-decoration: none; color: #000;
        box-sizing: border-box; }
  .amapg .amaanchor { display: flex; justify-content: space-between; font-size: 9pt;
        margin-top: 1pt; line-height: 1.05; }
  .amapg .amaanchor .al { text-align: left; max-width: 47%; }
  .amapg .amaanchor .ar { text-align: right; max-width: 47%; }
  .amapg .ama-sum { font-weight: bold; margin: 8pt 0 0; line-height: 1.5; padding-left: 0.35in; }
  .amapg input.ama-fld { display: inline-block; border: none; border-bottom: 1px solid #000;
        background: transparent; font: inherit; outline: none; height: 1em; width: 2.2in; }
  .amapg .ama-foot { font-weight: bold; margin: 10pt 0 0; }
  .amapg .ama-sign { font-size: 11pt; margin: 8pt 0 0; padding-left: 0.3in; }
  .amapg input.w-amasig { width: 3.2in; }
  .amapg .ama-sign .tok-ul { text-decoration: underline; }
  /* page 10 (only 5 questions) -> taller boxes + more spacing to use the page */
  #p-ama3 table.ama td { padding: 13pt 6pt; }
  #p-ama3 .ama-sum { margin-top: 18pt; }
  #p-ama3 .ama-sign { margin-top: 16pt; }

  /* ---------- CA DWC Employee's Disability Questionnaire (page 11; Arial; section
     margins top=180 left=260 right=400 bottom=280 twips). Barcode = faithful image
     crop (decorative, non-decodable Code-39 art). Identity blanks left editable. ---------- */
  @page deu { size: Letter; margin: 0.13in 0.28in 0.19in 0.18in; }
  #p-deu1 { page: deu; font-family: "Arial","Liberation Sans",sans-serif; font-size: 11pt; }
  #p-deu1 .deu-head { position: relative; text-align: center; min-height: 1.0in; }
  #p-deu1 .deu-bc { position: absolute; top: 2pt; left: 0.08in; width: 1.87in; height: auto; }
  #p-deu1 .deu-h1 { font-weight: bold; font-size: 12pt; margin: 0; }
  #p-deu1 .deu-h2 { font-weight: bold; font-size: 12pt; line-height: 1.18; margin: 5pt 0 0; }
  #p-deu1 .deu-h3 { font-weight: bold; font-size: 12pt; margin: 9pt 0 0; }
  #p-deu1 .deu-box { border: 1px solid #000; padding: 4pt 7pt; text-align: justify;
        line-height: 1.3; margin: 7pt 0 0; }
  #p-deu1 .deu-emp { font-weight: bold; margin: 9pt 0 0; }
  #p-deu1 .deu-fld { margin: 27pt 0 0; }
  #p-deu1 input.deu-line { border: none; border-bottom: 1px solid #000; background: transparent;
        font: inherit; outline: none; height: 1.1em; display: inline-block; vertical-align: bottom; }
  #p-deu1 .deu-lab { font-size: 11pt; margin-top: 1pt; }
  #p-deu1 .deu-row { display: flex; justify-content: space-between; }
  #p-deu1 .deu-row .col-fn { flex: 0 0 5.3in; }
  #p-deu1 .deu-row .col-mi { flex: 0 0 0.8in; }
  #p-deu1 .deu-row .col-city { flex: 0 0 5.0in; }
  #p-deu1 .deu-row .col-state { flex: 0 0 1.1in; }
  #p-deu1 .deu-row .col-zip { flex: 0 0 1.2in; }
  #p-deu1 .deu-row input.deu-line { width: 100%; }
  #p-deu1 .deu-date { margin: 27pt 0 0; }
  #p-deu1 .deu-date input.deu-line { width: 2.0in; }
  #p-deu1 .deu-mmdd { font-size: 9pt; margin: 1pt 0 0 1.25in; }

  /* ---------- CA DWC Disability Questionnaire cont. (page 12; Arial; @page deu).
     Claim lines, 'Check one' square radios, short-answer lines, multi-line answer
     boxes (textarea), date/signature. ---------- */
  #p-deu2 { page: deu; font-family: "Arial","Liberation Sans",sans-serif; font-size: 11pt; }
  #p-deu2 .dq-claim { margin: 9pt 0 0; padding-left: 0.3in; white-space: nowrap; }
  #p-deu2 .dq-claim .lbl { display: inline-block; width: 1.35in; }
  #p-deu2 input.dq-line { border: none; border-bottom: 1px solid #000; background: transparent;
        font: inherit; outline: none; height: 1.1em; display: inline-block; vertical-align: baseline; }
  #p-deu2 input.dq-claimline { width: 4.3in; }
  #p-deu2 .dq-rule { border: none; border-top: 2px solid #000; margin: 26pt 0 0; }
  #p-deu2 .dq-h { font-weight: bold; margin: 12pt 0 0; }
  #p-deu2 .dq-chkrow { margin: 11pt 0 0; padding-left: 0.3in; }
  #p-deu2 a.dq-chk { display: inline-block; width: 16px; height: 16px; border: 1.5px solid #000;
        margin-right: 8px; vertical-align: middle; text-decoration: none; }
  #p-deu2 .dq-q { margin: 10pt 0 0; }
  #p-deu2 textarea.dq-box { border: 1px solid #000; background: transparent; font: inherit;
        width: 100%; display: block; margin: 2pt 0 0; resize: none; box-sizing: border-box; }
  #p-deu2 .dq-sign { margin: 12pt 0 0; }

  /* ---------- CA DWC Description of Employee's Job Duties (page 13; Arial). Centered
     header, bordered instructions box, identity grid (colgroup 49/24/27 -> dividers at
     49% and 73% mirror the original), job-responsibilities textarea, and a 23-row
     activity x 4-frequency matrix. Each activity row = one 4-value checkmark radio
     (packet.patient.jobduties1.<activity>; NEVER|OCCASIONALLY|FREQUENTLY|CONSTANTLY);
     dominant-hand = 2-value circle choice. All identity blanks editable. ---------- */
  @page jobduties { size: Letter; margin: 0.3in 0.34in 0.2in 0.34in; }
  #p-jd1 { page: jobduties; font-family: "Arial","Liberation Sans",sans-serif; font-size: 11pt; }
  #p-jd1 .jd-state { text-align: center; font-weight: bold; font-size: 13pt; margin: 0; }
  #p-jd1 .jd-div { text-align: center; font-weight: bold; font-size: 9pt; margin: 1pt 0 0; }
  #p-jd1 .jd-title { text-align: center; font-weight: bold; font-size: 12pt; margin: 12pt 0 0; }
  #p-jd1 .jd-instr { border: 1px solid #000; padding: 3pt 5pt; font-size: 9pt; line-height: 1.25;
        text-align: justify; margin: 8pt 0 0; }
  #p-jd1 .jd-instr b { font-weight: bold; }
  /* identity grid -- NO flexbox (WeasyPrint mis-places form widgets sized by
     flex-grow; every input carries an explicit width). colgroup 50/25/5.3/19.7
     puts the dividers at 50%, 75%, 80.3% exactly like the docx (table #7). */
  #p-jd1 table.jd-id { width: 100%; border-collapse: collapse; table-layout: fixed; margin: 9pt 0 0; }
  #p-jd1 table.jd-id td { border: 1px solid #000; padding: 2pt 5pt; vertical-align: top;
        font-size: 10pt; line-height: 1.15; overflow: hidden; }
  #p-jd1 .jd-cell-row { height: 0.58in; }
  #p-jd1 .jd-cell-resp { height: 1.25in; }
  #p-jd1 input.jd-fill { display: inline-block; border: none; background: transparent;
        font: inherit; outline: none; height: 1.05em; padding: 0; vertical-align: baseline; }
  #p-jd1 input.jd-blk { display: block; margin-top: 4pt; }
  #p-jd1 textarea.jd-fill { border: none; background: transparent; font: inherit; outline: none;
        width: 100%; resize: none; display: block; margin-top: 2pt; }
  /* big free-text fields are MULTI-LINE so long content (addresses, names, titles)
     WRAPS and stays visible instead of scrolling out of a single-line box. */
  #p-jd1 textarea.jd-mt { display: inline-block; vertical-align: top; border: none;
        background: transparent; font: inherit; outline: none; resize: none;
        overflow: hidden; line-height: 1.2; padding: 0; }
  #p-jd1 .jd-il { display: inline-block; vertical-align: top; white-space: nowrap; }
  #p-jd1 .jd-lb { white-space: nowrap; }
  /* employee-name sub-columns: inline-block (NOT flex) with fixed widths */
  #p-jd1 .jd-nm { white-space: nowrap; }
  #p-jd1 .jd-nm .lab { display: inline-block; width: 1.45in; }
  #p-jd1 .jd-nm .c { display: inline-block; width: 1.5in; text-align: center; }
  #p-jd1 .jd-nm.flds { margin-top: 5pt; }
  /* "1. Check the frequency..." */
  #p-jd1 .jd-check { font-size: 10pt; margin: 11pt 0 5pt; }
  /* activity matrix */
  #p-jd1 table.jd-mx { width: 100%; border-collapse: collapse; table-layout: fixed; font-size: 10pt; }
  #p-jd1 table.jd-mx th, #p-jd1 table.jd-mx td { border: 1px solid #000; padding: 0 5px;
        height: 18.5px; vertical-align: middle; line-height: 1.0; overflow: hidden; }
  #p-jd1 table.jd-mx th { text-align: center; font-weight: bold; }
  #p-jd1 table.jd-mx th .sub { display: block; font-weight: normal; font-size: 9pt; }
  #p-jd1 table.jd-mx td.lbl { text-align: left; white-space: nowrap; }
  #p-jd1 table.jd-mx td.lbl.ind { padding-left: 18px; }
  #p-jd1 table.jd-mx td.ans { padding: 0; }
  #p-jd1 a.jd-mxcell { display: block; width: 100%; height: 18.5px; text-decoration: none; }
  #p-jd1 a.jd-hand { text-decoration: none; color: #000; font-weight: normal; }

  /* ---------- CA DWC Description of Employee's Job Duties cont. (page 14; Arial).
     Lifting/Carrying matrix (11 cols; LIFTING/CARRYING super-headers each span their 4
     freq columns; Height/Distance are text columns); heaviest-item box; job-requires
     a-j Yes/No (2-value square radio) + describe lines; Employee/Employer comment boxes;
     signature block (50/50). Each weight row = two 4-value checkmark radios
     (lift_<w>, carry_<w>). All blanks editable; big free-text fields wrap (multi-line). */
  @page jobduties2 { size: Letter; margin: 0.3in 0.34in 0.2in 0.34in; }
  #p-jd2 { page: jobduties2; font-family: "Arial","Liberation Sans",sans-serif; font-size: 10pt; }
  #p-jd2 .jd2-instr { line-height: 1.2; margin: 0 0 7pt; }
  /* lifting/carrying matrix */
  #p-jd2 table.jd2-mx { width: 100%; border-collapse: collapse; table-layout: fixed; font-size: 8.5pt; }
  #p-jd2 table.jd2-mx th, #p-jd2 table.jd2-mx td { border: 1px solid #000; padding: 0 2px;
        height: 16px; vertical-align: middle; line-height: 1.05; text-align: center; overflow: hidden; }
  #p-jd2 table.jd2-mx th { font-weight: bold; }
  #p-jd2 table.jd2-mx th.grp { font-size: 11pt; }
  #p-jd2 table.jd2-mx th.sub { font-weight: normal; }
  #p-jd2 table.jd2-mx th.sub .h { display: block; }
  #p-jd2 table.jd2-mx td.wt { text-align: left; white-space: nowrap; font-size: 9pt; }
  #p-jd2 table.jd2-mx td.ans { padding: 0; }
  #p-jd2 table.jd2-mx td.hd { padding: 0; }
  #p-jd2 a.jd2-mxcell { display: block; width: 100%; height: 18px; text-decoration: none; }
  #p-jd2 input.jd2-cell { display: block; width: 100%; border: none; background: transparent;
        font: inherit; outline: none; text-align: center; height: 1.2em; padding: 0; }
  /* heaviest-item write area (multi-line, wraps) */
  #p-jd2 .jd2-heavy { margin: 9pt 0 0; }
  #p-jd2 textarea.jd2-mt { display: block; width: 100%; border: none; background: transparent;
        font: inherit; outline: none; resize: none; overflow: hidden; line-height: 1.3; padding: 0; }
  /* job-requires + comments + signatures: ONE outer-bordered table. The a-j rows have
     NO internal grid (just per-row describe underlines, like the original); only the
     comments + signature rows draw dividers. */
  #p-jd2 table.jd2-req { width: 100%; border: 1px solid #000; border-collapse: collapse;
        table-layout: fixed; }
  #p-jd2 table.jd2-req td { padding: 1.5pt 5pt; vertical-align: top; }
  #p-jd2 table.jd2-req td.item { line-height: 1.15; }
  #p-jd2 table.jd2-req td.bx { text-align: center; vertical-align: bottom; padding: 0 0 2pt; }
  #p-jd2 a.jd2-chk { display: inline-block; width: 13px; height: 13px; border: 1.5px solid #000;
        vertical-align: middle; text-decoration: none; }
  #p-jd2 table.jd2-req td.desc { vertical-align: bottom; padding: 0 3pt 1pt; }
  /* describe = multi-line so a long answer wraps + stays visible; bottom underline for the
     fill-in affordance (the original is a single ruled line). */
  #p-jd2 textarea.jd2-desc { display: block; width: 100%; border: none; border-bottom: 1px solid #000;
        background: transparent; font: inherit; outline: none; resize: none; overflow: hidden;
        line-height: 1.3; padding: 0; }
  #p-jd2 .jd2-hq { vertical-align: top; }                 /* "3. Please indicate..." */
  #p-jd2 .jd2-yes { vertical-align: bottom; text-align: center; }
  #p-jd2 .jd2-no  { vertical-align: top; text-align: center; }
  #p-jd2 .jd2-dh  { vertical-align: top; }
  #p-jd2 table.jd2-req td.cmt { border-top: 1px solid #000; height: 0.7in; }
  #p-jd2 table.jd2-req td.sig { border-top: 1px solid #000; height: 0.56in; white-space: nowrap; }
  #p-jd2 table.jd2-req td.sig-r { border-left: 1px solid #000; }
  #p-jd2 .jd2-iflab { font-size: 8pt; }                   /* (IF APPLICABLE) */

  /* ---------- Patient Information/Update (page 15; Calibri -> Carlito). Title 22pt
     bold+underline; top block = bold 11pt labels with DASHED fill lines; Attorney
     (regular 11pt) + Emergency/Interpreter (regular 14pt) blocks with bold 14pt
     underlined headings and SOLID underscore fill lines. All blanks editable, NOT
     pre-filled. Big free-text fields (names, addresses, email) wrap (multi-line). */
  @page patinfo { size: Letter; margin: 0.5in 0.6in 0.2in 0.5in; }
  #p-patinfo { page: patinfo; font-family: "Carlito","Calibri",sans-serif; font-size: 11pt; }
  #p-patinfo .pi-title { text-align: center; font-weight: bold; font-size: 22pt;
        text-decoration: underline; margin: 0 0 24pt; }
  #p-patinfo .pi-body { margin-left: 0.5in; }
  #p-patinfo .pi-row { font-weight: bold; white-space: nowrap; margin: 0 0 18pt; }
  #p-patinfo .pi-row .g { margin-left: 0.28in; }          /* gap before 2nd/3rd field on a line */
  #p-patinfo .pi-h { font-weight: bold; font-size: 14pt; text-decoration: underline;
        margin: 22pt 0 8pt; }
  #p-patinfo .pi-row2 { white-space: nowrap; margin: 0 0 10pt; }
  #p-patinfo .pi-row3 { font-size: 14pt; white-space: nowrap; margin: 0 0 5pt; }
  #p-patinfo input.pi-fld, #p-patinfo textarea.pi-fld { border: none;
        border-bottom: 1px solid #000; background: transparent; font: inherit; outline: none;
        padding: 0; }
  #p-patinfo input.pi-fld { display: inline-block; height: 1.05em; vertical-align: baseline; }
  #p-patinfo textarea.pi-fld { display: inline-block; vertical-align: top; resize: none;
        overflow: hidden; line-height: 1.3; }

  /* ---------- Cover letter (section margins top=520 left=640 right=740
     bottom=280 twips). Letterhead is Footlight MT Light -> image crop. ---------- */
  @page cover { size: Letter; margin: 0.36in 0.51in 0.19in 0.44in; }
  #p-cover { page: cover; font-family: "Carlito","Calibri",sans-serif;
             font-size: 11pt; line-height: 1.2; }
  #p-cover .cl { margin: 0; }
  #p-cover .cl.center { text-align: center; }
  #p-cover .cl-head { display: block; width: 4.41in; height: auto; margin: 0; }
"""


# ----------------------------------------------------------------------------- Activities of Daily Living
# 4-column single-select matrix; one circle-the-choice
# (cc anchor) per answer cell. Grid mirrors the docx exactly: 6 columns
# widths twips [2999,1970,1978,25,1781,1891]; header gridSpans [2,2,1,1]; the
# 25-twip col is a Word sliver artifact (a real but unmarkable gap between
# "With some difficulty" and "With much difficulty").
ADL = [
    ("Self-Care, Personal Hygiene:",
     f"(Example {EN_DASH} Urinating, Defecating, Brushing Teeth, Combing Hair, Bathing, Dressing Oneself, Eating)",
     "selfcare", [
         ("Dress yourself including shoes", "dress_shoes"),
         ("Comb your hair", "comb_hair"),
         ("Wash and dry yourself", "wash_dry"),
         ("Take a bath", "bath"),
         ("Get on and off the toilet", "toilet"),
         ("Brush your teeth", "brush_teeth"),
         ("Cut your food", "cut_food"),
         ("Lift a full cup/glass to your mouth", "lift_cup"),
         ("Open a new milk carton", "milk_carton"),
         ("Make a meal", "make_meal"),
     ]),
    ("Communication:",
     f"(Example {EN_DASH} Writing, Typing, Seeing, Hearing, Speaking)",
     "communication", [
         ("Write a note", "write_note"),
         ("Type a message on a computer", "type_computer"),
         ("See a television screen", "see_tv"),
         ("Use a telephone", "use_phone"),
         ("Speak clearly", "speak_clearly"),
     ]),
    ("Physical Activity:",
     f"(Example {EN_DASH} Standing, Sitting, Reclining, Walking, Climbing Stairs)",
     "physical", [
         ("Work outdoors on flat ground", "work_outdoors"),
         ("Climb up 1 flight of 10 steps", "climb_steps"),
         ("Stand", "stand"),
         ("Sit", "sit"),
         ("Recline", "recline"),
         ("Rise from a chair", "rise_chair"),
         ("Run errands", "run_errands"),
         ("Light housework", "light_housework"),
     ]),
    ("Sensory Function:",
     f"(Example {EN_DASH} Hearing, Seeing, Tactile Feeling, Tasting, Smelling)",
     "sensory", [
         ("Feel what you touch", "feel_touch"),
         ("Smell the food you eat", "smell_food"),
         ("Taste the food you eat", "taste_food"),
     ]),
    ("Nonspecialized Hand Activities: ",
     f"(Example {EN_DASH} Grasping, Lifting, Tactile Discrimination)",
     "hand", [
         ("Open car doors", "car_doors"),
         ("Open previously opened jars", "open_jars"),
         ("Turn faucets on and off", "faucets"),
     ]),
    ("Travel:",
     f"(Example {EN_DASH} Riding, Driving, Flying)",
     "travel", [
         ("Shop", "shop"),
         ("Get in and out of the car", "inout_car"),
         ("Drive a car", "drive_car"),
         ("Take a flight", "take_flight"),
     ]),
    ("Sleep / Sexual Function:",
     f"(Example {EN_DASH} Restful, Nocturnal Sleep Pattern, Orgasm, Ejaculation, Lubrication, Erection)",
     "sleep", [
         ("Sleep", "sleep"),
         ("Engage in sexual activity", "sexual_activity"),
     ]),
]


def _adl_row(section, label, slug):
    """One activity row: label + 4 tick-one answer cells (+ sliver).

    The 4 cells form ONE radio field (packet.patient.adl.<section>.<activity>); the
    chosen column is the field value (nodiff|somediff|muchdiff|unable). Authored as
    'cc:<field>#<value>' anchors that post_process groups into a single-select radio
    with a checkmark appearance (empty until ticked, mutually exclusive, deselectable).
    """
    base = f"packet.patient.adl.{section}.{slug}"

    def ans(level, gray):
        cls = "ans gray" if gray else "ans"
        return f'<td class="{cls}"><a class="cc-cell" href="cc:{base}#{level}"></a></td>'

    return (
        f'<tr><td class="lbl">{label}</td>'
        + ans("nodiff", False)        # col1, under "Without difficulty"
        + ans("somediff", True)       # col2, under "With some difficulty"
        + '<td class="sliver gray"></td>'   # col3, 25-twip sliver
        + ans("muchdiff", False)      # col4, under "With much difficulty"
        + ans("unable", True)         # col5, under "Unable to do"
        + '</tr>'
    )


def page_adl():
    """Activities of Daily Living Form."""
    rows = [
        '<tr>'
        '<th colspan="2" class="h-right">Without difficulty</th>'
        '<th colspan="2" class="gray">With some difficulty</th>'
        '<th>With much difficulty</th>'
        '<th class="gray">Unable to do</th>'
        '</tr>'
    ]
    for idx, (label, example, slug, acts) in enumerate(ADL):
        if idx > 0:
            rows.append('<tr class="spacer"><td colspan="6"></td></tr>')
        rows.append(
            f'<tr class="sect"><td colspan="6"><b>{label}</b> <i>{example}</i></td></tr>'
        )
        for a_label, a_slug in acts:
            rows.append(_adl_row(slug, a_label, a_slug))

    table = (
        '<table class="adl"><colgroup>'
        '<col style="width:28.176%"><col style="width:18.508%">'
        '<col style="width:18.583%"><col style="width:0.235%">'
        '<col style="width:16.732%"><col style="width:17.766%">'
        '</colgroup>'
        + ''.join(rows) +
        '</table>'
    )

    header = (
        '<div class="adl-title">ACTIVITIES OF DAILY LIVING FORM</div>'
        '<div class="adl-sub">Please indicate below any limitations, difficulties or '
        'impairments you have with any of these activities.</div>'
        '<div class="adl-id">'
        '<span class="lab">NAME:</span> '
        '<b><span class="tok">##Patients.FirstName##</span> '
        '<span class="tok">##Patients.LastName##</span></b><br>'
        '<span class="lab">ACCT#:</span> '
        '<span class="tok">##Appointments.RequestConfirmationNumber##</span>'
        '<span class="dategap"><span class="lab">DATE:</span> '
        '<span class="tok">##Appointments.AvailableDate##</span></span>'
        '</div>'
    )
    return f'<div class="page" id="p-adl">{header}{table}</div>'


# ----------------------------------------------------------------------------- Cover letter
def page_cover():
    """Falkinstein appointment cover letter (page 1; token-heavy, pre-filled+locked).

    Letterhead is set in Footlight MT Light (proprietary) -> reproduced as an exact
    image crop from the original render. Every token kept verbatim. Layout mirrors
    the rendered original line-for-line (centred location block + footer).
    """
    blank = '<p class="cl">&nbsp;</p>'
    parts = [
        '<img class="cl-head" src="images/cover_letterhead.png" '
        'alt="Yuri Falkinstein, M.D., FAAOS -- Fellow, American Academy of Orthopaedic Surgeons">',
        blank, blank,
        f'<p class="cl">{_tok("##Others.DateNow##")} </p>',
        blank, blank,
        f'<p class="cl"><b>{_tok("##Patients.FirstName##")}  {_tok("##Patients.LastName##")}</b></p>',
        f'<p class="cl">{_tok("##Patients.Street##")} </p>',
        f'<p class="cl">{_tok("##Patients.City##")},  {_tok("##Patients.State##")} {_tok("##Patients.ZipCode##")}</p>',
        blank,
        f'<p class="cl">Dear : <b>{_tok("##Patients.FirstName##")}  {_tok("##Patients.LastName##")}</b></p>',
        blank,
        '<p class="cl">Please be advised that an appointment has been scheduled for you to '
        f'see Yuri Falkinstein, M.D. on {_tok("##Appointments.AvailableDate##")}  at '
        f'{_tok("##Appointments.AppointmenTime##")}. Your appointment will be held at: </p>',
        blank,
        '<p class="cl center">WEST COAST SPINE INSTITUTE<br>'
        f'{_tok("##Appointments.Location##")}<br>'
        f'{_tok("##Appointments.LocationAddress##")} <br>'
        f'{_tok("##Appointments.LocationCity##")},  {_tok("##Appointments.LocationState##")}<br>'
        f'{_tok("##Appointments.LocationZipCode##")}</p>',
        blank,
        '<p class="cl">The parking fee for this location is parking fee '
        f'{_tok("##Appointments.LocationParkingFee##")}</p>',
        blank,
        '<p class="cl">Please make sure you keep this appointment as it is the most important '
        'medical appointment for your case.  Please allow ample time (minimum 4 hours) to be at '
        'our office as you may require diagnostic testing on the day of your appointment.</p>',
        blank,
        '<p class="cl">Please review and compare this appointment with any other appointment '
        'letter you may have received.  In case of any discrepancies, please contact our office '
        'immediately for clarification.</p>',
        blank,
        '<p class="cl">Kindly note that <u>you must check in at the above address 30 minutes '
        'prior</u> to your scheduled appointment time with proof of identification.</p>',
        blank,
        '<p class="cl"><b>It is necessary that you contact our office at 818-582-2600, 10 days '
        'prior to your appointment, for a detailed history of your injury.  </b>This will save '
        'you time at your scheduled appointment. </p>',
        blank,
        '<p class="cl">If you have no knowledge of this appointment, please contact your '
        'attorney ASAP.</p>',
        blank,
        '<p class="cl">Thank you, </p>',
        blank,
        '<p class="cl">APPOINTMENT DEPARTMENT</p>',
        blank, blank,
        '<p class="cl center"><b>SCHEDULING: (818) 582-2600<br>'
        'P.O. Box 261656, Encino, CA 91426<br>'
        'FAX: (818)855-2466</b></p>',
    ]
    return f'<div class="page" id="p-cover">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- Release of Medical Records
def page_release():
    """Release of Medical Records (page 3).

    Editable: Date, To (3 recipient lines), Signature. Locked tokens (rendered as
    underlined text): patient Name, DOB, Date of Injury, SSN.
    """
    def fld(name, cls):
        return f'<input type="text" class="fld {cls}" name="packet.patient.release.{name}">'

    parts = [
        '<div class="rel-title">RELEASE OF MEDICAL RECORDS</div>',
        f'<div class="r-line"><span class="r-key">Date:</span>{fld("date", "f-line")}</div>',
        f'<div class="r-line"><span class="r-key">To:</span>{fld("to", "f-line")}</div>',
        f'<div class="r-line"><span class="r-key"></span>{fld("to_addr2", "f-line")}</div>',
        f'<div class="r-line"><span class="r-key"></span>{fld("to_addr3", "f-line")}</div>',
        '<p class="r-please">Please furnish my medical history, x-rays, treatment, '
        'medication,  MRI\u2019s, and other information in your possession pertinent to '
        'my  medical care to:</p>',
        '<p class="r-inst">West Coast Spine Institute</p>',
        '<p class="r-addr">16530 VENTURA BLVD.,</p>',
        '<p class="r-addr">STE. 130</p>',
        '<p class="r-addr">ENCINO, CA 91436</p>',
        '<p class="r-phone">PHONE: (818) 582-2600</p>',
        '<p class="r-fax">FAX: (818) 855-2466</p>',
        '<p class="r-field r-gaptop"><span class="lab">Patient\u2019s Name:</span>'
        f'<b class="tok-ul">{_tok("##Patients.FirstName##")}  {_tok("##Patients.LastName##")}</b></p>',
        '<p class="r-field"><span class="lab">Date of Birth:</span>'
        f'<span class="tok-ul">{_tok("##Patients.DateOfBirth##")}</span></p>',
        '<p class="r-field"><span class="lab">Date of Injury:</span>'
        f'<span class="tok-ul">{_tok("##InjuryDetails.DateOfInjury##")}</span></p>',
        '<p class="r-field"><span class="lab">Social Security Number:</span>&nbsp;&nbsp;'
        f'<span class="tok-ul">{_tok("##Patients.SocialSecurityNumber##")}</span></p>',
        f'<p class="r-sign"><span class="lab">Signature:</span> {fld("signature", "f-sign")}</p>',
    ]
    return f'<div class="page" id="p-release">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- Privacy Policy Statement
def page_privacy():
    """Privacy Policy Statement (page 4) -- fully static legal text, no fields."""
    blocks = [
        ("h", "Privacy Policy Statement"),
        ("p", "Purpose: The following privacy statement ensures that this medical practice "
              "complies with Federal and State privacy laws and regulations. Any violations of "
              "these provisions will result in severe disciplinary action including termination "
              "of employment and/or criminal prosecution."),
        ("p", "Effective April 14, 2003, it is the policy of our office to comply with the "
              "California Laws, and maintain compliance with HIPAA requirements."),
        ("h", "Notice of Privacy Practice"),
        ("p", "It is our policy that the notice explaining our privacy practice be provided to "
              "any and all patients. All uses and disclosures of \u201cprotected health "
              "information\u201d is done in accordance with our privacy practices, and a copy "
              "will be posted in the waiting room. Copies will be available for distribution at "
              "the reception desk."),
        ("h", "Assigning Privacy"),
        ("p", "It is our office policy that we will make sufficient resources available and "
              "authorization of select individuals designated as compliance officers to fulfill "
              "their responsibilities in the maintenance of patient privacy and security, and "
              "compliance of HIPAA requirements."),
        ("h", "Minimum Use and Disclosures of Protected Health Information"),
        ("p", "It is our office policy for all routine uses and disclosures of PHI that these "
              "will be limited to disclosures authorized by the patient, those required for "
              "treatment purposes or those required by HIPAA law. All other requests will be "
              "limited to the amount of information needed to accomplish the purpose of the request."),
        ("h", "Marketing"),
        ("p", "It is our policy that any disclosures of PHI for marketing be done only after "
              "authorization is in effect. Any face to face communication made by us to the "
              "patient or a promotional gift of nominal value given to the patient does not "
              "require an authorization."),
        ("h", "Complaints"),
        ("p", "It is our policy that all complaints relating to the protection of the health "
              "information be investigated and resolved timely. It is prohibited for any "
              "employee or contractor to engage in any intimidating or retaliating acts against "
              "persons filing complaints under the HIPAA regulations. It is also our policy that "
              "no employee or contractor shall condition treatment, payment, enrollment or "
              "eligibility for benefits to obtain authorization to disclose protected health "
              "information as expressly authorized under the regulations."),
        ("h", "Responsibility and Identification"),
        ("p", "It is our office policy that our office and staff are responsible for implementing "
              "policy and procedures. It is also our responsibility to perform verification of "
              "identification of anyone seeking access to protected health information, and any "
              "unauthorized use or disclosure of protected health information be mitigated to "
              "the extent possible."),
    ]
    rows = [f'<p class="pp-{kind}">{text}</p>' for kind, text in blocks]
    return f'<div class="page" id="p-privacy">{"".join(rows)}</div>'


# ----------------------------------------------------------------------------- Pregnancy/X-ray Ack + Emergency
def page_pregnancy():
    """Pregnancy/X-ray Acknowledgement + Emergency Contact (page 5; bilingual EN/ES).

    Locked tokens: NAME/ACCT/DATE header. Editable: LMP + signature/date (EN & ES),
    and emergency name/relationship/phone (EN & ES). Letterhead is an image crop.
    """
    def f(name, cls):
        return f'<input type="text" class="fld {cls}" name="packet.patient.pregnancy.{name}">'

    parts = [
        '<img class="pg-head" src="images/pregnancy_letterhead.png" '
        'alt="Yuri Falkinstein, M.D., FAAOS -- Fellow, American Academy of Orthopaedic Surgeons">',
        '<div class="pg-idblock">'
        f'<div class="pg-id">NAME: {_tok("##Patients.FirstName##")}  {_tok("##Patients.LastName##")}</div>'
        f'<div class="pg-id">ACCT: <span class="vr">{_tok("##Appointments.RequestConfirmationNumber##")}</span></div>'
        f'<div class="pg-id">DATE: <span class="vr">{_tok("##Appointments.AvailableDate##")}</span></div>'
        '</div>',
        '<p class="pg-ack">TO THE BEST OF MY KNOWLEDGE, I AM NOT PREGNANT, AND I REALIZE '
        'THAT X- RAYS ARE POTENTIALLY HAZARDOUS TO THE FETUS.</p>',
        f'<div class="pg-line">First Day of Last Menstrual Cycle: {f("lmp", "f-lmp")}</div>',
        f'<div class="pg-line">Signature: {f("signature", "f-sig")}</div>',
        f'<div class="pg-line">Date: {f("date", "f-date")}</div>',
        '<p class="pg-ack">NO ESTOY EN CONOCIMIENTO DE ESTAR EMBARAZADA; Y SI ENTIENDO QUE '
        'LOS RAYOS X PUEDEN SER PERJUDICIALIS PARA EL FETO.</p>',
        f'<div class="pg-line">Primer Dia de Ultima Menstruacion: {f("lmp_es", "f-lmp")}</div>',
        f'<div class="pg-line">Firma: {f("firma", "f-sig")}</div>',
        f'<div class="pg-line">Fecha: {f("fecha", "f-date")}</div>',
        '<hr class="pg-rule">',
        '<p class="pg-emh">IN CASE OF EMERGENCY PLEASE NOTIFY:</p>',
        f'<div class="pg-line">(NAME) {f("em_name", "f-nm")} RELATIONSHIP {f("em_rel", "f-rel")}</div>',
        f'<div class="pg-line">PHONE #: 1. {f("em_phone1", "f-nm")} 2. {f("em_phone2", "f-ph")}</div>',
        '<p class="pg-emh">EN CASO DE EMERJENCIA POR FAVOR NOTIFICA:</p>',
        f'<div class="pg-line">(NOMBRE) {f("em_nombre", "f-nm")} RELACION {f("em_relacion", "f-rel")}</div>',
        f'<div class="pg-line">NUMERO DE TELEFONO -  1. {f("em_tel1", "f-nm")} 2. {f("em_tel2", "f-ph")}</div>',
        '<div class="pg-foot-wrap">'
        '<p class="b">Mailing Address: P.O. Box 261656, Encino, CA 91426</p>'
        '<p class="r">Phone: (818) 582-2600 \u2022 Fax: (818) 855-2466</p>'
        '</div>',
    ]
    return f'<div class="page" id="p-pregnancy">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- Privacy Practices Acknowledgement
def page_privacyack():
    """Acknowledgement of Receipt of Notice of Privacy Practices (page 6).

    Locked tokens: patient name (on a rule) + date. Editable: patient signature,
    representative signature, representative relationship (each a blank rule above
    its descriptive label).
    """
    def sig(name):
        return ('<div class="pa-sigblock"><input class="pa-sig" type="text" '
                f'name="packet.patient.privacyack.{name}"></div>')

    parts = [
        '<div class="pa-title">West Coast Spine Institute Acknowledgement of Receipt of '
        'Notice of Privacy Practices</div>',
        '<p class="pa-p"><b>West Coast Spine Institute</b> reserves the right to modify the '
        'privacy practice outlined in this notice.</p>',
        '<p class="pa-p">I have received a copy of the <b>NOTICE OF PRIVACY PRACTICES</b> for:</p>',
        '<p class="pa-wcs"><b>West Coast Spine Institute</b></p>',
        f'<div class="pa-namerule"><span class="pa-name">{_tok("##Patients.FirstName##")}&nbsp;&nbsp;'
        f'{_tok("##Patients.LastName##")}</span></div>',
        '<div class="pa-lbl">Name of Patient (Print or Type)</div>',
        sig("signature"),
        '<div class="pa-lbl">Signature of Patient</div>',
        f'<p class="pa-date">Date<b>: </b><span class="pa-datetok">{_tok("##Appointments.AvailableDate##")}</span></p>',
        sig("rep_signature"),
        '<div class="pa-lbl">Signature of Patient Representative<br>'
        '(Required if patient is a minor or an adult unable to sign this form)</div>',
        sig("rep_relationship"),
        '<div class="pa-lbl">Relationship of Patient Representative to Patient</div>',
    ]
    return f'<div class="page" id="p-privacyack">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- Present Complaints
def page_complaints():
    """Present Complaints (page 7; bilingual). Body images are a freehand draw canvas
    (per decision: print/ink, not structured). All other data is structured: 0-10
    intensity = circle-the-choice highlight; Worse/Same/less + No/Yes = tick-one
    radios over the box glyph; write-ins = text fields."""
    def box(group, value):
        return f'<a class="cc-box" href="cc:packet.patient.complaints.{group}#{value}">\u25a1</a>'

    def num(n):
        return f'<a class="cc-opt" href="cc:hl:packet.patient.complaints.intensity#{n}">{n}</a>'

    def txt(name, cls=""):
        return f'<input type="text" class="pc-fld {cls}" name="packet.patient.complaints.{name}">'

    def q6(label, group, where=True):
        s = (f'<div class="pc-q6"><span class="sx">{label}</span> {box(group, "no")} No/No '
             f'{box(group, "yes")} Yes/Si')
        if where:
            s += f', Where? (Donde) {txt(group + "_where", "w-where")}'
        return s + '</div>'

    legend_rows = [("Ache", "(Dolor)", "^^^^^^"), ("Burning", "(Ardor)", "BBBBB"),
                   ("Numbness", "(Entumecimiento)", "OOOOOO"),
                   ("Pins and Needles", "(Hormigueo)", "- - - - - -"),
                   ("Stabbing", "(Pu\u00f1alada)", "I I I I I I"), ("Bruises", "(Moretones)", "+ + + + + +")]
    legend = ('<table class="pc-legend">'
              '<tr>' + ''.join(f'<td>{n}</td>' for n, _, _ in legend_rows) + '</tr>'
              '<tr>' + ''.join(f'<td>{es}</td>' for _, es, _ in legend_rows) + '</tr>'
              '<tr>' + ''.join(f'<td class="sym">{s}</td>' for _, _, s in legend_rows) + '</tr></table>')

    bodies = (
        '<table class="pc-bodies"><tr>'
        '<td><div class="pc-lrbox">Left<br>(Izquierda)</div></td>'
        '<td class="bodycell"><div class="pc-bf-lbl">BACK<br>(Parte Posterior)</div>'
        '<img src="Posterior_Body.png" alt="posterior body outline"></td>'
        '<td><div class="pc-lrbox">Right<br>(Derecha)</div></td>'
        '<td><div class="pc-lrbox">Right<br>(Derecha)</div></td>'
        '<td class="bodycell"><div class="pc-bf-lbl">FRONT<br>(Parte Anterior)</div>'
        '<img src="Anterior_Body.png" alt="anterior body outline"></td>'
        '<td><div class="pc-lrbox">Left<br>(Izquierda)</div></td>'
        '</tr></table>'
    )

    scale = (
        '<div class="pc-scale">'
        '<div class="row ticks">' + ''.join('<div>I</div>' for _ in range(11)) + '</div>'
        '<div class="row nums">' + ''.join(f'<div>{num(n)}</div>' for n in range(11)) + '</div>'
        '<div class="labels"><div class="lmid" style="flex:0 0 0;text-align:left">No Pain '
        '<span class="lsub">(Sin Dolor)</span></div>'
        '<div class="lmid">Moderate <span class="lsub">(Moderado)</span></div>'
        '<div class="lend">Severe <span class="lsub">(Severo)</span></div></div>'
        '</div>'
    )

    parts = [
        '<div class="pc-title">Present Complaints</div>',
        f'<div class="pc-id">NAME: {_tok("##Patients.FirstName##")}   {_tok("##Patients.LastName##")}</div>',
        f'<div class="pc-id">ACCT#: <span class="vr">{_tok("##Appointments.RequestConfirmationNumber##")}</span>'
        f'      DATE: <span class="vr">{_tok("##Appointments.AvailableDate##")}</span></div>',
        '<div class="pc-instr">Draw the location of your pain in the body outlines, write duration '
        'of the pain as % of the day (25%, 50%, 75%, 90%+), and mark how bad it is on the line   at '
        'the bottom of body outline.( Dibuje la ubicacion de su dolor en el cuerpo del dibujo, escriba '
        'la duracion del dolor en % del dia (25%, 50%, 75%,   90%+) y marque lo malo que es en la linea '
        'el la parte inferior del cuerpo)</div>',
        legend,
        bodies,
        scale,
        f'<div class="pc-q">1. Indicates where the pain travels to by drawing an arrow or noting here '
        f'{txt("q1_travels")}<br>(Indica si el dolor desplaza dibujando una fleche o escriba nada aqui)</div>',
        f'<div class="pc-q">2. Pain in arms compared to neck is (Dolor en brazos en comparacion con el '
        f'cuello es): {box("q2_arms", "worse")} Worse(Peor)  {box("q2_arms", "same")} Same(Igual) '
        f'{box("q2_arms", "less")} less (Menos)</div>',
        f'<div class="pc-q">3. Pain in legs compared to back is(Dolor en piernas en comparacion con '
        f'espalda es):{box("q3_legs", "worse")} Worse(Peor) {box("q3_legs", "same")} Same(Igual)  '
        f'{box("q3_legs", "less")} less (Menos)</div>',
        '<div class="pc-q">4. What makes the pain better? (i.e. medications-over-the-counter or '
        'prescription, rest-for how long, ice/heat,, hot shower) (Que hace que el dolor mejore) '
        f'(i.e medicamento, descanso-por cuanto tiempo, hielo/calor, ducha de agua caliente) {txt("q4_better", "w-full")}</div>',
        f'<div class="pc-q">5. What makes the pain worse? (Que empeora el dolor) {txt("q5_worse", "w-full")}</div>',
        '<div class="pc-q">6. Are you experiencing any of the following? (Esta experimentando '
        'cualquiera de los siguientes)</div>',
        q6("Night pain?(Dolor Nocturno)", "q6_night"),
        q6("Stiffness?(Rigidez)", "q6_stiffness"),
        q6("Spasm?(Espasmo)", "q6_spasm"),
        q6("Tingling?(Hormigueo)", "q6_tingling"),
        q6("Weakness?(Debilidad)", "q6_weakness"),
        q6("Swelling?(Hinchazon)", "q6_swelling"),
        q6("Locking?(Bloqueo)", "q6_locking"),
        q6("Give-way?(Revelacion Involuntaria)", "q6_giveway"),
        q6("Deformity/Scar?(Deformidad/Cicatriz)", "q6_deformity"),
        f'<div class="pc-q6"><span class="sx" style="width:auto">Are there any bowel or bladder '
        f'problems?(Hay problemas de intestino o de la vejiga)</span> {box("q6_bowel", "no")} No/No '
        f'{box("q6_bowel", "yes")} Yes/Si</div>',
        f'<div class="pc-q">Details/(Detalles): {txt("q6_details", "w-details")}</div>',
        f'<div class="pc-q" style="margin-top:6pt">Patient Signature (Firma): {txt("signature", "w-sig")} '
        f'Date(Fecha): {txt("date", "w-date")}</div>',
    ]
    return f'<div class="page" id="p-complaints">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- AMA Questionnaire p.1
def page_ama1():
    """AMA Guidelines Pain Questionnaire, page 1 (Section I Pain A-E, Section II
    Activity Limitation A-G). Each 0-10 scale is a single-select circle-the-choice
    (radio + highlight). Footer normalized to 'Page 1 of 3' (decision 9)."""
    def scale(qid):
        return ('<div class="amascale">'
                + ''.join(f'<a href="cc:hl:packet.patient.ama1.{qid}#{n}">{n}</a>' for n in range(11))
                + '</div>')

    def qbox(text, qid):
        return f'<tr><td><div class="amaqt">{text}</div>{scale(qid)}</td></tr>'

    sec1 = [
        ("A.Rate how severe your pain is right now, at this moment (circle a number)", "s1_now"),
        ("B.Rate how severe your pain is at its worst (circle a number)", "s1_worst"),
        ("C.Rate how severe your pain is on the average (circle a number)", "s1_avg"),
        ("D.Rate how much your pain is aggravated by activity (circle a number)", "s1_aggravated"),
        ("E. Rate how frequent you experience pain (circle a number)", "s1_frequent"),
    ]
    sec2 = [
        ("A. How much does your pain interfere with your ability to walk 1 block? (Circle a number)", "s2_walk"),
        ("B. How much does your pain prevent you from lifting 10 pounds (a bag of groceries)? (Circle a number)", "s2_lift"),
        ("C. How much does your pain interfere with your ability to sit for \u00bd hour? (Circle a number)", "s2_sit"),
        ("D. How much does your pain interfere with your ability to stand for \u00bd hour? (Circle a number)", "s2_stand"),
        ("E. How much does your pain interfere with your ability to get enough sleep? (Circle a number)", "s2_sleep"),
        ("F. How much does your pain interfere with your ability to participate in social activities? (Circle a number)", "s2_social"),
        ("G. How much does your pain interfere with your ability to travel up to 1 hour by car? (Circle a number)", "s2_travel"),
    ]
    parts = [
        '<div class="ama-title">AMA GUIDELINES (5<sup>TH</sup> EDITION)<br>'
        'ACTIVITIES OF DAILY LIVING/PAIN QUESTIONNAIRE</div>',
        '<div class="ama-sub">Table 18-4 Ratings Determining Impairment Associated With Pain</div>',
        '<div class="ama-sec">I. Pain (Self-report of Severity)</div>',
        '<table class="ama">' + ''.join(qbox(t, q) for t, q in sec1) + '</table>',
        '<div class="ama-sum">Sum score of Section I:<br>A-D = Total pain severity /4: '
        '<input type="text" class="ama-fld" name="packet.patient.ama1.sum_section1"></div>',
        '<div class="ama-sec">II.   Activity Limitation of Interference</div>',
        '<table class="ama">' + ''.join(qbox(t, q) for t, q in sec2) + '</table>',
        '<div class="ama-foot">Page 1 of 3</div>',
    ]
    return f'<div class="page amapg" id="p-ama1">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- AMA Questionnaire p.2
def page_ama2():
    """AMA Activity Limitation of Interference (cont.), page 2 (questions A-I, each a
    0-10 circle-the-choice scale with left/right anchor labels). Footer 'Page 2 of 3'."""
    def scale(qid):
        return ('<div class="amascale">'
                + ''.join(f'<a href="cc:hl:packet.patient.ama2.{qid}#{n}">{n}</a>' for n in range(11))
                + '</div>')

    def qbox(text, qid, left=None, right=None):
        anchor = ''
        if left is not None:
            anchor = (f'<div class="amaanchor"><span class="al">{left}</span>'
                      f'<span class="ar">{right}</span></div>')
        return f'<tr><td><div class="amaqt">{text}</div>{scale(qid)}{anchor}</td></tr>'

    rows = [
        qbox("A. In general, how much does your pain interfere with your daily activities? (Circle a number)",
             "a", "Does not interfere with my daily activities", "Completely Interferes with my daily activities"),
        qbox("B. How much do you limit your activities to prevent your pain from getting worse? (Circle a number)", "b"),
        qbox("C. How much does your pain interfere with your relationship with your family/partner/significant others? (Circle a number)",
             "c", "Does not interfere with relationships", "Completely Interferes with relationships"),
        qbox("D. How much does your pain interfere with your ability to do jobs around your home? (Circle a number)",
             "d", "Does not interfere", "Completely unable to do any job around home"),
        qbox("E. How much does your pain interfere with your ability to shower or bathe without help from someone else? (Circle a number)",
             "e", "Does not interfere with relationships", "My pain makes it impossible to shower or bathe without help"),
        qbox("F. How much does your pain interfere with your ability to write or type? (Circle a number)",
             "f", "Does not interfere at all", "Makes it impossible to wrote or type"),
        qbox("G. How much does your pain interfere with your ability to dress yourself? (Circle a number)",
             "g", "Does not interfere", "Makes it impossible to dress"),
        qbox("H. How much does your pain interfere with your ability to engage in sexual activities? (Circle a number)",
             "h", "Does not interfere", "impossible to engage in any sexual activity"),
        qbox("I. How much does your pain interfere with your ability to concentrate? (Circle a number",
             "i", "Never", "All the time"),
    ]
    parts = [
        '<div class="ama-sec">II.   Activity Limitation of Interference (Cont.)</div>',
        '<table class="ama">' + ''.join(rows) + '</table>',
        '<div class="ama-sum">Sum score of Section II:<br>'
        'A \u2013 P = Total score for activities<br>'
        'limitation/16 = Mean Activity<br>'
        'Limitation =<br>'
        '<input type="text" class="ama-fld" name="packet.patient.ama2.sum_section2"></div>',
        '<div class="ama-foot">Page 2 of 3</div>',
    ]
    return f'<div class="page amapg" id="p-ama2">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- AMA Questionnaire p.3
def page_ama3():
    """AMA Individual's Report of Effect of Pain on Mood, page 3 (mood scales A-E with
    anchor labels). Locked tokens: PATIENT NAME + DATE. Editable: SIGNATURE.
    Footer 'Page 3 of 3' (already 'of 3' in source)."""
    def scale(qid):
        return ('<div class="amascale">'
                + ''.join(f'<a href="cc:hl:packet.patient.ama3.{qid}#{n}">{n}</a>' for n in range(11))
                + '</div>')

    def qbox(text, qid, left, right):
        anchor = (f'<div class="amaanchor"><span class="al">{left}</span>'
                  f'<span class="ar">{right}</span></div>')
        return f'<tr><td><div class="amaqt">{text}</div>{scale(qid)}{anchor}</td></tr>'

    rows = [
        qbox("A. Rate your overall mood during the past week (Circle a number)",
             "a", "Extremely high/good", "Extremely low/bad"),
        qbox("B.During the past week, how anxious or worried have you been because of your pain? (Circle a number)",
             "b", "Not at all anxious/worried", "Extremely anxious/worried"),
        qbox("C. During the past week, how depressed have you been because of your pain? (Circle a number)",
             "c", "Not at all depressed", "Extremely depressed"),
        qbox("D.During the past week, how irritable have you been because of your pain? (Circle a number)",
             "d", "Not at all irritable", "Extremely irritable"),
        qbox("E. In general, how anxious/worried are you about performing activities because they might make your pain/symptoms worse? (Circle a number)",
             "e", "Not at all worried", "Extremely anxious/worried"),
    ]
    parts = [
        '<div class="ama-sec">III. Individual\u2019s Report of Effect of Pain on Mood</div>',
        '<table class="ama">' + ''.join(rows) + '</table>',
        '<div class="ama-sum">Sum score of Section III:<br>'
        'A-E \u2013 Total pain impairment attributed to mood state/5 = Mean<br>'
        'Score = <input type="text" class="ama-fld" name="packet.patient.ama3.sum_section3"></div>',
        '<div class="ama-sign">PATIENT NAME (Print)  '
        f'{_tok("##Patients.FirstName##")}   {_tok("##Patients.LastName##")}</div>',
        '<div class="ama-sign">SIGNATURE: '
        '<input type="text" class="ama-fld w-amasig" name="packet.patient.ama3.signature">'
        f' DATE: <span class="tok-ul">{_tok("##Appointments.AvailableDate##")}</span></div>',
        '<div class="ama-foot">Page 3 of 3</div>',
    ]
    return f'<div class="page amapg" id="p-ama3">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- CA DWC Disability Questionnaire (identity)
def page_deu1():
    """CA DWC Employee's Disability Questionnaire, page 1 (page 11). Barcode = faithful
    image crop; identity blanks left EDITABLE (not pre-filled, per decision 2). Each
    field is an entry line with its label BELOW it (government-form style)."""
    def line(name, w):
        return (f'<input type="text" class="deu-line" name="packet.patient.deu1.{name}" '
                f'style="width:{w}">')

    def fld(name, label, w):
        return f'<div class="deu-fld">{line(name, w)}<div class="deu-lab">{label}</div></div>'

    parts = [
        '<div class="deu-head">'
        '<img class="deu-bc" src="images/deu_barcode.png" alt="form barcode">'
        '<div class="deu-h1">STATE OF CALIFORNIA</div>'
        '<div class="deu-h2">Division of Workers\u2019<br>Compensation Disability<br>Evaluation Unit</div>'
        '<div class="deu-h3">EMPLOYEE\u2019S DISABILITY QUESTIONNAIRE</div>'
        '</div>',
        '<div class="deu-box">This form will aid the doctor in determining your permanent '
        'impairment or disability. Please complete this form and give it to the physician who '
        'will be performing the evaluation. The doctor will include this form with his or her '
        'report and submit it to the Disability Evaluation Unit, with a copy to you and your '
        'claims administrator.</div>',
        '<div class="deu-emp">Employee</div>',
        # First Name + MI (line above, label below)
        '<div class="deu-fld"><div class="deu-row"><span class="col-fn">' + line("first_name", "100%")
        + '</span><span class="col-mi">' + line("mi", "100%") + '</span></div>'
        '<div class="deu-row"><span class="col-fn deu-lab">First Name</span>'
        '<span class="col-mi deu-lab">MI</span></div></div>',
        fld("last_name", "Last Name", "2.0in"),
        fld("ssn", "SSN (Numbers Only)", "6.5in"),
        fld("street1", "Street Address 1/PO Box (Please leave blank spaces between numbers, names or words)", "6.6in"),
        fld("street2", "Street Address 2/PO Box (Please leave blank spaces between numbers, names or words)", "6.6in"),
        fld("intl_address", "International Address (Please leave blank spaces between numbers, names or words)", "5.2in"),
        # City / State / Zip
        '<div class="deu-fld"><div class="deu-row"><span class="col-city">' + line("city", "100%")
        + '</span><span class="col-state">' + line("state", "100%") + '</span>'
        '<span class="col-zip">' + line("zip", "100%") + '</span></div>'
        '<div class="deu-row"><span class="col-city deu-lab">City</span>'
        '<span class="col-state deu-lab">State</span><span class="col-zip deu-lab">Zip code</span></div></div>',
        '<div class="deu-date">Date of Birth: ' + line("dob", "2.0in")
        + '<div class="deu-mmdd">MM/DD/YYYY</div></div>',
        '<div class="deu-date">Date of Injury: ' + line("doi", "2.0in")
        + '<div class="deu-mmdd">MM/DD/YYYY</div></div>',
        fld("employer", "Employer", "6.6in"),
        fld("nature_business", "Nature of Employers Business", "6.6in"),
    ]
    return f'<div class="page" id="p-deu1">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- CA DWC Disability Questionnaire (cont.)
def page_deu2():
    """CA DWC Employee's Disability Questionnaire, page 2 (page 12). Claim numbers,
    'Check one' square radios (single-select), short-answer lines, multi-line answer
    boxes (textarea), date/signature. All editable (not pre-filled)."""
    def chk(group, value):
        return f'<a class="dq-chk" href="cc:packet.patient.deu2.{group}#{value}"></a>'

    def inline(name, w):
        return (f'<input type="text" class="dq-line" name="packet.patient.deu2.{name}" '
                f'style="width:{w}">')

    def box(name, h):
        return f'<textarea class="dq-box" name="packet.patient.deu2.{name}" style="height:{h}"></textarea>'

    claims = ''.join(
        f'<div class="dq-claim"><span class="lbl">Claim Number {i}</span>'
        f'<input type="text" class="dq-line dq-claimline" name="packet.patient.deu2.claim{i}"></div>'
        for i in range(1, 6))

    parts = [
        claims,
        '<hr class="dq-rule">',
        '<div class="dq-h">PLEASE ANSWER THE FOLLOWING QUESTIONS FULLY:</div>',
        '<div class="dq-h">How was your evaluating doctor selected? (Check one)</div>',
        '<div class="dq-chkrow">' + chk("doctor_selected", "from_list")
        + 'From a list of doctors provided by the State of California, Division of Workers\u2019 Compensation.</div>',
        '<div class="dq-chkrow">' + chk("doctor_selected", "other")
        + 'Other (Explain) ' + inline("doctor_other", "5.3in") + '</div>',
        '<div class="dq-q">What is the name of the doctor who will be doing the evaluation? '
        + inline("doctor_name", "3.5in") + '</div>',
        '<div class="dq-q">When is your examination scheduled? ' + inline("exam_scheduled", "5.0in") + '</div>',
        '<div class="dq-q">What were your job duties at the time of your injury?'
        + box("job_duties", "0.8in") + '</div>',
        '<div class="dq-q">What is the disability resulting from your injury?'
        + box("disability", "1.1in") + '</div>',
        '<div class="dq-q">How does this injury affect you in your work?'
        + box("work_effect", "1.0in") + '</div>',
        '<div class="dq-q">Have you ever had a disability as a result of another injury or illness? '
        + inline("prior_disability", "3.2in") + '</div>',
        '<div class="dq-q">If so, when? ' + inline("prior_when", "3.0in") + '</div>',
        '<div class="dq-q">Please describe the disability?' + box("describe_disability", "0.8in") + '</div>',
        '<div class="dq-sign">Date' + inline("date", "2.0in")
        + ' MM/DD/YYYY&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; Signature' + inline("signature", "3.4in") + '</div>',
    ]
    return f'<div class="page" id="p-deu2">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- Job Duties (DWC) p.1
# Identity grid + 23-row activity/frequency matrix. Each activity row is one
# 4-value checkmark radio; dominant hand is a 2-value circle choice.
JD_INSTRUCTIONS = (
    "This form shall be developed jointly by the employer and employee and is intended "
    "to describe the employee\u2019s job duties. The completed form will be reviewed by the "
    "treating doctor to determine whether the employee is able to return to his/her job. "
    "This is an important document and should accurately show the requirements of the "
    "employee\u2019s job. If the employee needs help in completing this form, the employee may "
    "contact the Information and Assistance Officer at the Division of Workers\u2019 "
    "Compensation. The phone number can be found in the State Government section of the "
    "phone book."
)

# (label, slug, indented) -- activity rows of the frequency matrix, in document order.
JD_ACTIVITIES = [
    ("Sitting", "sitting", False),
    ("Walking", "walking", False),
    ("Standing", "standing", False),
    ("Bending (neck)", "bending_neck", False),
    ("Bending (waist)", "bending_waist", False),
    ("Squatting", "squatting", False),
    ("Climbing", "climbing", False),
    ("Kneeling", "kneeling", False),
    ("Crawling", "crawling", False),
    ("Twisting (neck)", "twisting_neck", False),
    ("Twisting (waist)", "twisting_waist", False),
    ("Is repetitive use of hand required?", "repetitive_use", True),
    ("Simple Grasping (right hand)", "simple_grasp_right", True),
    ("Simple Grasping (left hand)", "simple_grasp_left", True),
    ("Power Grasping (right hand)", "power_grasp_right", True),
    ("Power Grasping (left hand)", "power_grasp_left", True),
    ("Fine Manipulation (right hand)", "fine_manip_right", True),
    ("Fine Manipulation (left hand)", "fine_manip_left", True),
    ("Pushing &amp; Pulling (right hand)", "push_pull_right", True),
    ("Pushing &amp; Pulling (left hand)", "push_pull_left", True),
    ("Reaching (above shoulder level)", "reach_above", True),
    ("Reaching (below shoulder level)", "reach_below", True),
]


def page_jobduties1():
    """CA DWC 'Description of Employee's Job Duties', page 1 (page 13). Centered header,
    bordered instructions box, identity grid (colgroup 50/25/5.3/19.7 so dividers fall at
    50%, 75%, 80.3% exactly like the docx table), job-responsibilities textarea, and a
    23-row activity x 4-frequency matrix. Each activity row is one 4-value checkmark radio
    (packet.patient.jobduties1.<slug>; never|occasionally|frequently|constantly); the
    dominant hand is a 2-value circle choice. All identity blanks editable (not pre-filled).
    No flexbox: WeasyPrint mis-places form widgets sized by flex-grow, so every input
    carries an explicit width (see plan 'weak flexbox -> CSS tables')."""
    NS = "packet.patient.jobduties1"

    def fill(name, w, block=False):
        cls = "jd-fill jd-blk" if block else "jd-fill"
        return f'<input type="text" class="{cls}" name="{NS}.{name}" style="width:{w}">'

    def mfill(name, w, h="0.3in", block=False):
        """Multi-line wrapping field -- long content wraps + stays visible."""
        disp = "display:block;margin-top:3pt;" if block else ""
        return (f'<textarea class="jd-mt" name="{NS}.{name}" '
                f'style="width:{w};height:{h};{disp}"></textarea>')

    def freq_cells(slug):
        base = f"{NS}.{slug}"
        return ''.join(
            f'<td class="ans"><a class="jd-mxcell" href="cc:{base}#{v}"></a></td>'
            for v in ("never", "occasionally", "frequently", "constantly"))

    # identity grid: 4-col group [50, 25, 5.3, 19.7] -> dividers at 50%, 75%, 80.3%
    # (exact docx table #7). Row 0: EMPLOYEE NAME spans cols 1-3 (80.3%) + CLAIM# col 4;
    # row 1: full width; row 2: JOB TITLE col 1 (50%) + HRS/DAY col 2 (75%) + HRS/WEEK
    # cols 3-4; row 3: full-width responsibilities textarea.
    grid = (
        '<table class="jd-id"><colgroup>'
        '<col style="width:50%"><col style="width:25%">'
        '<col style="width:5.3%"><col style="width:19.7%"></colgroup>'
        '<tr><td colspan="3" class="jd-cell-row">'
        '<div class="jd-nm"><span class="lab">EMPLOYEE NAME:</span>'
        '<span class="c">(LAST)</span><span class="c">(FIRST)</span><span class="c">(M.I.)</span></div>'
        '<div class="jd-nm flds"><span class="lab"></span>'
        f'<span class="c">{mfill("employee_last", "1.3in")}</span>'
        f'<span class="c">{mfill("employee_first", "1.3in")}</span>'
        f'<span class="c">{fill("employee_mi", "0.8in")}</span></div></td>'
        f'<td class="jd-cell-row">CLAIM#:{mfill("claim", "95%", "0.34in", block=True)}</td></tr>'
        '<tr><td colspan="4" class="jd-cell-row jd-lb">'
        f'<span class="jd-il">EMPLOYER NAME:</span>{mfill("employer_name", "2.1in", "0.4in")}'
        f'&nbsp;&nbsp;&nbsp; <span class="jd-il">JOB ADDRESS:</span>{mfill("job_address", "2.3in", "0.4in")}</td></tr>'
        '<tr><td class="jd-cell-row jd-lb">'
        f'<span class="jd-il">JOB TITLE:</span>{mfill("job_title", "2.7in", "0.4in")}</td>'
        f'<td class="jd-cell-row">HRS. WORKED PER DAY:{fill("hrs_per_day", "90%", block=True)}</td>'
        f'<td colspan="2" class="jd-cell-row">HRS. WORKED PER WEEK:{fill("hrs_per_week", "90%", block=True)}</td></tr>'
        '<tr><td colspan="4" class="jd-cell-resp">'
        'DESCRIPTION OF JOB RESPONSIBILITIES:&nbsp; (DESCRIBE ALL JOB DUTIES)'
        f'<textarea class="jd-fill" name="{NS}.job_responsibilities" style="height:0.85in"></textarea>'
        '</td></tr></table>'
    )

    # activity/frequency matrix
    header = (
        '<tr><th>ACTIVITY<span class="sub">(Hours per day)</span></th>'
        '<th>NEVER<span class="sub">0 hours</span></th>'
        '<th>OCCASIONALLY<span class="sub">up to 3 hours</span></th>'
        '<th>FREQUENTLY<span class="sub">3 - 6 hours</span></th>'
        '<th>CONSTANTLY<span class="sub">6 - 8+ hours</span></th></tr>'
    )
    hand_use = (
        '<tr><td class="lbl">Hand Use: Dominant hand&nbsp;&nbsp; '
        f'<a class="jd-hand" href="cc:{NS}.dominant_hand#right">Right</a>---&nbsp;&nbsp; '
        f'<a class="jd-hand" href="cc:{NS}.dominant_hand#left">Left</a>---</td>'
        '<td class="ans"></td><td class="ans"></td><td class="ans"></td><td class="ans"></td></tr>'
    )
    rows = []
    for label, slug, indent in JD_ACTIVITIES:
        if slug == "repetitive_use":
            rows.append(hand_use)
        cls = "lbl ind" if indent else "lbl"
        rows.append(f'<tr><td class="{cls}">{label}</td>{freq_cells(slug)}</tr>')

    matrix = (
        '<table class="jd-mx"><colgroup>'
        '<col style="width:34.2%"><col style="width:16.4%"><col style="width:16.4%">'
        '<col style="width:16.4%"><col style="width:16.6%"></colgroup>'
        + header + ''.join(rows) + '</table>'
    )

    parts = [
        '<div class="jd-state">STATE OF CALIFORNIA</div>',
        '<div class="jd-div">DIVISION OF WORKERS\u2019 COMPENSATION</div>',
        '<div class="jd-title">DESCRIPTION OF EMPLOYEE\u2019S JOB DUTIES</div>',
        f'<div class="jd-instr"><b>INSTRUCTIONS:</b> {JD_INSTRUCTIONS}</div>',
        grid,
        '<div class="jd-check">1. Check the frequency of activity required of the '
        'employee to perform the job.</div>',
        matrix,
    ]
    return f'<div class="page" id="p-jd1">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- Job Duties (DWC) p.2
# (label, slug) weight rows of the lifting/carrying matrix, in document order.
JD2_WEIGHTS = [
    ("0-10 lbs.", "0_10"),
    ("11-25 lbs.", "11_25"),
    ("26-50 lbs.", "26_50"),
    ("51-75 lbs.", "51_75"),
    ("76-100lbs.", "76_100"),
    ("100+ lbs.", "100plus"),
]

# (letter, text, slug) job-requirement rows a-j, in document order. Item j wraps to two
# lines in the original.
JD2_REQUIRES = [
    ("a.", "Driving cars, trucks, forklifts and other equipment", "a"),
    ("b.", "Working around equipment and machinery", "b"),
    ("c.", "Walking on uneven ground", "c"),
    ("d.", "Exposure to excessive noise", "d"),
    ("e.", "Exposure to extremes in temperature, humidity or wetness", "e"),
    ("f.", "Exposure to dust, gas, fumes, or chemicals", "f"),
    ("g.", "Working at heights", "g"),
    ("h.", "Operation of foot controls or repetitive foot movement", "h"),
    ("i.", "Use of special visual or auditory protective equipment", "i"),
    ("j.", "Working with bio-hazards such as:  blood borne pathogens, "
           "sewage, hospital waste, etc", "j"),
]

# (label, name) signature-block rows; left cell label + right cell label.
JD2_SIGN = [
    ("EMPLOYER CONTACT NAME:", "employer_contact_name",
     "EMPLOYER CONTACT TITLE:", "employer_contact_title"),
    ("EMPLOYER REPRESENTATIVE SIGNATURE:", "employer_rep_signature", "DATE:", "employer_rep_date"),
    ("EMPLOYEE\u2019S SIGNATURE:", "employee_signature", "DATE:", "employee_date"),
    ("QUALIFIED REHAB. REPRESENTATIVE SIGNATURE:", "qre_signature", "DATE:", "qre_date"),
]


def page_jobduties2():
    """CA DWC 'Description of Employee's Job Duties', page 2 (page 14). Lifting/Carrying
    matrix (per weight row: two 4-value checkmark radios lift_<w>/carry_<w> + Height and
    Distance text fields), heaviest-item write area, job-requires a-j (2-value Yes/No
    square radio + describe line), Employee/Employer comment boxes, and the signature
    block. All blanks editable; big free-text fields are multi-line so content wraps."""
    NS = "packet.patient.jobduties2"
    FREQ = ("never", "occasionally", "frequently", "constantly")

    def freq_cells(group):
        base = f"{NS}.{group}"
        return ''.join(
            f'<td class="ans"><a class="jd2-mxcell" href="cc:{base}#{v}"></a></td>' for v in FREQ)

    def cell_input(name):
        return f'<td class="hd"><input type="text" class="jd2-cell" name="{NS}.{name}"></td>'

    def mfill(name, h):
        return f'<textarea class="jd2-mt" name="{NS}.{name}" style="height:{h}"></textarea>'

    # ---- lifting/carrying matrix (table #8) ----
    sub = ('<th class="sub"><span class="h">Never</span><span class="h">0 hrs.</span></th>'
           '<th class="sub"><span class="h">Occasionally</span><span class="h">up to 3 hrs.</span></th>'
           '<th class="sub"><span class="h">Frequently</span><span class="h">3-6 hrs.</span></th>'
           '<th class="sub"><span class="h">Constantly</span><span class="h">6-8+ hrs.</span></th>')
    mx_rows = [
        '<tr><th rowspan="2"></th><th class="grp" colspan="4">LIFTING</th>'
        '<th rowspan="2">Height</th><th class="grp" colspan="4">CARRYING</th>'
        '<th rowspan="2">Distance</th></tr>',
        f'<tr>{sub}{sub}</tr>',
    ]
    for label, w in JD2_WEIGHTS:
        mx_rows.append(
            f'<tr><td class="wt">{label}</td>{freq_cells("lift_" + w)}'
            f'{cell_input("lift_height_" + w)}{freq_cells("carry_" + w)}'
            f'{cell_input("carry_distance_" + w)}</tr>')
    matrix = (
        '<table class="jd2-mx"><colgroup>'
        '<col style="width:11.8%"><col style="width:7.2%"><col style="width:11%">'
        '<col style="width:9.2%"><col style="width:9.6%"><col style="width:6.6%">'
        '<col style="width:7.2%"><col style="width:10.9%"><col style="width:9.2%">'
        '<col style="width:9.6%"><col style="width:7.6%"></colgroup>'
        + ''.join(mx_rows) + '</table>')

    # ---- job-requires + comments + signatures (table #9) ----
    def chk(slug, value):
        return f'<a class="jd2-chk" href="cc:{NS}.requires_{slug}#{value}"></a>'

    req_rows = [
        '<tr><td rowspan="2" class="item jd2-hq">3.&nbsp; Please indicate if your job '
        'requires:</td><td class="jd2-yes">Yes</td><td class="jd2-no">NO</td>'
        '<td class="jd2-dh">(IF YES, PLEASE BRIEFLY DESCRIBE)</td></tr>',
        '<tr><td></td><td></td><td></td></tr>',
    ]
    for letter, text, slug in JD2_REQUIRES:
        req_rows.append(
            f'<tr><td class="item">{letter}&nbsp; {text}</td>'
            f'<td class="bx">{chk(slug, "yes")}</td><td class="bx">{chk(slug, "no")}</td>'
            f'<td class="desc"><textarea class="jd2-desc" name="{NS}.requires_{slug}_desc" '
            f'style="height:0.26in"></textarea></td></tr>')
    req_rows.append('<tr><td colspan="4" class="cmt">Employee Comments:'
                    + mfill("employee_comments", "0.42in") + '</td></tr>')
    req_rows.append('<tr><td colspan="4" class="cmt">Employer Comments:'
                    + mfill("employer_comments", "0.42in") + '</td></tr>')
    for llab, lname, rlab, rname in JD2_SIGN:
        extra = ('<div class="jd2-iflab">(IF APPLICABLE)</div>'
                 if lname == "qre_signature" else '')
        req_rows.append(
            f'<tr><td class="sig">{llab}{mfill(lname, "0.34in")}{extra}</td>'
            f'<td colspan="3" class="sig sig-r">{rlab}{mfill(rname, "0.34in")}</td></tr>')
    reqtable = (
        '<table class="jd2-req"><colgroup>'
        '<col style="width:50%"><col style="width:6%"><col style="width:6%">'
        '<col style="width:38%"></colgroup>'
        + ''.join(req_rows) + '</table>')

    parts = [
        '<div class="jd2-instr">2.&nbsp; Please indicate the daily Lifting and Carrying '
        'requirements of the job:&nbsp; Indicate the height the object is lifted from floor, '
        'table or overhead location and the distance the object is carried.</div>',
        matrix,
        '<div class="jd2-heavy">Describe the heaviest item required to carry and the '
        'distance to be carried:' + mfill("heaviest_item", "0.5in") + '</div>',
        reqtable,
    ]
    return f'<div class="page" id="p-jd2">{"".join(parts)}</div>'


# ----------------------------------------------------------------------------- Patient Information/Update
def page_patinfo():
    """Patient Information/Update (page 15). Bold title; a top block of bold labels with
    dashed fill lines (date/name/DOB/SSN/phone/address/city/state/zip/email); Attorney,
    Emergency Contact, and Interpreter blocks with bold underlined headings and solid
    underscore fill lines. Every blank is editable and NOT pre-filled. Fields are wide
    single-line underlines matching the original; the generous widths (3-5in) keep
    realistic names/addresses/emails fully visible without the dropped-underline look a
    multi-line field would give on these labeled lines."""
    NS = "packet.patient.patinfo"

    def line(name, w, dashed=False):
        border = "dashed" if dashed else "solid"
        return (f'<input type="text" class="pi-fld" name="{NS}.{name}" '
                f'style="width:{w};border-bottom-style:{border}">')

    gap = '&nbsp;&nbsp;&nbsp; '
    top = (
        '<div class="pi-row">TODAY\u2019S DATE ' + line("today_date", "2in", dashed=True) + '</div>'
        '<div class="pi-row">NAME (FIRST AND LAST) '
        + line("name", "4.8in", dashed=True) + '</div>'
        '<div class="pi-row">DOB ' + line("dob", "1.4in", dashed=True) + gap
        + 'SOC. SEC. #' + line("ssn", "1.3in", dashed=True) + gap
        + 'HOME/CELL PHONE' + line("phone", "1.1in", dashed=True) + '</div>'
        '<div class="pi-row">ADDRESS ' + line("address", "5.3in", dashed=True) + '</div>'
        '<div class="pi-row">CITY ' + line("city", "1.7in", dashed=True) + gap
        + 'STATE' + line("state", "1.5in", dashed=True) + gap
        + 'ZIP CODE' + line("zip", "1.5in", dashed=True) + '</div>'
        '<div class="pi-row">EMAIL ADDRESS' + line("email", "4.4in", dashed=True) + '</div>'
    )
    attorney = (
        '<div class="pi-h">ATTORNEY INFORMATION (IF REPRESENTED)</div>'
        '<div class="pi-row2">NAME OF LAW FIRM: ' + line("law_firm", "4.4in") + '</div>'
        '<div class="pi-row2">NAME OF ATTORNEY: ' + line("attorney_name", "4.2in") + '</div>'
        '<div class="pi-row2">ADDRESS: ' + line("attorney_address", "5.2in") + '</div>'
        '<div class="pi-row2">PHONE NUMBER:' + line("attorney_phone", "4.6in") + '</div>'
    )
    emergency = (
        '<div class="pi-h">EMERGENCY CONTACT INFORMATION</div>'
        '<div class="pi-row3">Emergency Contact Number' + line("emergency_number", "2.6in") + '</div>'
        '<div class="pi-row3">Relationship to patient ' + gap
        + line("relationship", "2.5in") + '</div>'
    )
    interpreter = (
        '<div class="pi-h">INTERPRETER INFORMATION</div>'
        '<div class="pi-row3">Interpreter Name ' + line("interpreter_name", "3.4in") + '</div>'
        '<div class="pi-row3">Interpreter Certification # ' + line("interpreter_cert", "2.5in") + '</div>'
    )

    parts = [
        '<div class="pi-title">PATIENT INFORMATION/UPDATE</div>',
        '<div class="pi-body">' + top
        + '<div style="height:18pt"></div>' + attorney + emergency + interpreter + '</div>',
    ]
    return f'<div class="page" id="p-patinfo">{"".join(parts)}</div>'


# --- page registry: (slug, render-fn). One page per approval gate, in document
# order. New order after the 2026-06-03 template edit: cover letter is page 1;
# Epworth + QME-122 + Service-List pages removed; trailing empty page dropped. ---
PAGES = [
    ("cover", page_cover),
    ("adl", page_adl),
    ("release", page_release),
    ("privacy", page_privacy),
    ("pregnancy", page_pregnancy),
    ("privacyack", page_privacyack),
    ("complaints", page_complaints),
    ("ama1", page_ama1),
    ("ama2", page_ama2),
    ("ama3", page_ama3),
    ("deu1", page_deu1),
    ("deu2", page_deu2),
    ("jobduties1", page_jobduties1),
    ("jobduties2", page_jobduties2),
    ("patinfo", page_patinfo),
]


def build():
    """Assemble all registered pages into patient.html."""
    body = "\n".join(fn() for _, fn in PAGES)
    html = (
        "<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\">\n"
        f"<style>{CSS}</style></head>\n<body>\n{body}\n</body></html>\n"
    )
    html = _inline_images(html)
    with open("patient.html", "w", encoding="utf-8") as fh:
        fh.write(html)
    print(f"patient.html written: {len(PAGES)} page(s) registered")


if __name__ == "__main__":
    build()
