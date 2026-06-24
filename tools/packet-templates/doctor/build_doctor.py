"""Doctor packet fillable-PDF template generator -- single source of truth.

One function per page; assembled into ONE doctor.html (no per-page HTML files).
Rendered by WeasyPrint --pdf-forms, finalized by shared/post_process.py.

Conventions:
- Tokens kept EXACT in <span class="tok">##Group.Field##</span> (never paraphrased).
- Field names: packet.doctor.<page>.<section>.<field>[.<index>][.r|l]  (round-trip).
- Controls: text inputs (measurements/notes), checkboxes (multi-select),
  circle-the-choice radios (single-choice ordinal markers -- added with page 2).
"""

import base64
import mimetypes
import os
import re

# ----------------------------------------------------------------------------- CSS
CSS = r"""
  /* free metric-compatible fonts: Carlito=Calibri, Liberation=Arial/Times */
  @page { size: Letter; margin: 0.35in 0.30in 0.20in 0.42in; }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body { font-family: "Carlito","Calibri","Liberation Sans","Arial",sans-serif;
         font-size: 9pt; color:#000; line-height: 1.15; }

  /* each logical page -> its own PDF page */
  .page { break-after: page; }
  .page:last-child { break-after: auto; }

  .hdr { display: flex; justify-content: space-between; font-size: 11pt; }
  .hdr .lbl { font-weight: bold; }
  .title { text-align: center; font-weight: bold; font-size: 11pt; margin: 1px 0 7px; }
  .tok { font-style: italic; color:#444; }   /* pre-fill substitution point */

  /* two-column body via CSS table (WeasyPrint flexbox is weak) */
  .cols { display: table; width: 100%; table-layout: fixed; }
  .left { display: table-cell; width: 3.5in; vertical-align: top; }
  .right { display: table-cell; vertical-align: top; padding-left: 0.28in; }

  table { border-collapse: collapse; width: 100%; margin: 0 0 4px; table-layout: fixed; }
  td, th { border: 1px solid #000; padding: 1px 3px; font-size: 9pt; vertical-align: middle; height: 19px; }
  .secthead { text-align: center; font-weight: bold; }
  .colhead { text-align: center; font-weight: bold; }
  .rowlabel { white-space: nowrap; }

  input[type="text"] { display: block; width: 100%; border: none; outline: none; background: transparent;
        font: inherit; padding: 0 3px; margin: 0; box-sizing: border-box; height: 19px; line-height: 19px; }
  td.fieldcell { padding: 0; }
  td.fieldcell input[type="text"] { height: 19px; }

  input[type="checkbox"], input[type="radio"] { display: inline-block; width: 9pt; height: 9pt;
        box-sizing: content-box; margin: 0 4px 0 0; vertical-align: middle; }
  input[type="radio"] { border-radius: 50%; }   /* WeasyPrint bakes a circle, not a square */
  .opt { display: inline-block; white-space: nowrap; margin: 0 5px 1px 0; }
  .obs-row { line-height: 1.25; }
  .indent { padding-left: 16px; }
  .notes td { padding: 0; }
  .notes input[type="text"] { height: 19px; }

  /* ---- page 2 (spinal) ---- */
  table.sp { width: 100%; margin: 0 0 4px; }
  table.sp td, table.sp th { height: 23px; font-size: 11pt; padding: 0 3px; }
  table.sp th { text-align: center; }   /* center all page-2 table heading labels */
  table.sp input[type="text"] { height: 22px; line-height: 22px; }
  td.lbl { text-align: left; white-space: nowrap; }
  .fit { font-size: 8.5pt; }   /* shrink the few labels too long for their cell */
  td.ctr { text-align: center; }
  td.mk { padding: 0; text-align: center; white-space: nowrap; }
  table.bare, table.bare td { border: none; }
  .sptitle { text-align: center; font-weight: bold; font-size: 11pt; margin: 1px 0 2px; }
  .ama { text-align: right; font-size: 7pt; font-style: italic; margin: 0; }
  .cols2 { display: table; width: 100%; table-layout: fixed; margin-top: 3px; }
  .lcell { display: table-cell; width: 54%; vertical-align: top; padding-right: 0.16in; }
  .rcell { display: table-cell; vertical-align: top; }

  /* circle-the-choice marker: a plain anchor glyph (NO form input, so WeasyPrint
     bakes no control box). post_process harvests each anchor's 'cc:' link rectangle
     into a highlight checkbox widget with a blank-off / yellow-highlight appearance. */
  a.cc-opt { display: inline-block; text-align: center; text-decoration: none;
             color: #000; font-size: 11pt; line-height: 21px; }

  /* ---- page 3 (upper extremities) -- denser, so smaller ---- */
  table.ue td, table.ue th { height: 18px; font-size: 8.5pt; padding: 0 1px; }
  table.ue th { text-align: center; }
  table.ue input[type="text"] { height: 16px; line-height: 16px; }
  table.ue a.cc-opt { font-size: 8.5pt; line-height: 15px; }
  /* Fingers table gained a Flex/Ext header row (INDEX DIP fix); tighten its rows so the
     table keeps its prior height and page 3 stays one page. */
  table.ue.fingers td, table.ue.fingers th { height: 16px; }
  table.ue.fingers input[type="text"] { height: 14px; line-height: 14px; }
  /* J-Tech band: the Strength|Orthopedic divider stops at the last content row,
     so the J-Tech row (and rows below it) read as one open band (matches original). */
  table.ue td.obl { border-right: none; }
  table.ue td.obr { border-left: none; }

  /* ---- page 5 (fee ticket) -- header band + dense 3-column billing grid ---- */
  .feetitle { text-align: center; font-weight: bold; font-size: 15pt;
              font-family: "Liberation Serif","Times New Roman",serif; margin: 0 0 4px; }
  table.feehdr { margin: 0; }
  table.feehdr td { font-size: 9pt; height: auto; padding: 2px 5px; vertical-align: top;
                    word-break: break-all; }   /* wrap long tokens inside the cell (no overflow) */
  table.feehdr .lbl { font-weight: bold; }
  /* dense grid: small font, tight rows; rows still expand to fit a wrapped line,
     and because the grid is ONE table the three columns stay row-aligned. */
  table.fee { margin: 0; }
  table.fee td, table.fee th { height: 14px; font-size: 7.5pt; padding: 0 2px; line-height: 12px; }
  table.fee input[type="text"] { height: 13px; line-height: 13px; }
  td.feesec { text-align: center; font-weight: bold; font-style: italic; }
  th.colh { text-align: center; font-weight: bold; }
  td.feeofc, td.feecpt { text-align: center; }
  td.feedesc { text-align: left; }
  .feegap { border: none; }   /* thin borderless gutter between the 3 column groups */

  /* ---- page 6 (orders) -- flow form: checkbox groups + underline write-ins ---- */
  .ordtitle { text-align: center; font-size: 26pt; font-weight: normal; margin: 38px 0 0; }
  .ordsub { text-align: center; font-size: 13pt; margin: 2px 0 24px; }
  .ord { font-size: 12pt; line-height: 1.5; }
  .ord .orow { margin: 0 0 14px; }
  .ord .gap { margin-top: 26px; }
  .ord label.opt { margin-right: 20px; white-space: nowrap; }
  .ord .oline { display: table; width: 100%; }
  .ord .olbl { display: table-cell; width: 1%; white-space: nowrap; padding-right: 5px; vertical-align: bottom; }
  .ord .ofill { display: table-cell; vertical-align: bottom; }
  .ord .imgcol { display: table-cell; vertical-align: top; white-space: nowrap; line-height: 1.7; }
  .ord .indent { padding-left: 0.6in; }
  /* underline-style write-in: a borderless input with only a baseline rule */
  .ord input.uline { border: none; border-bottom: 1px solid #000; background: transparent;
                     font: inherit; height: 18px; line-height: 18px; padding: 0 2px; }
  .ord input.ufill { display: block; width: 100%; }
  .ord input.uin { display: inline-block; vertical-align: baseline; }
  /* multi-line free-text write-ins (Body Parts, Comment) -- long entries wrap, stay visible */
  .ord textarea { display: block; width: 100%; border: none; border-bottom: 1px solid #000;
                  background: transparent; font: inherit; resize: none; padding: 0 2px; line-height: 21px; }

  /* ---- page 7 (DWC 10133.36 Return-to-Work form) -- dense mixed layout ---- */
  .dwc { font-size: 10pt; line-height: 1.3; }
  .dwc-hdr { position: relative; text-align: center; min-height: 56px; margin-bottom: 6px; }
  .dwc-logo { position: absolute; left: 0; top: 0; width: 56px; height: 56px; }
  .dwc-title { font-size: 15pt; font-weight: bold; }
  .dwc-sub { font-size: 10pt; }
  .dwc .row { margin: 7px 0; }
  .dwc .it { font-style: italic; }
  .dwc label.opt { margin-right: 14px; }
  /* info tables (employee / claims / employer) -- labels regular weight, tokens below.
     Token font stays small so the long substitution tokens fit; word-break is a safety net. */
  .dwc table.info { width: 100%; margin: 0; }
  .dwc table.info td { border: 1px solid #000; font-size: 8.5pt; padding: 2px 4px; height: 24px; vertical-align: top; }
  .dwc table.info td.tok-cell { height: 26px; word-break: break-all; }
  /* restrictions grid -- borderless, checkboxes aligned in 5 frequency columns */
  .dwc table.grid { width: 100%; margin: 6px 0; border-collapse: collapse; }
  .dwc table.grid td { border: none; font-size: 10pt; padding: 0 2px; height: 23px; }
  .dwc table.grid td.act { text-align: left; white-space: nowrap; }
  .dwc table.grid td.fh { text-align: center; white-space: nowrap; font-weight: normal; }
  .dwc table.grid td.bx { text-align: center; }
  .dwc table.grid input[type="checkbox"] { margin: 0; }
  .dwc a.cc-opt { font-size: 9pt; line-height: normal; }
  /* underline write-ins + label/fill rows (same idiom as page 6) */
  .dwc .oline { display: table; width: 100%; }
  .dwc .olbl { display: table-cell; width: 1%; white-space: nowrap; padding-right: 5px; vertical-align: bottom; }
  .dwc .ofill { display: table-cell; vertical-align: bottom; padding-right: 14px; }
  .dwc input.uline { border: none; border-bottom: 1px solid #000; background: transparent;
                     font: inherit; height: 15px; line-height: 15px; padding: 0 2px; }
  .dwc input.ufill { display: block; width: 100%; }
  .dwc input.uin { display: inline-block; vertical-align: baseline; }
  /* ---- page 8 (DWC 10133.36 instructions) -- prose, no fields ---- */
  .ins { font-size: 12pt; line-height: 1.34; }
  .ins-h1 { text-align: center; font-weight: bold; font-size: 13pt;
            font-family: "Liberation Serif","Times New Roman",serif; margin: 0; }
  .ins-title { text-align: center; font-size: 16pt; margin: 7px 0 0; }
  .ins-sub { text-align: center; font-size: 11.5pt; margin: 0 0 18px; }
  .ins p { text-align: justify; margin: 0 0 13px; }
  .ins-foot { font-size: 9pt; margin-top: 1.7in; }

  /* free-write boxes (Other Restrictions / Explain) -- multi-line text fields */
  .dwc .box { border: 1px solid #000; padding: 2px 3px; margin: 2px 0; }
  .dwc table.obox { width: 100%; margin: 2px 0; }
  .dwc table.obox td { border: 1px solid #000; vertical-align: top; padding: 2px 3px; }
  .dwc table.obox td.olbl { width: 13%; }   /* wraps to "Other / Restrictions" like the original */
  .dwc textarea { border: none; display: block; width: 100%; background: transparent;
                  font: inherit; resize: none; padding: 0; }
"""

# ----------------------------------------------------------------------------- Page 1
def page1():
    """Physical Examination."""
    return r"""
  <div class="hdr">
    <div><span class="lbl">Patient</span>: <span class="tok">##Patients.FirstName##</span> <span class="tok">##Patients.LastName##</span></div>
    <div><span class="lbl">Date:</span> <span class="tok">##Appointments.AvailableDate##</span></div>
  </div>
  <div class="title">Physical Examination</div>

  <div class="cols">
    <div class="left">

      <table style="width:2.3in">
        <colgroup><col style="width:1.25in"><col style="width:1.05in"></colgroup>
        <tr><td class="secthead" colspan="2">Vitals</td></tr>
        <tr><td class="rowlabel">Height</td><td class="fieldcell"><input type="text" name="packet.doctor.vitals.height"></td></tr>
        <tr><td class="rowlabel">Weight</td><td class="fieldcell"><input type="text" name="packet.doctor.vitals.weight"></td></tr>
        <tr><td class="rowlabel">Blood Pressure</td><td class="fieldcell"><input type="text" name="packet.doctor.vitals.blood_pressure"></td></tr>
        <tr><td class="rowlabel">Pulse</td><td class="fieldcell"><input type="text" name="packet.doctor.vitals.pulse"></td></tr>
        <tr><td class="rowlabel">Respiratory</td><td class="fieldcell"><input type="text" name="packet.doctor.vitals.respiratory"></td></tr>
        <tr><td class="rowlabel">Temperature</td><td class="fieldcell"><input type="text" name="packet.doctor.vitals.temperature"></td></tr>
      </table>

      <table>
        <colgroup><col style="width:2.0in"><col style="width:0.5in"><col style="width:0.5in"><col style="width:0.5in"></colgroup>
        <tr><td class="secthead">Dynamometer (kg)</td><td class="colhead">1</td><td class="colhead">2</td><td class="colhead">3</td></tr>
        <tr><td class="rowlabel">Right Hand</td>
          <td class="fieldcell"><input type="text" name="packet.doctor.dynamometer.right_hand.trial1"></td>
          <td class="fieldcell"><input type="text" name="packet.doctor.dynamometer.right_hand.trial2"></td>
          <td class="fieldcell"><input type="text" name="packet.doctor.dynamometer.right_hand.trial3"></td></tr>
        <tr><td class="rowlabel">Left Hand</td>
          <td class="fieldcell"><input type="text" name="packet.doctor.dynamometer.left_hand.trial1"></td>
          <td class="fieldcell"><input type="text" name="packet.doctor.dynamometer.left_hand.trial2"></td>
          <td class="fieldcell"><input type="text" name="packet.doctor.dynamometer.left_hand.trial3"></td></tr>
        <tr><td colspan="4"><label class="opt"><input type="checkbox" name="packet.doctor.dynamometer.jtech_report">J-Tech Report</label></td></tr>
      </table>

      <table>
        <colgroup><col style="width:2.56in"><col style="width:0.47in"><col style="width:0.47in"></colgroup>
        <tr><td class="secthead">Circumferential Measurements (cm)</td><td class="colhead">R</td><td class="colhead">L</td></tr>
        <tr><td class="rowlabel">Biceps (10 cm supraolecranon)</td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.biceps.r"></td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.biceps.l"></td></tr>
        <tr><td class="rowlabel">Forearm (10 cm infraolecranon)</td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.forearm.r"></td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.forearm.l"></td></tr>
        <tr><td class="rowlabel">Thigh (10 cm infraolecranon)</td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.thigh.r"></td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.thigh.l"></td></tr>
        <tr><td class="rowlabel">Calf (midcalf widest point)</td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.calf.r"></td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.calf.l"></td></tr>
        <tr><td class="rowlabel">Leg Length (ASIS to Medial Malleolus)</td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.leg_length.r"></td><td class="fieldcell"><input type="text" name="packet.doctor.circumference.leg_length.l"></td></tr>
      </table>

      <table>
        <tr><td class="secthead">General Observation</td></tr>
        <tr><td class="obs-row">
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.limps">Limps &#8211; Favoring</label>
          <span class="opt"><input type="radio" name="packet.doctor.obs.limps_side" value="R">R
          <input type="radio" name="packet.doctor.obs.limps_side" value="L">L</span></td></tr>
        <tr><td class="obs-row"><label class="opt"><input type="checkbox" name="packet.doctor.obs.altered_gait">Altered Gait</label></td></tr>
        <tr><td class="obs-row">
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.antalgic">Antalgic</label>
          <span class="opt"><input type="radio" name="packet.doctor.obs.antalgic_side" value="R">R
          <input type="radio" name="packet.doctor.obs.antalgic_side" value="L">L</span>
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.flexed">Flexed</label></td></tr>
        <tr><td class="obs-row"><label class="opt"><input type="checkbox" name="packet.doctor.obs.difficulty_moving">Difficulty Moving During Exam</label></td></tr>
        <tr><td class="obs-row"><label class="opt"><input type="checkbox" name="packet.doctor.obs.assistive_device">Requires Assistive Device</label></td></tr>
        <tr><td class="obs-row indent">
          <label class="opt"><input type="radio" name="packet.doctor.obs.device" value="cane">Cane</label>
          <label class="opt"><input type="radio" name="packet.doctor.obs.device" value="crutch">Crutch</label>
          <label class="opt"><input type="radio" name="packet.doctor.obs.device" value="walker">Walker</label>
          <label class="opt"><input type="radio" name="packet.doctor.obs.device" value="wheelchair">Wheelchair</label>
          <label class="opt"><input type="radio" name="packet.doctor.obs.device" value="scooter">Scooter</label></td></tr>
        <tr><td class="obs-row"><label class="opt"><input type="checkbox" name="packet.doctor.obs.uses_support">Uses Support</label></td></tr>
        <tr><td class="obs-row indent">
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.support.cs_collar">C/S Collar</label>
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.support.ls_support">L/S Support</label>
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.support.sling">Sling</label>
          <span style="white-space:nowrap"><label class="opt"><input type="checkbox" name="packet.doctor.obs.support.wrist_brace">Wrist Brace</label><label class="opt"><input type="checkbox" name="packet.doctor.obs.wrist_r">R</label><label class="opt"><input type="checkbox" name="packet.doctor.obs.wrist_l">L</label></span></td></tr>
        <tr><td class="obs-row indent">
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.knee_brace">Knee Brace</label>
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.knee_r">R</label>
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.knee_l">L</label>
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.ankle_brace">Ankle Brace</label>
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.ankle_r">R</label>
          <label class="opt"><input type="checkbox" name="packet.doctor.obs.ankle_l">L</label></td></tr>
        <tr><td class="obs-row"><label class="opt"><input type="checkbox" name="packet.doctor.obs.other">Other</label></td></tr>
        <tr><td class="fieldcell"><input type="text" name="packet.doctor.obs.other_note.0"></td></tr>
        <tr><td class="fieldcell"><input type="text" name="packet.doctor.obs.other_note.1"></td></tr>
        <tr><td class="fieldcell"><input type="text" name="packet.doctor.obs.other_note.2"></td></tr>
      </table>

      <table>
        <colgroup><col style="width:1.3in"><col style="width:1.0in"><col style="width:1.2in"></colgroup>
        <tr><td class="colhead">MEDICATION</td><td class="colhead">DOSAGE</td><td class="colhead">LAST TAKEN</td></tr>
""" + "".join(
        f'        <tr><td class="fieldcell"><input type="text" name="packet.doctor.meds.{i}.name"></td>'
        f'<td class="fieldcell"><input type="text" name="packet.doctor.meds.{i}.dosage"></td>'
        f'<td class="fieldcell"><input type="text" name="packet.doctor.meds.{i}.last_taken"></td></tr>\n'
        for i in range(7)
    ) + r"""      </table>

    </div>

    <div class="right">
      <table class="notes">
        <tr><td class="secthead">Body Parts and General Notes</td></tr>
""" + "".join(
        f'        <tr><td class="fieldcell"><input type="text" name="packet.doctor.notes.{i}"></td></tr>\n'
        for i in range(42)
    ) + r"""      </table>
    </div>
  </div>
"""

# ----------------------------------------------------------------------------- Page 2 helpers
# glyph entities (ASCII source -> HTML entities)
_END = "&#8211;"    # en dash
_APOS = "&#8217;"   # right single quote
_UPA = "&#8593;"    # up arrow
_DNA = "&#8595;"    # down arrow


def _colgroup(twips):
    """<colgroup> with percentage widths preserving the original twip proportions."""
    tot = sum(twips)
    return "<colgroup>" + "".join(f'<col style="width:{t / tot * 100:.3f}%">' for t in twips) + "</colgroup>"


def _txt(name):
    return f'<input type="text" name="{name}">'


def _jtech(name, what="Range of Motion"):
    return f'<label class="opt"><input type="checkbox" name="{name}">J-Tech Report ({what})</label>'


def _slug(s):
    """Field-name key from a display label (strip span tags + html entities)."""
    s = re.sub(r"<[^>]+>", "", s)
    s = re.sub(r"&#?\w+;", "", s)
    return re.sub(r"[^A-Za-z0-9]+", "_", s).strip("_").lower()


def _cc(name, glyph, w):
    """One circle-the-choice option: a plain anchor whose 'cc:' link rectangle
    post_process turns into a highlight checkbox. NOT a form input, so no baked box."""
    return f'<a class="cc-opt" href="cc:{name}" style="width:{w}pt">{glyph}</a>'


# Every circle-the-choice option becomes an independent highlight CHECKBOX (placed by
# post_process from the anchor rect). Single-choice markers are "circle one by
# convention" (not hard-enforced) -- matching the hand-circled paper form.
def pm_single(name, w=13):
    """+/- options (circle one)."""
    return _cc(name + ".plus", "+", w) + _cc(name + ".minus", "-", w)


def ths(prefix, w=16):
    """Multi-select T H S."""
    return _cc(prefix + ".t", "T", w) + _cc(prefix + ".h", "H", w) + _cc(prefix + ".s", "S", w)


def single_t(name, w=16):
    """A lone T (tenderness)."""
    return _cc(name + ".t", "T", w)


def swa(prefix, w=16):
    """Vascular pulse S W A (circle one)."""
    return _cc(prefix + ".s", "S", w) + _cc(prefix + ".w", "W", w) + _cc(prefix + ".a", "A", w)


def reflex(prefix, w=7):
    """Reflex grade 0-5 (circle one)."""
    return "".join(_cc(f"{prefix}.{d}", str(d), w) for d in range(6))


def sensation(prefix, w=12):
    """Sensation up / N / down (circle one)."""
    return _cc(prefix + ".up", _UPA, w) + _cc(prefix + ".n", "N", w) + _cc(prefix + ".dn", _DNA, w)


def ribcage(prefix, w=11):
    """T ( P L A ) -- T plus P/L/A locations, all multi-select; parens are literal."""
    return (_cc(prefix + ".t", "T", w) + " ("
            + _cc(prefix + ".p", "P", w) + _cc(prefix + ".l", "L", w)
            + _cc(prefix + ".a", "A", w) + ")")


# region data: (label, normal value, key) for ROM; (label, kind, key) for ortho/palp
ROM_C = [("Cervical Flexion", "50", "cervical_flexion"), ("Cervical Extension", "60", "cervical_extension"),
         ("Cervical Rotation", "80", "cervical_rotation"), ("Cervical Lat Flex", "45", "cervical_lat_flex")]
ORTHO_C = [('<span class="fit">Cervical Compression</span>', "single", "cervical_compression"),
           ("Cervical Distraction", "single", "cervical_distraction"), ("Soto Hall", "single", "soto_hall"),
           (f"Spurling{_APOS}s", "bi", "spurlings"), ("Shoulder Depression", "bi", "shoulder_depression"),
           (f"Adson{_APOS}s", "bi", "adsons"), ("Hyperabduction", "bi", "hyperabduction"),
           ("Costoclavicular", "bi", "costoclavicular")]
PALP_C = [("Suboccipital", "ths", "suboccipital"),
          ('<span class="fit">Cervical Paravertebral</span>', "ths", "cervical_paravertebral"),
          ("Levator Scapulae", "ths", "levator_scapulae"), ("Trapezius", "ths", "trapezius"),
          ("Scalenes", "ths", "scalenes"),
          ('<span class="fit">Sternocleidomastoid</span>', "ths", "sternocleidomastoid"),
          ("Cervical Spine", "T", "cervical_spine"), ("Carotid Pulse", "swa", "carotid_pulse")]

ROM_T = [("Thoracic Flexion", "50", "thoracic_flexion"), ("Thoracic Rotation", "30", "thoracic_rotation")]
PALP_T = [("Thoracic Paravertebral", "ths", "thoracic_paravertebral"), ("Rhomboids", "ths", "rhomboids"),
          ("Thoracic Spine", "T", "thoracic_spine"), ("Sternum", "T", "sternum"),
          ("Costosternal Joints", "T2", "costosternal_joints"), ("Ribcage", "ribcage", "ribcage")]

ROM_L = [("Lumbar Flexion", "60", "lumbar_flexion"), ("Lumbar Extension", "25", "lumbar_extension"),
         ("Lumbar Lat Flex", "25", "lumbar_lat_flex")]
ORTHO_L = [("Straight Leg Raise", "bi", "straight_leg_raise"), (f"Braggard{_APOS}s", "bi", "braggards"),
           (f"Kemp{_APOS}s", "bi", "kemps"), ("Patrick-Fabere", "bi", "patrick_fabere"),
           (f"Gaenslen{_APOS}s", "bi", "gaenslens"), (f"Yeoman{_APOS}s", "bi", "yeomans"),
           ("Valsalva", "single", "valsalva"), ("Dejerines Triad", "single", "dejerines_triad"),
           (f"Minor{_APOS}s Sign", "single", "minors_sign")]
PALP_L = [("Lumbar Paraspinal", "ths", "lumbar_paraspinal"),
          ('<span class="fit">Quadratus Lumborum</span>', "ths", "quadratus_lumborum"),
          ("Gluteal Muscles", "ths", "gluteal_muscles"), ("Piriformis", "ths", "piriformis"),
          ("Lumbar Spine", "T", "lumbar_spine"), ("Sacrum", "T", "sacrum"), ("Coccyx", "T", "coccyx"),
          ("Sacroiliac Joint", "T2", "sacroiliac_joint"), ("Sciatic Notch", "T2", "sciatic_notch")]


def _palp_cell(label, kind, base):
    """Palpation row cells (label + R/L markers) for one palpation entry."""
    if kind == "ths":
        return f'<td class="lbl">{label}</td><td class="mk">{ths(base + ".r")}</td><td class="mk">{ths(base + ".l")}</td>'
    if kind == "T":
        return f'<td class="lbl">{label}</td><td class="mk" colspan="2">{single_t(base)}</td>'
    if kind == "T2":
        return f'<td class="lbl">{label}</td><td class="mk">{single_t(base + ".r")}</td><td class="mk">{single_t(base + ".l")}</td>'
    if kind == "swa":
        return f'<td class="lbl">{label}</td><td class="mk">{swa(base + ".r")}</td><td class="mk">{swa(base + ".l")}</td>'
    # ribcage
    return f'<td class="lbl">{label}</td><td class="mk">{ribcage(base + ".r")}</td><td class="mk">{ribcage(base + ".l")}</td>'


def _region(twips, title_html, rom, ortho, palp, base, has_ortho):
    """Build one spinal region table (Cervical/Thoracic/Lumbar)."""
    nrows = max(len(ortho) if has_ortho else 0, len(palp))
    jt_row = len(rom)
    out = [f'<table class="sp">{_colgroup(twips)}']
    hdr = f'<tr><th class="lbl">{title_html}</th><th>N</th><th>R</th><th>L</th>'
    if has_ortho:
        hdr += '<th class="lbl">Orthopedic Testing</th><th>R</th><th>L</th>'
    hdr += '<th class="lbl">Palpation</th><th>R</th><th>L</th></tr>'
    out.append(hdr)
    for i in range(nrows):
        cells = []
        # ROM columns
        if i < len(rom):
            label, nval, key = rom[i]
            cells.append(f'<td class="lbl">{label}</td><td class="ctr">{nval}</td>'
                         f'<td class="fieldcell">{_txt(base + ".rom." + key + ".r")}</td>'
                         f'<td class="fieldcell">{_txt(base + ".rom." + key + ".l")}</td>')
        elif i == jt_row:
            cells.append(f'<td class="lbl" colspan="4">{_jtech(base + ".jtech")}</td>')
        else:
            cells.append('<td colspan="4"></td>')
        # Orthopedic columns
        if has_ortho:
            if i < len(ortho):
                label, kind, key = ortho[i]
                ob = base + ".ortho." + key
                if kind == "single":
                    cells.append(f'<td class="lbl">{label}</td><td class="mk" colspan="2">{pm_single(ob)}</td>')
                else:
                    cells.append(f'<td class="lbl">{label}</td><td class="mk">{pm_single(ob + ".r")}</td>'
                                 f'<td class="mk">{pm_single(ob + ".l")}</td>')
            else:
                cells.append('<td colspan="3"></td>')
        # Palpation columns
        if i < len(palp):
            label, kind, key = palp[i]
            cells.append(_palp_cell(label, kind, base + ".palp." + key))
        else:
            cells.append('<td colspan="3"></td>')
        out.append("<tr>" + "".join(cells) + "</tr>")
    out.append("</table>")
    return "".join(out)


def _nerve_table():
    tw = [854, 898, 898, 840, 892, 658, 710]
    roots = ["C5", "C6", "C7", "C8", "T1", "L4", "L5", "S1"]
    has_reflex = {"C5", "C6", "C7", "L4", "S1"}
    b = "packet.doctor.spinal.nerve"
    o = [f'<table class="sp">{_colgroup(tw)}']
    o.append('<tr><th class="lbl" rowspan="2">Nerve Root</th><th colspan="2">Reflex</th>'
             '<th colspan="2">Sensation</th><th colspan="2">Strength</th></tr>')
    o.append('<tr><th>R</th><th>L</th><th>R</th><th>L</th><th>R</th><th>L</th></tr>')
    for rt in roots:
        rl = rt.lower()
        refr = reflex(f"{b}.{rl}.reflex.r") if rt in has_reflex else ""
        refl = reflex(f"{b}.{rl}.reflex.l") if rt in has_reflex else ""
        o.append(f'<tr><td class="ctr">{rt}</td><td class="mk">{refr}</td><td class="mk">{refl}</td>'
                 f'<td class="mk">{sensation(b + "." + rl + ".sensation.r")}</td>'
                 f'<td class="mk">{sensation(b + "." + rl + ".sensation.l")}</td>'
                 f'<td class="fieldcell">{_txt(b + "." + rl + ".strength.r")}</td>'
                 f'<td class="fieldcell">{_txt(b + "." + rl + ".strength.l")}</td></tr>')
    for label, key in [(f"Hoffman{_APOS}s", "hoffmans"), ("Babinski", "babinski"), ("Clonus", "clonus")]:
        o.append(f'<tr><td class="ctr"></td><td class="lbl" colspan="2">{label}</td>'
                 f'<td class="mk">{pm_single(b + "." + key + ".r")}</td>'
                 f'<td class="mk">{pm_single(b + "." + key + ".l")}</td><td colspan="2"></td></tr>')
    o.append(f'<tr><td class="lbl" colspan="7">{_jtech(b + ".jtech")}</td></tr>')
    o.append("</table>")
    return "".join(o)


def _inspection_table():
    tw = [1454, 2892]
    items = ["Edema", "Bruise", "Atrophy", "Discoloration", "Rash", "Scar", "Abrasion", "Laceration"]
    b = "packet.doctor.spinal.inspection"
    o = [f'<table class="sp">{_colgroup(tw)}', '<tr><th class="lbl">Inspection</th><th></th></tr>']
    for it in items:
        o.append(f'<tr><td class="lbl">{it}</td><td class="fieldcell">{_txt(b + "." + it.lower())}</td></tr>')
    o.append("</table>")
    return "".join(o)


def _neuro_table():
    tw = [1934, 1332, 1260]
    b = "packet.doctor.spinal.neuro"
    o = [f'<table class="sp">{_colgroup(tw)}', '<tr><th class="lbl">Neurological Test</th><th>R</th><th>L</th></tr>']
    for label, key in [("Heel Walk", "heel_walk"), ("Toe Walk", "toe_walk")]:
        o.append(f'<tr><td class="lbl">{label}</td><td class="mk">{pm_single(b + "." + key + ".r")}</td>'
                 f'<td class="mk">{pm_single(b + "." + key + ".l")}</td></tr>')
    o.append(f'<tr><td class="lbl">Rhomberg{_APOS}s</td><td class="mk" colspan="2">{pm_single(b + ".rhombergs")}</td></tr>')
    o.append("</table>")
    return "".join(o)


def page2():
    """Spinal Examination."""
    title = ('<table class="bare" style="width:100%;margin:0 0 2px"><tr>'
             '<td style="width:26%"></td>'
             '<td style="text-align:center;font-weight:bold;font-size:11pt">SPINAL EXAMINATION</td>'
             '<td style="width:26%;text-align:right;font-size:7pt;font-style:italic">'
             'AMA 5TH EDITION PAGE 418, 420, 421</td></tr></table>')
    cerv = _region([1756, 444, 650, 637, 1843, 627, 600, 1711, 1054, 1389],
                   f"Cervical {_END} ROM", ROM_C, ORTHO_C, PALP_C, "packet.doctor.spinal.cervical", True)
    thor = _region([3427, 907, 816, 816, 2276, 1216, 1253],
                   "Thoracic- ROM", ROM_T, [], PALP_T, "packet.doctor.spinal.thoracic", False)
    lumb = _region([1700, 456, 650, 650, 1759, 660, 631, 1671, 1284, 1250],
                   "Lumbar - ROM", ROM_L, ORTHO_L, PALP_L, "packet.doctor.spinal.lumbar", True)
    bottom = ('<div class="cols2"><div class="lcell">' + _nerve_table()
              + '</div><div class="rcell">' + _inspection_table() + _neuro_table() + '</div></div>')
    return (title + cerv
            + '<div class="ama">AMA 5TH EDITION PAGE 411</div>' + thor
            + '<div class="ama">AMA 5TH EDITION PAGE 407, 424</div>' + lumb + bottom)


# ----------------------------------------------------------------------------- Page 3 (Upper Extremities)
# Dense tables (Shoulder/Elbow/Wrist): label | ROM(N,R,L) | Strength(N,R,L) |
# Orthopedic(label,R,L) | Palpation(label,R,L), with two J-Tech checkboxes each.
# rs entry: (label, romN)  -- romN None => strength-only row (label spans ROM cols).
# palp entry: (label, kind) kind in 'ths' | 'T' | 'empty'.
TW_SH = [1253, 619, 444, 564, 425, 379, 341, 1975, 360, 384, 360, 360, 1795, 276, 360, 271, 365, 360, 360]
TW_EL = [1171, 701, 444, 564, 425, 379, 341, 1975, 360, 384, 360, 360, 1795, 276, 360, 271, 365, 360, 360]
TW_WR = [1171, 701, 444, 564, 425, 379, 323, 1993, 360, 384, 360, 360, 3067, 360, 360]

RS_SH = [("Flexion", "180"), ("Extension", "50"), ("Abduction", "180"),
         ("Adduction", "50"), ("Int. Rot.", "90"), ("Ext. Rot", "90")]
ORTHO_SH = ["Arm Drop", "Supraspinatus", "Apprehension", "Speed", f"Yergason{_APOS}s",
            f"Neer{_APOS}s Impingment", "Hawkins Impingment"]
PALP_SH = [("Trapezius", "ths"), ("Parascapular M", "ths"), ("Rhomboids", "ths"), ("Deltoid", "ths"),
           ("Biceps", "ths"), ("Triceps", "ths"), ("Subacromial Spine", "ths"),
           ("Biceps Tendon", "T"), ("AC Joints", "T"), ("Clavicle", "T"), ("Sternoclavicular Jt.", "T")]

RS_EL = [("Flexion", "140"), ("Extension", "0"), ("Supination", "80"), ("Pronation", "80"),
         ("Finger Adbuction (Ulner Nerve)", None)]
ORTHO_EL = [f"Cozen{_APOS}s", f"Mill{_APOS}s", "Elbow Flexion", f"Tinel{_APOS}s Ulnar Nerve",
            f"Tinel{_APOS}s Radial Nerve", "Medial Stability", "Lateral Stability"]
PALP_EL = [("Biceps", "ths"), ("Triceps", "ths"), ("Forearm Flexors", "ths"), ("Forearm Extensors", "ths"),
           ("Biceps Tendon", "T"), ("Olecranon Process", "T"), ("Lateral Epicondyle", "T"),
           ("Medial Epicondyle", "T"), ("Ulnar Groove", "T")]

RS_WR = [("Flexion", "60"), ("Extension", "60"), ("Radial Dev", "20"), ("Ulnar Dev", "30"),
         ("Thumb Abduction (Median Nerve)", None), ("Finger Abduction (Ulnar Nerve)", None)]
ORTHO_WR = [f"Tinel{_APOS}s Median Nerve", f"Tinel{_APOS}s Ulnar Nerve", f"Phalen{_APOS}s",
            f"Reverse Phalen{_APOS}s", "Median N. Comp", f"Finklestein{_APOS}s"]
PALP_WR = [("Dorsal Carpals", "T"), ("Extensor Tendons", "T"), ("Palmer Carpals", "T"), ("Flexor Tendons", "T"),
           ("Snuffbox", "T"), ("Thenar Pad", "T"), ("TFCC", "T"), ("Hypothenar Pad", "T")]


def _upper_region(twips, title, ama, rs, ortho, palp, base, pps):
    # J-Tech row sits below BOTH the ROM/Strength data and the Orthopedic items, so the
    # "Muscle Strength" checkbox (which spans the Orthopedic columns) never collides with
    # an Orthopedic test row.
    jt = max(2 + len(rs), len(ortho) + 1)
    nbody = max(len(ortho), len(palp), jt)
    out = [f'<div class="ama">{ama}</div>' if ama else "", f'<table class="sp ue">{_colgroup(twips)}']
    out.append(f'<tr><th class="lbl">{title}</th><th colspan="3">Range of Motion</th>'
               '<th colspan="3">Strength</th><th class="lbl">Orthopedic Testing</th>'
               f'<th colspan="2">R</th><th colspan="2">L</th><th class="lbl">Palpation</th>'
               f'<th colspan="{pps}">R</th><th colspan="{pps}">L</th></tr>')
    for i in range(1, nbody + 1):
        c = []
        # ROM + Strength (cols 0-6)
        if i == 1:
            c.append('<td></td><td class="ctr">N</td><td class="ctr">R</td><td class="ctr">L</td>'
                     '<td class="ctr">N</td><td class="ctr">R</td><td class="ctr">L</td>')
        elif 2 <= i <= 1 + len(rs):
            label, romN = rs[i - 2]
            k = _slug(label)
            if romN is None:
                c.append(f'<td class="lbl" colspan="4">{label}</td><td class="ctr">5</td>'
                         f'<td class="fieldcell">{_txt(base + ".strength." + k + ".r")}</td>'
                         f'<td class="fieldcell">{_txt(base + ".strength." + k + ".l")}</td>')
            else:
                c.append(f'<td class="lbl">{label}</td><td class="ctr">{romN}</td>'
                         f'<td class="fieldcell">{_txt(base + ".rom." + k + ".r")}</td>'
                         f'<td class="fieldcell">{_txt(base + ".rom." + k + ".l")}</td>'
                         f'<td class="ctr">5</td>'
                         f'<td class="fieldcell">{_txt(base + ".strength." + k + ".r")}</td>'
                         f'<td class="fieldcell">{_txt(base + ".strength." + k + ".l")}</td>')
        elif i == jt:
            c.append(f'<td class="lbl obl" colspan="7">{_jtech(base + ".jtech_rom")}</td>')
        elif i > jt:
            c.append('<td colspan="12"></td>')   # open band below J-Tech (no internal dividers)
        else:
            c.append('<td colspan="7"></td>')    # gap row: ROM done, Orthopedic still running
        # Orthopedic (cols 7-11) -- J-Tech (Muscle Strength) spans this section on the jtech row
        if i <= len(ortho):
            lab = ortho[i - 1]
            ob = base + ".ortho." + _slug(lab)
            c.append(f'<td class="lbl">{lab}</td><td class="mk" colspan="2">{pm_single(ob + ".r", 10)}</td>'
                     f'<td class="mk" colspan="2">{pm_single(ob + ".l", 10)}</td>')
        elif i == jt:
            c.append(f'<td class="lbl obr" colspan="5">{_jtech(base + ".jtech_str", "Muscle Strength")}</td>')
        elif i > jt:
            pass   # already merged into the colspan-12 open band emitted above
        else:
            c.append('<td colspan="5"></td>')
        # Palpation
        if i <= len(palp):
            lab, kind = palp[i - 1]
            pb = base + ".palp." + _slug(lab)
            if kind == "ths":
                c.append(f'<td class="lbl">{lab}</td><td class="mk" colspan="{pps}">{ths(pb + ".r", 11)}</td>'
                         f'<td class="mk" colspan="{pps}">{ths(pb + ".l", 11)}</td>')
            elif kind == "T":
                c.append(f'<td class="lbl">{lab}</td><td class="mk" colspan="{pps}">{single_t(pb + ".r", 12)}</td>'
                         f'<td class="mk" colspan="{pps}">{single_t(pb + ".l", 12)}</td>')
            else:
                c.append(f'<td class="lbl">{lab}</td><td colspan="{pps}"></td><td colspan="{pps}"></td>')
        else:
            c.append(f'<td></td><td colspan="{pps}"></td><td colspan="{pps}"></td>')
        out.append("<tr>" + "".join(c) + "</tr>")
    out.append("</table>")
    return "".join(out)


def _fingers():
    tw = [1253, 714, 913, 1067, 632, 636, 624, 632]
    b = "packet.doctor.upper.fingers"

    def hand(fb):  # Flex/Ext inputs for both hands
        return (f'<td class="fieldcell">{_txt(fb + ".r.flex")}</td><td class="fieldcell">{_txt(fb + ".r.ext")}</td>'
                f'<td class="fieldcell">{_txt(fb + ".l.flex")}</td><td class="fieldcell">{_txt(fb + ".l.ext")}</td>')

    o = [f'<table class="sp ue fingers">{_colgroup(tw)}',
         '<tr><th class="lbl">FINGERS</th><th colspan="3">Range of Motion - Normals</th>'
         '<th colspan="2">Right Hand</th><th colspan="2">Left Hand</th></tr>']
    # THUMB: CMC descriptive (single input per hand), Flexion/Extension header, MCP/IP
    cmc = [("Adduction (0cm)", "adduction"), ("Opposition (8cm)", "opposition"), ("Adbuction (50)", "adbuction")]
    for i, (d, k) in enumerate(cmc):
        lead = '<td class="lbl" rowspan="6">THUMB</td><td class="ctr" rowspan="3">CMC</td>' if i == 0 else ''
        o.append(f'<tr>{lead}<td class="lbl" colspan="2">{d}</td>'
                 f'<td class="fieldcell" colspan="2">{_txt(b + ".thumb.cmc." + k + ".r")}</td>'
                 f'<td class="fieldcell" colspan="2">{_txt(b + ".thumb.cmc." + k + ".l")}</td></tr>')
    o.append('<tr><td></td><td class="ctr">Flexion</td><td class="ctr">Extension</td>'
             '<td colspan="2"></td><td colspan="2"></td></tr>')
    for joint, nf, ne in [("MCP", "60", "0"), ("IP", "80", "0")]:
        o.append(f'<tr><td class="ctr">{joint}</td><td class="ctr">{nf}</td><td class="ctr">{ne}</td>'
                 f'<td class="fieldcell" colspan="2">{_txt(b + ".thumb." + joint.lower() + ".r")}</td>'
                 f'<td class="fieldcell" colspan="2">{_txt(b + ".thumb." + joint.lower() + ".l")}</td></tr>')
    # Flex/Ext hand sub-headers on their OWN row (the original put them on the INDEX DIP row,
    # which robbed INDEX of a DIP input). Now every finger -- INDEX included -- has DIP/PIP/MCP.
    o.append('<tr><td></td><td></td><td colspan="2"></td>'
             '<td class="ctr">Flex</td><td class="ctr">Ext</td><td class="ctr">Flex</td><td class="ctr">Ext</td></tr>')
    for finger in ["INDEX", "MIDDLE", "RING", "LITTLE"]:
        fl = finger.lower()
        for j, (joint, nf, ne) in enumerate([("DIP", "70", "0"), ("PIP", "100", "0"), ("MCP", "90", "0")]):
            lead = f'<td class="lbl" rowspan="3">{finger}</td>' if j == 0 else ''
            o.append(f'<tr>{lead}<td class="ctr">{joint}</td><td class="ctr">{nf}</td><td class="ctr">{ne}</td>'
                     f'{hand(b + "." + fl + "." + joint.lower())}</tr>')
    o.append("</table>")
    return "".join(o)


def _insp_u():
    tw = [1622, 2885]
    items = ["Edema", "Bruise", "Atrophy", "Discoloration", "Rash", "Scar", "Abrasion", "Laceration"]
    b = "packet.doctor.upper.inspection"
    o = [f'<table class="sp ue">{_colgroup(tw)}', '<tr><th class="lbl">Inspection</th><th class="lbl">Location</th></tr>']
    for it in items:
        o.append(f'<tr><td class="lbl">{it}</td><td class="fieldcell">{_txt(b + "." + it.lower())}</td></tr>')
    o.append("</table>")
    return "".join(o)


def _periph():
    tw = [1891, 900, 905]
    b = "packet.doctor.upper.peripheral"
    o = [f'<table class="sp ue">{_colgroup(tw)}',
         '<tr><th class="lbl">Peripheral Nerves</th><th>Right</th><th>Left</th></tr>']
    for n in ["Median", "Ulnar", "Radial"]:
        o.append(f'<tr><td class="lbl">{n}</td><td class="mk">{sensation(b + "." + n.lower() + ".r", 10)}</td>'
                 f'<td class="mk">{sensation(b + "." + n.lower() + ".l", 10)}</td></tr>')
    o.append("</table>")
    return "".join(o)


def _vascular_u():
    tw = [1666, 1108, 872]
    b = "packet.doctor.upper.vascular"
    o = [f'<table class="sp ue">{_colgroup(tw)}',
         '<tr><th class="lbl">Vascular Pulse</th><th>Right</th><th>Left</th></tr>']
    for n in ["Brachial", "Radial", "Ulnar"]:
        o.append(f'<tr><td class="lbl">{n}</td><td class="mk">{swa(b + "." + n.lower() + ".r", 12)}</td>'
                 f'<td class="mk">{swa(b + "." + n.lower() + ".l", 12)}</td></tr>')
    o.append("</table>")
    return "".join(o)


def page3():
    """Upper Extremities."""
    title = '<div class="sptitle">UPPER EXTREMITIES</div>'
    sh = _upper_region(TW_SH, "SHOULDER", f"AMA 5TH ED PG {_END} 476, 477, 479",
                       RS_SH, ORTHO_SH, PALP_SH, "packet.doctor.upper.shoulder", 3)
    el = _upper_region(TW_EL, "ELBOW", "AMA 5TH EDITION PAGE 472, 475",
                       RS_EL, ORTHO_EL, PALP_EL, "packet.doctor.upper.elbow", 3)
    wr = _upper_region(TW_WR, "WRIST", "AMA 5TH EDITION PAGE 467, 469",
                       RS_WR, ORTHO_WR, PALP_WR, "packet.doctor.upper.wrist", 1)
    bottom = ('<div class="cols2"><div class="lcell">'
              '<div class="ama">AMA 5TH EDITION PAGE 456, 457, 459, 460</div>' + _fingers()
              + '</div><div class="rcell">' + _insp_u() + _periph() + _vascular_u() + '</div></div>')
    return title + sh + el + wr + bottom


# ----------------------------------------------------------------------------- Page 4 (Lower Extremities)
# Same ROM/Strength/Orthopedic/Palpation skeleton as page 3, with three faithful variations:
# (1) HIP carries Strength only on its first three ROM rows (the rest are merged-empty);
# (2) KNEE/ANKLE stack the two J-Tech checkboxes vertically in the left column because the
#     Orthopedic list runs far past ROM (the Strength|Orthopedic divider runs full height);
# (3) ANKLE Orthopedic mixes +/- tests and tenderness-T rows, and its Palpation R/L are free text.
# rs entry: (label, romN, strN)  -- strN None => no Strength cell. ortho/palp entry: (label, kind).
TW_HIP = [1243, 617, 444, 564, 425, 379, 341, 1966, 360, 391, 360, 377, 1785, 279, 360, 268, 365, 360, 367]
TW_KNEE = [1164, 696, 444, 564, 425, 379, 341, 1966, 360, 391, 360, 377, 1785, 279, 360, 273, 360, 360, 367]
TW_ANKLE = [1458, 615, 513, 521, 389, 408, 360, 1895, 354, 346, 359, 355, 1773, 260, 343, 269, 343, 344, 343]

RS_HIP = [("Flexion", "100", "5"), ("Extension", "30", "5"), ("Abduction", "40", "5"),
          ("Adduction", "20", None), ("Int. Rot.", "40", None), ("Ext. Rot", "20", None)]
ORTHO_HIP = [("Patrick-Fabere", "bi"), ("Thomas", "bi"), ("Trendelenberg", "bi"), (f"Hibb{_APOS}s", "bi"),
             (f"Gaenslen{_APOS}s", "bi"), ("SI Compression", "bi"), (f"Ober{_APOS}s", "bi")]
PALP_HIP = [("Adductors", "ths"), ("Quadraceps", "ths"), ("Gluteus Medius", "ths"), ("Gluteus Maximus", "ths"),
            ("Piriformis", "ths"), ("Hamstring", "ths"), ("Inguinal Region", "T"), ("G. Trochanter", "T"),
            ("SI Joint", "T")]

RS_KNEE = [("Flexion", "150", "5"), ("Extension", "0", "5")]
ORTHO_KNEE = [("Valgus Stress", "bi"), ("Varus Stress", "bi"), ("Mc Murrays", "bi"), ("Anterior Drawer", "bi"),
              ("Posterior Drawer", "bi"), (f"Lachman{_APOS}s", "bi"), ("Pivot Shift", "bi"),
              ("Patellofemoral Grind", "bi")]
PALP_KNEE = [("Quadraceps", "ths"), ("Hamstring", "ths"), ("Gastrocnemius", "ths"), ("Parapetella", "T"),
             ("Patella", "T"), ("Patellar Tendon", "T"), ("Tibial Tubercle", "T"), ("Medial Joint Line", "T"),
             ("Lateral Joint Line", "T"), ("Popliteal Fossa", "T")]

RS_ANKLE = [("Plantarflexion", "40", "5"), ("Dorsiflexion", "20", "5"),
            ("Inversion", "30", "5"), ("Eversion", "20", "5")]
ORTHO_ANKLE = [("Dorsiflexion", "bi"), ("Anterior Drawer", "bi"), ("Posterior Drawer", "bi"),
               ("Medial Stability", "bi"), ("Lateral Stability", "bi"), (f"Thompson{_APOS}s", "bi"),
               (f"Homan{_APOS}s", "bi"), (f"Tinel{_APOS}s Post Tibial N", "bi"),
               ("Lateral Malleolus", "T"), ("Inf. Tibiofibular Jt", "T"), ("Ant. Talofib Lig", "T"),
               ("Calcaneofibular Lig", "T"), ("Post. Talofib Lig.", "T"), ("Subtalar Join", "T")]
PALP_ANKLE = [("Tibialis Anterior", "ths"), ("Peroneal Mus/Tend.", "ths"), ("Gastrocnemius", "ths"),
              ("Tibialis Posterior", "ths"), ("Plantarfascia", "ths"), ("Mortis Joint", "T"),
              ("Ext. Hallicus Tendon", "T"), ("Ext. Digi Tendon", "T"), ("Cuneaforms", "T"),
              ("Metatarsal 12345", "T"), ("MTP Joint 12345", "T"), ("Medial Malleolus", "T"),
              ("Deltoid Ligament", "T"), ("Calcaneous", "T"), ("Anchilles Tendon", "T")]


def _lower_region(twips, title, ama, rs, ortho, palp, base, pps):
    """One Lower-Extremity region table (Hip / Knee / Ankle).

    Band mode (Orthopedic ends with ROM -- Hip): the two J-Tech checkboxes sit side by
    side in an open band below the data, and the Strength|Orthopedic divider stops at the
    last content row (identical to page 3). Stack mode (Orthopedic runs past ROM --
    Knee/Ankle): the J-Tech checkboxes stack in the left column and the divider runs the
    full height. The mode is chosen by where the J-Tech ROM row lands relative to the
    Orthopedic list length.
    """
    nrom = len(rs)
    jt_rom = 2 + nrom                 # 1-indexed body row of the ROM J-Tech (row 1 = N/R/L sub-header)
    band = len(ortho) < jt_rom
    jt_str = jt_rom if band else jt_rom + 1
    nbody = max(jt_str, len(ortho), len(palp))

    out = [f'<div class="ama">{ama}</div>' if ama else "", f'<table class="sp ue">{_colgroup(twips)}']
    out.append(f'<tr><th class="lbl">{title}</th><th colspan="3">Range of Motion</th>'
               '<th colspan="3">Strength</th><th class="lbl">Orthopedic Testing</th>'
               '<th colspan="2">R</th><th colspan="2">L</th><th class="lbl">Palpation</th>'
               f'<th colspan="{pps}">R</th><th colspan="{pps}">L</th></tr>')

    for i in range(1, nbody + 1):
        c = []
        skip_ortho = False
        # ---- ROM + Strength (cols 0-6) ----
        if i == 1:
            c.append('<td></td><td class="ctr">N</td><td class="ctr">R</td><td class="ctr">L</td>'
                     '<td class="ctr">N</td><td class="ctr">R</td><td class="ctr">L</td>')
        elif 2 <= i <= 1 + nrom:
            label, romN, strN = rs[i - 2]
            k = _slug(label)
            cell = (f'<td class="lbl">{label}</td><td class="ctr">{romN}</td>'
                    f'<td class="fieldcell">{_txt(base + ".rom." + k + ".r")}</td>'
                    f'<td class="fieldcell">{_txt(base + ".rom." + k + ".l")}</td>')
            if strN is None:
                cell += '<td colspan="3"></td>'         # Strength merged-empty (faithful)
            else:
                cell += (f'<td class="ctr">{strN}</td>'
                         f'<td class="fieldcell">{_txt(base + ".strength." + k + ".r")}</td>'
                         f'<td class="fieldcell">{_txt(base + ".strength." + k + ".l")}</td>')
            c.append(cell)
        elif i == jt_rom:
            cls = "lbl obl" if band else "lbl"
            c.append(f'<td class="{cls}" colspan="7">{_jtech(base + ".jtech_rom")}</td>')
        elif (not band) and i == jt_str:
            c.append(f'<td class="lbl" colspan="7">{_jtech(base + ".jtech_str", "Muscle Strength")}</td>')
        elif band and i > jt_rom:
            c.append('<td colspan="12"></td>')           # open band: merge left + Orthopedic
            skip_ortho = True
        else:
            c.append('<td colspan="7"></td>')
        # ---- Orthopedic (cols 7-11) ----
        if not skip_ortho:
            if i <= len(ortho):
                lab, kind = ortho[i - 1]
                ob = base + ".ortho." + _slug(lab)
                if kind == "T":
                    mk = (f'<td class="mk" colspan="2">{single_t(ob + ".r", 11)}</td>'
                          f'<td class="mk" colspan="2">{single_t(ob + ".l", 11)}</td>')
                else:
                    mk = (f'<td class="mk" colspan="2">{pm_single(ob + ".r", 10)}</td>'
                          f'<td class="mk" colspan="2">{pm_single(ob + ".l", 10)}</td>')
                c.append(f'<td class="lbl">{lab}</td>' + mk)
            elif band and i == jt_rom:
                c.append(f'<td class="lbl obr" colspan="5">{_jtech(base + ".jtech_str", "Muscle Strength")}</td>')
            else:
                c.append('<td colspan="5"></td>')
        # ---- Palpation ----
        if i <= len(palp):
            lab, kind = palp[i - 1]
            pb = base + ".palp." + _slug(lab)
            if kind == "ths":
                c.append(f'<td class="lbl">{lab}</td><td class="mk" colspan="{pps}">{ths(pb + ".r", 11)}</td>'
                         f'<td class="mk" colspan="{pps}">{ths(pb + ".l", 11)}</td>')
            elif kind == "T":
                c.append(f'<td class="lbl">{lab}</td><td class="mk" colspan="{pps}">{single_t(pb + ".r", 12)}</td>'
                         f'<td class="mk" colspan="{pps}">{single_t(pb + ".l", 12)}</td>')
            elif kind == "text":
                c.append(f'<td class="lbl">{lab}</td><td class="fieldcell" colspan="{pps}">{_txt(pb + ".r")}</td>'
                         f'<td class="fieldcell" colspan="{pps}">{_txt(pb + ".l")}</td>')
            else:
                c.append(f'<td class="lbl">{lab}</td><td colspan="{pps}"></td><td colspan="{pps}"></td>')
        else:
            c.append(f'<td></td><td colspan="{pps}"></td><td colspan="{pps}"></td>')
        out.append("<tr>" + "".join(c) + "</tr>")
    out.append("</table>")
    return "".join(out)


def _insp_l():
    """Lower-extremity Inspection (label + free-text Location), with the trailing write-in row."""
    tw = [1800, 3096]
    items = ["Edema", "Bruise", "Atrophy", "Discoloration", "Rash", "Scar", "Abrasion", "Laceration"]
    b = "packet.doctor.lower.inspection"
    o = [f'<table class="sp ue">{_colgroup(tw)}', '<tr><th class="lbl">Inspection</th><th class="lbl">Location</th></tr>']
    for it in items:
        o.append(f'<tr><td class="lbl">{it}</td><td class="fieldcell">{_txt(b + "." + it.lower())}</td></tr>')
    o.append(f'<tr><td class="lbl">&nbsp;</td><td class="fieldcell">{_txt(b + ".extra")}</td></tr>')
    o.append("</table>")
    return "".join(o)


def _vascular_l():
    """Lower-extremity Vascular Pulses (S W A circle-the-choice per side)."""
    tw = [1690, 988, 900]
    b = "packet.doctor.lower.vascular"
    o = [f'<table class="sp ue">{_colgroup(tw)}',
         '<tr><th class="lbl">Vascular Pulses</th><th>Right</th><th>Left</th></tr>']
    for n in ["Femoral", "Popliteal", "Dorsalis Pedis", "Posterior Tibial"]:
        o.append(f'<tr><td class="lbl">{n}</td><td class="mk">{swa(b + "." + _slug(n) + ".r", 12)}</td>'
                 f'<td class="mk">{swa(b + "." + _slug(n) + ".l", 12)}</td></tr>')
    o.append("</table>")
    return "".join(o)


def page4():
    """Lower Extremities."""
    ama = '<div class="ama">AMA 5TH EDITION PAGE 537</div>'
    title = '<div class="sptitle">LOWER EXTREMITIES</div>'
    hip = _lower_region(TW_HIP, "HIP", "", RS_HIP, ORTHO_HIP, PALP_HIP, "packet.doctor.lower.hip", 3)
    knee = _lower_region(TW_KNEE, "KNEE", "", RS_KNEE, ORTHO_KNEE, PALP_KNEE, "packet.doctor.lower.knee", 3)
    ankle = _lower_region(TW_ANKLE, "ANKLE", "", RS_ANKLE, ORTHO_ANKLE, PALP_ANKLE, "packet.doctor.lower.ankle", 3)
    bottom = ('<div class="cols2"><div class="lcell">' + _insp_l()
              + '</div><div class="rcell">' + _vascular_l() + '</div></div>')
    return title + hip + ama + knee + ama + ankle + ama + bottom


# ----------------------------------------------------------------------------- Page 5 (Fee Ticket)
# One header band (checkboxes + tokens) above a 3-column billing grid. Each grid column is an
# independent list of section headers ("h") and item rows ("i", ofc, cpt, desc); blanks ("b")
# pad a column where its neighbour still has rows. The grid is ONE 14-column table (3 groups of
# 4 + 2 borderless gutter columns) so the three columns stay row-aligned exactly as the original.
# Per Adrian: FEE is the only fillable cell per row; inline description blanks stay literal text.
# OFC/CPT/DESCRIPTION are pre-printed verbatim (misspellings + odd codes preserved).

# twip widths of the 14 grid columns (OFC|CPT|DESC|FEE  gap  OFC|CPT|DESC|FEE  gap  OFC|CPT|DESC|FEE)
TW_FEE = [451, 631, 1910, 430, 106, 523, 631, 2090, 430, 106, 523, 631, 1819, 521]

FEE_LEFT = [
    ("h", "MED LEGAL"),
    ("i", "102", "ML102", "Basic Med/Legal Eval."),
    ("i", "103", "ML103", "Complex Med Legal Eval."),
    ("i", "104", "ML104", "Med/Legal By Report x _____ hr."),
    ("i", "106", "ML106", "Supplemental Med Legal x _____ hr."),
    ("i", "101", "ML101", "Follow-Up Med Legal"),
    ("i", "100", "ML100", "Missed Appointment"),
    ("b",),
    ("h", "NEW PATIENT INITIAL"),
    ("i", "107", "99205", "High Complexity (60 min)"),
    ("i", "108", "99204", "Moderate to High Complexity (45 min)"),
    ("i", "109", "99203", "Moderate Complexity (30 min)"),
    ("i", "110", "99202", "Low Complexity (20 min)"),
    ("i", "111", "99354", "Prolonged direct contact (30-74 min)"),
    ("i", "112", "99355", "Each additional 30 min. beyond 74"),
    ("b",),
    ("h", "FOLLOW-UP VISIT"),
    ("i", "113", "99215", "Complex F/U Visit (40 min)"),
    ("i", "114", "99214", "Intermediate F/U Visit (25 min)"),
    ("i", "115", "99213", "Basic F/U Visit (15 min)"),
    ("i", "116", "99212", "Low Complexity F/U Visit (10 min)"),
    ("i", "117", "99024", "Post Op"),
    ("b",),
    ("h", "REPORTS"),
    ("i", "202", "WC002", "PR-2"),
    ("i", "204", "WC004", "PR-4 P & S Report"),
    ("i", "205", "99199", "Authorized Review of Records"),
    ("i", "206", "99442", "Peer/Peer Phone Conference 11-20"),
    ("i", "207", "99443", "Peer/Peer Phone Conference 21-30"),
    ("b",),
    ("h", "PI PATIENTS"),
    ("i", "901", "", "Consultation Exam"),
    ("i", "902", "", "Initial Exam"),
    ("i", "903", "", "Follow Up Exam"),
    ("i", "904", "", "Final Exam"),
    ("b",),
    ("h", "IME"),
    ("i", "400", "", "IME Evaluation"),
    ("b",),
    ("h", "INJECTIONS"),
    ("i", "701", "J3301", "Kenalog 40mg/ML vial"),
    ("i", "702", "J7638", "Dexamethasone 120mg/30ML vial"),
    ("i", "703", "J3490", "Marcaine 0.5 vial"),
    ("i", "704", "J2001", "Lidocaine HCL 1% vial"),
    ("i", "705", "J7321", "Supartz 1 2 3 4 5 R or L"),
    ("i", "706", "J7325", "Synvisc"),
    ("i", "08", "20552", "Trigger Point 1-2 areas"),
    ("i", "709", "20553", "Trigger Point Multiple Areas"),
    ("i", "710", "20610", "Injection of Large Joint"),
    ("i", "711", "20605", "Injection of Small Joints"),
]

FEE_MIDDLE = [
    ("h", "MODIFIERS"),
    ("i", "94", "94", "AME"),
    ("i", "95", "95", "PANEL QME"),
    ("i", "24", "24", "Unrelated E&M Post OP Period"),
    ("i", "25", "25", "Separately identifiable same day"),
    ("i", "57", "57", "Decision for Surgery"),
    ("i", "59", "59", "Distinct Procedure"),
    ("i", "93", "93", "Interpreter for ML102 & ML103 only"),
    ("b",),
    ("h", "TESTING"),
    ("i", "302", "95831", "Muscle Test Extrm/Trunk"),
    ("i", "303", "95832", "Muscle Test Hand Manual"),
    ("i", "304", "95833", "Muscle Test Body No Hand"),
    ("i", "305", "95834", "Muscle Test Body No Hand"),
    ("i", "306", "95851", "ROM ea Extremity"),
    ("i", "307", "95852", "ROM Hands"),
    ("b",),
    ("h", "MISCELLANEOUS CHARGES"),
    ("i", "WC009", "WC009", "Duplicate Reports # of Pages"),
    ("i", "WC0010", "WC0010", "Duplicate X-rays # of Pages ___"),
    ("i", "WC0011", "WC0011", "Duplicate Scan # of Pages"),
    ("i", "WC0012", "WC0012", "Missed Appt"),
    ("b",),
    ("h", "LABORATORY"),
    ("i", "801", "9900", "Specimen handling for UA"),
    ("b",),
    ("h", "MEDICATIONS"),
    ("i", "501", "", "Naproxen Sodium 550mg #60"),
    ("i", "502", "", "Amitriptyline 50mg #30"),
    ("i", "503", "", "Codeine/Acetaminophen 30/300mg#60"),
    ("i", "504", "", "Cyclobenzaprine 10mg #60"),
    ("i", "505", "", "Hydrocodone/APAP 10/325mg #60"),
    ("i", "507", "", "Ibuprofen 800mg #60"),
    ("i", "509", "", "Omeprazole 20mg #60"),
    ("i", "510", "", "Tramadol 50mg #60"),
    ("i", "511", "", "Zolpidem 5mg #30"),
    ("i", "513", "", "Codeine/Acetaminophen 30/300mg #60"),
    ("i", "514", "", "Tizanidine HCL 4mg"),
    ("i", "515", "", "Carisoprodol 350mg"),
    ("i", "521", "", "Etodolac 400mg"),
    ("i", "543", "", "Butalbital 50/325 40mg"),
    ("i", "551", "", "Methocarbomal 750mg"),
    ("i", "554", "", "Temazepam"),
    ("i", "555", "", "BioTherm 4oz"),
    ("i", "556", "", "Diclofenac Sodium 100mg #60"),
]

FEE_RIGHT = [
    ("h", "X-RAYS"),
    ("i", "602", "73600", "Ankle 2 V"),
    ("i", "603", "73610", "Ankle 3 V"),
    ("i", "604", "72040", "Cervical Spine 3 V or Less"),
    ("i", "605", "72050", "Cervical 4 or 5 V"),
    ("i", "606", "72052", "Cervical Spine 6 or More V"),
    ("i", "607", "71020", "Chest 2 V Frontal and Lateral"),
    ("i", "608", "73000", "Clavicle Complete"),
    ("i", "609", "72070", "Thoracic Spine 2 V"),
    ("i", "610", "73070", "Elbow 2 V"),
    ("i", "611", "73080", "Elbow 3 V"),
    ("i", "644", "73551", "Femur 1 V"),
    ("i", "645", "73552", "Femur 2 V"),
    ("i", "613", "73140", "Finger 2 V"),
    ("i", "614", "73620", "Foot 2 V"),
    ("i", "615", "73630", "Foot 3 V"),
    ("i", "616", "73090", "Forearm 2 V"),
    ("i", "617", "73120", "Hand 2 V"),
    ("i", "619", "73130", "Hand 3 V"),
    ("i", "646", "73501", "Hip, Unilateral 1 V"),
    ("i", "647", "73502", "Hip, Unilateral 2 - 3 V"),
    ("i", "648", "73503", "Hip, Unilateral 4 V"),
    ("i", "649", "73521", "Hip, Bilateral 2 V"),
    ("i", "650", "73522", "Hip, Bilateral 3 - 4 V"),
    ("i", "651", "7323", "Hip, Bilateral 5 V"),
    ("b",),
    ("i", "621", "73060", "Humerus 2 V"),
    ("i", "622", "73560", "Knee 1 V or 2 V"),
    ("i", "623", "73562", "Knee 3 V"),
    ("i", "624", "73564", "Knee 4 V or more V"),
    ("i", "625", "73565", "Weightbearing- Both Knees Standing"),
    ("i", "626", "72100", "Lumbar Spine 2 or 3 V"),
    ("i", "627", "72110", "Lumbar Spine 4 V"),
    ("i", "629", "72120", "Pelvis 1 V or 2 V"),
    ("i", "630", "72190", "Pelvis Complete 3 V"),
    ("i", "631", "71100", "Ribs Unilateral 2 V"),
    ("i", "634", "72220", "Sacrum/Coccyx Minimum 2 V"),
    ("i", "635", "73010", "Scapula Complete"),
    ("i", "636", "73030", "Shoulder Complete 2 V"),
    ("i", "637", "73020", "Shoulder 1 V"),
    ("i", "639", "71120", "Sternum 2 V"),
    ("i", "640", "73590", "Tibia and Fibula 2 V"),
    ("i", "641", "73660", "Toes 2 V"),
    ("i", "642", "73100", "Wrist 2 V"),
    ("i", "643", "73110", "Wrist 3 V"),
]


def _fee_col(entry, col):
    """Render one grid column's cells for a row: section header (colspan 4), item
    (OFC | CPT | DESCRIPTION | fillable FEE), or blank (colspan 4)."""
    kind = entry[0]
    if kind == "h":
        return f'<td class="feesec" colspan="4">{entry[1]}</td>'
    if kind == "i":
        _, ofc, cpt, desc = entry
        fee = _txt(f"packet.doctor.fee.c{col}.{_slug(ofc) or 'r'}")
        return (f'<td class="feeofc">{ofc}</td><td class="feecpt">{cpt}</td>'
                f'<td class="feedesc">{desc}</td><td class="fieldcell">{fee}</td>')
    return '<td colspan="4"></td>'


def _fee_header():
    """Checkbox band + token rows above the grid (its own 4-column table)."""
    def cb(name, label=""):
        inner = f'<input type="checkbox" name="packet.doctor.fee.opt.{name}">{label}'
        return f'<label class="opt">{inner}</label>' if label else inner
    tok = lambda t: f'<span class="tok">{t}</span>'
    return (
        f'<table class="feehdr">{_colgroup([2676, 2006, 4320, 1800])}'
        f'<tr><td>{cb("box1")}</td><td>{cb("box2")}</td>'
        f'<td>{cb("ame", "AME")} {cb("qme", "QME")}</td>'
        f'<td>{cb("doctor", "Yuri Falkinstein M.D.")}</td></tr>'
        f'<tr><td><span class="lbl">Date:</span> {tok("##Appointments.AvailableDate##")}</td>'
        f'<td><span class="lbl">Acct:</span> {tok("##Appointments.RequestConfirmationNumber##")}</td>'
        f'<td><span class="lbl">Acct Type:</span> {_txt("packet.doctor.fee.acct_type")}</td>'
        f'<td></td></tr>'
        f'<tr><td colspan="2"><span class="lbl">Name:</span> '
        f'{tok("##Patients.FirstName##")} {tok("##Patients.LastName##")}</td>'
        f'<td><span class="lbl">Type:</span> {tok("##Appointments.AppointmentType##")}</td>'
        f'<td></td></tr></table>')


def page5():
    """Fee Ticket -- header band + 3-column billing grid (FEE column fillable)."""
    out = ['<div class="feetitle">Fee Ticket</div>', _fee_header(), f'<table class="fee">{_colgroup(TW_FEE)}']
    ch = '<th class="colh">OFC CODE</th><th class="colh">CPT/ RVS</th><th class="colh">DESCRIPTION</th><th class="colh">FEE</th>'
    out.append(f'<tr>{ch}<th class="feegap"></th>{ch}<th class="feegap"></th>'
               '<th class="colh">OFC CODE</th><th class="colh">CPT/ RVS</th>'
               '<th class="colh">DESCRIPTION V=VIEW</th><th class="colh">FEE</th></tr>')
    n = max(len(FEE_LEFT), len(FEE_MIDDLE), len(FEE_RIGHT))   # 50 grid rows (r4..r53)
    for i in range(n):
        cells = [_fee_col(FEE_LEFT[i], 1), '<td class="feegap"></td>']
        if i < len(FEE_MIDDLE) and i < len(FEE_RIGHT):
            cells += [_fee_col(FEE_MIDDLE[i], 2), '<td class="feegap"></td>', _fee_col(FEE_RIGHT[i], 3)]
        elif i == len(FEE_MIDDLE):   # middle + right exhausted -> sticker header spans both (colspan 9)
            cells.append('<td class="feesec" colspan="9">PLACE MEDICATION STICKERS BELOW</td>')
        elif i == len(FEE_MIDDLE) + 1:   # one open box for the stickers (no internal lines, like Word)
            cells.append(f'<td colspan="9" rowspan="{n - i}"></td>')
        # remaining sticker rows are covered by the rowspan above -> only LEFT + gutter emitted
        out.append("<tr>" + "".join(cells) + "</tr>")
    out.append("</table>")
    return "".join(out)


# ----------------------------------------------------------------------------- Page 6 (Orders)
# Flow-layout form (no tables in the original): imaging/testing checkbox groups plus free-text
# write-in lines. "Dr. Yuri Falkinstein" is literal (no token). Underline write-ins are fillable
# text inputs styled as a baseline rule; checkboxes are native (matching page-1 convention).
def page6():
    """Orders -- imaging/testing checkbox groups + free-text write-ins."""
    def cb(name, label):
        return f'<label class="opt"><input type="checkbox" name="packet.doctor.orders.{name}">{label}</label>'
    def uin(name, w):                     # fixed-width inline write-in
        return f'<input type="text" class="uline uin" style="width:{w}" name="packet.doctor.orders.{name}">'
    def ta(name, h):                      # multi-line write-in (long entries wrap)
        return f'<textarea name="packet.doctor.orders.{name}" style="height:{h}"></textarea>'
    return (
        '<div class="ordtitle">ORDERS</div>'
        '<div class="ordsub">Dr. Yuri Falkinstein</div>'
        '<div class="ord">'
        f'<div class="orow">CASE TYPE: &nbsp; {cb("case_type.wc", "WC")} {cb("case_type.qme", "QME")} {cb("case_type.ame", "AME")}</div>'
        f'<div class="orow"><div class="oline"><span class="olbl" style="vertical-align:top">Body Part(s):</span>'
        f'<span class="ofill">{ta("body_parts", "42px")}</span></div></div>'
        '<div class="orow"><div class="oline">'
        f'<span class="imgcol" style="width:48%">{cb("mri_without", "MRI WITHOUT CONTRAST")}<br>'
        f'{cb("mri_with_without", "MRI WITH AND WITHOUT CONTRAST")}<br>{cb("mr_arthrogram", "MR ARTHROGRAM")}</span>'
        f'<span class="imgcol">{cb("ct_without", "CT WITHOUT CONTRAST")}<br>'
        f'{cb("ct_with_without", "CT WITH AND W/O CONTRAST")}<br>{cb("ct_arthrogram", "CT ARTHROGRAM")}</span>'
        '</div></div>'
        '<div class="orow"><div class="oline">'
        f'<span class="imgcol" style="width:48%">{cb("emg_ncv", "EMG/NCV:")} &nbsp; {cb("bue", "BUE")} {cb("ble", "BLE")}</span>'
        f'<span class="imgcol">{cb("fce", "FCE")} {uin("fce_detail", "2.4in")}</span>'
        '</div></div>'
        f'<div class="orow gap"><div class="oline"><span class="olbl" style="vertical-align:top">COMMENT:</span>'
        f'<span class="ofill">{ta("comment", "66px")}</span></div></div>'
        f'<div class="orow gap indent">{cb("claim_status", "CLAIM STATUS:")} {uin("claim_status_val", "1.3in")}'
        f' &nbsp;&nbsp;&nbsp; MA INITIALS: {uin("ma_initials", "0.9in")}</div>'
        '</div>')


# ----------------------------------------------------------------------------- Page 7 (DWC 10133.36 Return-to-Work)
# Dense California DWC form. Pre-fill tokens stay EXACT (display-only spans, no field). Per Adrian:
# Lift/Carry blanks are fillable inputs; R/L/Bilat hand selectors are circle-the-choice markers;
# the top-left seal is the existing DWC_Logo.jpg. "Yuri Falkinstein, M.D." is literal.
RTW = "packet.doctor.rtw"
_RTW_FREQ = [("1-2 hours", "h1_2"), ("2-4 hours", "h2_4"), ("4-6 hours", "h4_6"),
             ("6-8 hours", "h6_8"), ("None", "none")]
# (label, key) -- "Reach" appears twice in the original; the 2nd is keyed reach2 to stay unique.
RTW_ACTS = [("Stand", "stand"), ("Walk", "walk"), ("Sit", "sit"), ("Bend", "bend"), ("Squat", "squat"),
            ("Climb", "climb"), ("Twist", "twist"), ("Reach", "reach"), ("Crawl", "crawl"),
            ("Drive", "drive"), ("Reach", "reach2")]


def _rtw_info(twips, cells):
    """A 2-row employee/claims/employer info table: regular-weight labels over token cells."""
    head = "".join(f'<td>{lab}</td>' for lab, _ in cells)
    toks = "".join(f'<td class="tok-cell">{tk}</td>' for _, tk in cells)
    return f'<table class="info">{_colgroup(twips)}<tr>{head}</tr><tr>{toks}</tr></table>'


def page7():
    """Physician's Return-to-Work & Voucher Report (DWC AD 10133.36)."""
    def cb(name, label=""):
        return f'<label class="opt"><input type="checkbox" name="{RTW}.{name}">{label}</label>'
    def box_(name):                       # checkbox only (grid cell), no label/margin
        return f'<input type="checkbox" name="{RTW}.{name}">'
    def fill(name):
        return f'<input type="text" class="uline ufill" name="{RTW}.{name}">'
    def uin(name, w):
        return f'<input type="text" class="uline uin" style="width:{w}" name="{RTW}.{name}">'
    tok = lambda t: f'<span class="tok">{t}</span>'

    o = ['<div class="dwc">']
    # --- header: logo + centered titles ---
    o.append('<div class="dwc-hdr"><img class="dwc-logo" src="DWC_Logo.jpg">'
             '<div class="dwc-title">Physician&#39;s Return-to-Work &amp; Voucher Report</div>'
             '<div class="dwc-sub">For injuries occurring on or after January 1, 2013</div></div>')
    # --- P&S statement ---
    o.append(f'<div class="row">{cb("ps_permanent", "The Employee is P&amp;S from all conditions and the injury has caused permanent partial disability")}</div>')
    # --- info tables (T36-T39) ---
    # MI widened from First Name: the original (LibreOffice auto-layout) sizes MI to fit its
    # token; our fixed layout must do so explicitly or ##Patients.MiddleName## overflows.
    o.append(_rtw_info([3955, 2300, 2527, 2928], [
        ("Employee Last Name", tok("##Patients.LastName##")), ("Employee First Name", tok("##Patients.FirstName##")),
        ("MI", tok("##Patients.MiddleName##")), ("Date of Injury", tok("##InjuryDetails.DateOfInjury##"))]))
    o.append(_rtw_info([5855, 5855], [
        ("Claims Administrator:", tok("##InjuryDetails.PrimaryInsuranceName##")),
        ("Claims Representative", tok("##InjuryDetails.ClaimExaminerName##"))]))
    o.append(_rtw_info([5855, 5855], [
        ("Employer name:", tok("##EmployerDetails.EmployerName##")),
        ("Employer Street Address:", tok("##EmployerDetails.Street##"))]))
    o.append(_rtw_info([2591, 2712, 2526, 235, 3136], [
        ("Employer City:", tok("##EmployerDetails.City##")), ("State", tok("##EmployerDetails.State##")),
        ("ZipCode", tok("##EmployerDetails.Zip##")), ("", ""), ("Claim No:", tok("##InjuryDetails.ClaimNumber##"))]))
    # --- return to regular work ---
    o.append(f'<div class="row">{cb("regular_work", "The Employee can return to regular work")}</div>')
    # --- restrictions grid (activity x frequency) ---
    g = [f'<table class="grid">{_colgroup([40, 12, 12, 12, 12, 12])}']
    g.append(f'<tr><td class="act">{cb("work_with_restrictions", "The Employee can work with restrictions:")}</td>'
             + "".join(f'<td class="fh">{fl}</td>' for fl, _ in _RTW_FREQ) + '</tr>')
    for lab, key in RTW_ACTS:
        cells = "".join(f'<td class="bx">{box_("restrict." + key + "." + fk)}</td>' for _, fk in _RTW_FREQ)
        g.append(f'<tr><td class="act">{lab}</td>{cells}</tr>')
    for hl, hk in [("Grasp", "grasp"), ("Push/Pull", "pushpull")]:
        rl = (_cc(f"{RTW}.{hk}.hand.r", "R", 9) + "/" + _cc(f"{RTW}.{hk}.hand.l", "L", 9)
              + "/" + _cc(f"{RTW}.{hk}.hand.bilat", "Bilat", 24))
        cells = "".join(f'<td class="bx">{box_(hk + "." + fk)}</td>' for _, fk in _RTW_FREQ)
        g.append(f'<tr><td class="act">{rl} Hand(s) (circle): {hl}</td>{cells}</tr>')
    g.append('</table>')
    o.append("".join(g))
    # --- lift/carry (inline fillable blanks) ---
    o.append('<div class="row">Lift/Carry Restrictions: May not lift/carry at a height of '
             + uin("lift.height", "1.1in") + ' more than ' + uin("lift.lbs", "0.7in")
             + ' lbs. for more than ' + uin("lift.hours", "0.7in") + ' hours per day</div>')
    # --- other restrictions box (label left, large write-in area right) ---
    o.append('<table class="obox"><tr><td class="olbl">Other Restrictions</td>'
             f'<td><textarea name="{RTW}.other_restrictions" style="height:84px"></textarea></td></tr></table>')
    # --- job description section ---
    o.append('<div class="row it">If a job Description has been provided, please complete: Job Description provided of: '
             + cb("job_provided.regular", "Regular") + cb("job_provided.modified", "Modified")
             + cb("job_provided.alternative", "Alternative Work") + '</div>')
    o.append('<div class="row"><div class="oline">'
             f'<span class="olbl">Job Title:</span><span class="ofill">{fill("job_title")}</span>'
             f'<span class="olbl">Work Location:</span><span class="ofill">{fill("work_location")}</span></div></div>')
    o.append('<div class="row it">Are the Work Duties compatible with the activity restrictions set forth in the provided job description? '
             + cb("compatible.yes", "Yes") + cb("compatible.no", "No,") + ' Explain below</div>')
    o.append(f'<div class="box"><textarea name="{RTW}.explain" style="height:56px"></textarea></div>')
    # --- signature block ---
    o.append('<div class="row" style="margin-top:12px"><div class="oline">'
             '<span class="olbl">Physician&#39;s Name: Yuri Falkinstein, M.D.</span>'
             '<span class="ofill"></span>'
             f'<span class="olbl">Role of Doctor (PTP,QME,AME):</span><span class="ofill">{fill("role")}</span></div></div>')
    # Date label + token kept as one right-side cell so the signature fill takes the rest of the line.
    o.append('<div class="row" style="margin-top:12px"><div class="oline">'
             '<span class="olbl">Physician&#39;s Signature:</span>'
             f'<span class="ofill" style="width:55%">{fill("signature")}</span>'
             f'<span class="olbl">Date: {tok("##Appointments.AvailableDate##")}</span></div></div>')
    o.append('</div>')
    return "".join(o)


# ----------------------------------------------------------------------------- Page 8 (DWC 10133.36 Instructions)
# Pure prose -- the form's official instructions. No fields, no tokens, no logo. Only the top two
# header lines are bold (serif); the body question lead-ins are regular weight (matches the original).
def page8():
    """Physician's Return-to-Work & Voucher Report -- instructions page (no fields)."""
    paras = [
        "Who is responsible for filling out this form?  The first physician who finds that the disability from "
        "all conditions for which compensation is claimed has become permanent and stationary (or has reached "
        "maximum medical improvement) and finds that the injury has caused permanent partial disability. The "
        "physician can be the primary treating physician, a Qualified Medical Evaluator, or an Agreed Medical Evaluator.",
        "What is the purpose of this form?  The purpose of the form is to fully inform the employer of the work "
        "capacities and activity restrictions resulting from the injury that are relevant to potential regular "
        "work, modified work, or alternative work. The information contained on the form is for voucher purposes "
        "and is not considered in any permanent impairment rating or any permanent disability indemnity.",
        "Is this a mandatory form?  This is a mandatory attachment to the first medical report finding that the "
        "disability from all conditions for which compensation is claimed has become permanent and stationary and "
        "that the injury has caused permanent partial disability. This form should be attached to a comprehensive "
        "medical-legal evaluation and does not replace such comprehensive medical-legal evaluations.",
        "When does the form need to be completed?  This form does not need to be completed until all conditions "
        "for which compensation is claimed have become permanent and stationary.",
        "If the employer or claims administrator has provided the physician with a job description providing "
        "physical requirements of the employee's regular work, proposed modified work, or proposed alternative "
        "work, the physician shall evaluate and describe in the form whether the work capacities and activity "
        "restrictions are compatible with the physical requirements set forth in that job description. The bottom "
        "portion of the form does not need to be completed if the physician has not been provided with a job description.",
        "Completing the employee's work restrictions:  The physician should indicate work restrictions in terms of "
        "how many hours a particular activity can be performed during an 8- hour work day. For hand restrictions, "
        "the physician should indicate whether the restrictions are for the right hand, left hand, or both.",
        "Other Restrictions can include psychiatric restrictions, chemical exposure, use of equipment, or any "
        "other restrictions. The space can also be used to further clarify or explain any of the checked restrictions.",
        "How does the employer receive the form?  The claims administrator shall forward the form to the employer.",
    ]
    body = "".join(f"<p>{p}</p>" for p in paras)
    return (
        '<div class="ins">'
        '<div class="ins-h1">State of California, Division of Workers&#39; Compensation</div>'
        '<div class="ins-h1">Retraining and Return to Work Unit</div>'
        '<div class="ins-title">Physician&#39;s Return-to-Work &amp; Voucher Report Instructions</div>'
        '<div class="ins-sub">For injuries on or after January 1, 2013  DWC - AD 10133.36</div>'
        + body
        + '<div class="ins-foot">DWC AD Form 10133.36 (Effective 1/13)</div>'
        '</div>')


# ----------------------------------------------------------------------------- assemble
PAGES = [page1, page2, page3, page4, page5, page6, page7, page8]

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


def build():
    body = "\n".join(f'<div class="page">\n{fn()}\n</div>' for fn in PAGES)
    html = ('<!DOCTYPE html>\n<html lang="en"><head><meta charset="utf-8">'
            '<title>Doctor Packet</title>\n<style>' + CSS + '</style></head>\n<body>\n'
            + body + '\n</body></html>')
    html = _inline_images(html)
    with open("doctor.html", "w", encoding="utf-8") as f:
        f.write(html)
    print(f"wrote doctor.html ({len(PAGES)} page(s))")

if __name__ == "__main__":
    build()
