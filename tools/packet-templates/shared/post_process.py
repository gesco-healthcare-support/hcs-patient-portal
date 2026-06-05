"""Finalize the WeasyPrint fillable PDF:
   1. clear NoToggleToOff on radio groups (so a selected radio can be cleared)
   2. replace checkbox/radio appearance streams with bold, print-friendly marks
Then emit page1_demo.pdf with a few selections set, to visually verify the marks.
All open-source (pikepdf / qpdf).
"""
import re
import pikepdf
from pikepdf import Pdf, Name, Dictionary, Array

RADIO = 1 << 15        # 32768  (Radio flag)
NOTOGGLE = 1 << 14     # 16384  (NoToggleToOff)
PUSHBUTTON = 1 << 16   # 65536  (Pushbutton)


def make_xobj(pdf, w, h, content):
    s = pikepdf.Stream(pdf, content.encode())
    s[Name.Type] = Name.XObject
    s[Name.Subtype] = Name.Form
    s[Name.BBox] = Array([0, 0, round(w, 2), round(h, 2)])
    s[Name.Resources] = Dictionary()
    return s


def circle(cx, cy, r):
    k = 0.5523 * r
    return (f"{cx + r:.2f} {cy:.2f} m "
            f"{cx + r:.2f} {cy + k:.2f} {cx + k:.2f} {cy + r:.2f} {cx:.2f} {cy + r:.2f} c "
            f"{cx - k:.2f} {cy + r:.2f} {cx - r:.2f} {cy + k:.2f} {cx - r:.2f} {cy:.2f} c "
            f"{cx - r:.2f} {cy - k:.2f} {cx - k:.2f} {cy - r:.2f} {cx:.2f} {cy - r:.2f} c "
            f"{cx + k:.2f} {cy - r:.2f} {cx + r:.2f} {cy - k:.2f} {cx + r:.2f} {cy:.2f} c")


# The box/circle OUTLINE is WeasyPrint's baked page content; our appearance only adds
# the MARK on the "on" state. Drawing our own outline too nests a second box/circle
# inside it (the bug). Off is an invisible no-op so viewers don't draw a default mark.
_BLANK = "q 1 1 1 rg 0 0 0.01 0.01 re f Q"


def cb_off(w, h):
    return _BLANK


def cb_on(w, h):
    # filled square mark, centered inside the baked checkbox outline
    return f"0 0 0 rg {0.27 * w:.2f} {0.27 * h:.2f} {0.46 * w:.2f} {0.46 * h:.2f} re f"


def rb_off(w, h):
    return _BLANK


def rb_on(w, h):
    # filled dot, centered inside the baked radio circle
    cx, cy, r = w / 2, h / 2, min(w, h) / 2 - 1.2
    return "0 0 0 rg " + circle(cx, cy, r * 0.55) + " f"


# --- circle-the-choice highlight (page 2 spinal markers) --------------------
# A translucent colored ellipse drawn over the glyph (the glyph itself is
# WeasyPrint page text, so it shows through). Default = soft yellow.
HL_RGB = "1 0.85 0.10"


def ellipse_path(w, h, pad=1.3):
    cx, cy = w / 2, h / 2
    rx = max(w / 2 - pad, w / 2 * 0.85)
    ry = max(h / 2 - pad, h / 2 * 0.85)
    ox, oy = 0.5523 * rx, 0.5523 * ry
    return (f"{cx + rx:.2f} {cy:.2f} m "
            f"{cx + rx:.2f} {cy + oy:.2f} {cx + ox:.2f} {cy + ry:.2f} {cx:.2f} {cy + ry:.2f} c "
            f"{cx - ox:.2f} {cy + ry:.2f} {cx - rx:.2f} {cy + oy:.2f} {cx - rx:.2f} {cy:.2f} c "
            f"{cx - rx:.2f} {cy - oy:.2f} {cx - ox:.2f} {cy - ry:.2f} {cx:.2f} {cy - ry:.2f} c "
            f"{cx + ox:.2f} {cy - ry:.2f} {cx + rx:.2f} {cy - oy:.2f} {cx + rx:.2f} {cy:.2f} c")


def make_xobj_alpha(pdf, w, h, content):
    """Form XObject with a 0.40-alpha ExtGState (GS1) so fills are translucent."""
    gs = pdf.make_indirect(Dictionary(Type=Name.ExtGState, ca=0.40, CA=1.0))
    s = pikepdf.Stream(pdf, content.encode())
    s[Name.Type] = Name.XObject
    s[Name.Subtype] = Name.Form
    s[Name.BBox] = Array([0, 0, round(w, 2), round(h, 2)])
    s[Name.Resources] = Dictionary(ExtGState=Dictionary(GS1=gs))
    return s


def set_hl_ap(pdf, widget):
    """Off = nothing; On = translucent colored ellipse. Suppress the field border."""
    rect = [float(x) for x in widget.Rect]
    w, h = abs(rect[2] - rect[0]), abs(rect[3] - rect[1])
    on = onstate_of(widget)
    n = Dictionary()
    # Off is intentionally blank, but NOT an empty stream: some viewers (poppler)
    # fall back to drawing default checkbox/radio chrome when the Off appearance is
    # empty. A no-op invisible mark keeps it blank while suppressing that fallback.
    n[Name("/Off")] = make_xobj(pdf, w, h, "q 1 1 1 rg 0 0 0.01 0.01 re f Q")
    n[Name(on)] = make_xobj_alpha(pdf, w, h, f"q /GS1 gs {HL_RGB} rg " + ellipse_path(w, h) + " f Q")
    widget[Name.AP] = Dictionary(N=n)
    widget[Name.BS] = Dictionary(W=0, S=Name("/S"))   # no default border box
    if Name.MK in widget:
        del widget[Name.MK]


def onstate_of(widget):
    ap = widget.get("/AP")
    if ap is not None and "/N" in ap:
        for key in ap["/N"].keys():
            if str(key) != "/Off":
                return str(key)
    return "/Yes"


def set_ap(pdf, widget, is_radio):
    rect = [float(x) for x in widget.Rect]
    w, h = abs(rect[2] - rect[0]), abs(rect[3] - rect[1])
    on = onstate_of(widget)
    off_x = make_xobj(pdf, w, h, rb_off(w, h) if is_radio else cb_off(w, h))
    on_x = make_xobj(pdf, w, h, rb_on(w, h) if is_radio else cb_on(w, h))
    n = Dictionary()
    n[Name("/Off")] = off_x
    n[Name(on)] = on_x
    widget[Name.AP] = Dictionary(N=n)
    # suppress WeasyPrint's own widget border (/MK + /BS) so only our /AP box shows;
    # otherwise the two borders render as a small box nested inside the checkbox.
    widget[Name.BS] = Dictionary(W=0, S=Name("/S"))
    if Name.MK in widget:
        del widget[Name.MK]


def is_highlight(name):
    """Page-2 spinal circle-the-choice markers (not the J-Tech checkboxes)."""
    return name.startswith("packet.doctor.spinal.") and "jtech" not in name


def _ensure_acroform(pdf):
    """Return the AcroForm, creating an empty one if the PDF has none (flat notices with no
    form fields, e.g. the AttorneyCE packets). Keeps fix()/harvest robust for any PDF."""
    root = pdf.Root
    if Name("/AcroForm") not in root:
        root.AcroForm = pdf.make_indirect(Dictionary(Fields=Array()))
    acro = root.AcroForm
    if Name("/Fields") not in acro:
        acro.Fields = Array()
    return acro


def fix(path):
    pdf = Pdf.open(path, allow_overwriting_input=True)
    # Honor our custom /AP appearances instead of letting the viewer regenerate
    # them (WeasyPrint defaults this to True, which hides circle-the-choice).
    acro = _ensure_acroform(pdf)
    acro.NeedAppearances = False
    radios = checkboxes = highlights = 0
    radio_kids = set()        # objgens of radio kid widgets (to de-list from top-level Fields)
    for f in acro.Fields:
        if f.get("/FT") != Name("/Btn"):
            continue
        ff = int(f.get("/Ff", 0))
        if ff & PUSHBUTTON:
            continue
        hl = bool(f.get(Name("/CcChoice"), False))   # circle-the-choice fields tagged at harvest
        if ff & RADIO:
            if ff & NOTOGGLE:
                f[Name.Ff] = ff & ~NOTOGGLE          # allow deselect
            for k in f.get("/Kids", []):
                # WeasyPrint mis-tags each radio kid with its own /T (= the group name),
                # yielding a malformed 'group.group' fully-qualified name and pypdf
                # 'already parsed' warnings. Kids are widgets, not sub-fields -- strip /T.
                if Name.T in k:
                    del k[Name.T]
                if k.is_indirect:
                    radio_kids.add(k.objgen)
                set_hl_ap(pdf, k) if hl else set_ap(pdf, k, True)
            highlights += 1 if hl else 0
            radios += 0 if hl else 1
        else:
            for w in (list(f.Kids) if "/Kids" in f else [f]):
                set_hl_ap(pdf, w) if hl else set_ap(pdf, w, False)
            highlights += 1 if hl else 0
            checkboxes += 0 if hl else 1
    # WeasyPrint lists radio kids BOTH under the parent /Kids and again as top-level
    # AcroForm.Fields. Drop the redundant top-level copies so Fields has no nameless
    # orphan widgets (the parent still owns them via /Kids).
    if radio_kids:
        kept = [f for f in acro.Fields
                if not (f.is_indirect and f.objgen in radio_kids)]
        acro.Fields = Array(kept)
    # Auto-size single-line text fields: zero the /DA font size ("... /Font N Tf" -> "... 0 Tf")
    # so a long entry shrinks to fit the field instead of clipping. Multiline fields keep their
    # fixed size (their text wraps instead).
    autosized = 0
    for f in acro.Fields:
        if f.get(Name("/FT")) != Name("/Tx"):
            continue
        if int(f.get(Name("/Ff"), 0)) & (1 << 12):    # multiline -> wrap at fixed size
            continue
        da = f.get(Name("/DA"))
        if da is None:
            continue
        new = re.sub(r"[\d.]+(\s+Tf\b)", r"0\1", str(da), count=1)
        if new != str(da):
            f[Name("/DA")] = pikepdf.String(new)
            autosized += 1
    print(f"radios={radios} checkboxes={checkboxes} highlights={highlights} autosized={autosized}")
    pdf.save(path)


def activate(f):
    """Turn a button field ON (checkbox -> checked; radio -> first option)."""
    if "/Kids" in f:
        first = True
        for k in f.Kids:
            on = onstate_of(k)
            if first:
                k[Name.AS] = Name(on); f[Name.V] = Name(on); first = False
            else:
                k[Name.AS] = Name("/Off")
    else:
        on = onstate_of(f)
        f[Name.V] = Name(on); f[Name.AS] = Name(on)


def demo(src, dst, names):
    pdf = Pdf.open(src)
    targets = set(names)
    for f in pdf.Root.AcroForm.Fields:
        if str(f.get("/T", "")) in targets:
            activate(f)
    pdf.save(dst)


def harvest_circle_fields(path):
    """Convert WeasyPrint 'cc:' link annotations into highlight checkbox widgets.

    The circle-the-choice glyphs are authored as plain <a href="cc:NAME"> anchors
    (no form input, so WeasyPrint bakes no control box). Each anchor becomes a Link
    annotation with the glyph's exact rectangle; we replace it with a Btn widget so
    fix() can attach the blank-off / yellow-highlight appearance.
    """
    pdf = Pdf.open(path, allow_overwriting_input=True)
    fields = _ensure_acroform(pdf).Fields
    added = 0
    for page in pdf.pages:
        pg = page.obj
        annots = pg.get("/Annots", None)
        if annots is None:
            continue
        keep = []
        for a in annots:
            act = a.get("/A")
            uri = act.get("/URI") if act is not None else None
            if a.get("/Subtype") == Name("/Link") and uri is not None and str(uri).startswith("cc:"):
                name = str(uri)[3:]
                r = [float(x) for x in a.Rect]
                x1, x2 = sorted((r[0], r[2]))
                y1, y2 = sorted((r[1], r[3]))
                widget = pdf.make_indirect(Dictionary(
                    Type=Name.Annot, Subtype=Name.Widget, FT=Name.Btn, T=name, F=4,
                    Rect=Array([round(x1, 2), round(y1, 2), round(x2, 2), round(y2, 2)]),
                    AS=Name("/Off"), P=pg, CcChoice=True))   # tag: circle-the-choice highlight
                keep.append(widget)
                fields.append(widget)
                added += 1
            else:
                keep.append(a)
        pg[Name.Annots] = Array(keep)
    pdf.save(path)
    print(f"circle fields harvested: {added}")
    return added


def finalize(path):
    """Renderer entry point: harvest circle-the-choice anchors, then normalize the form
    (radio cleanup, checkbox/highlight appearances, single-line auto-size) in place. Safe on
    flat PDFs with no form fields (e.g. the AttorneyCE notices) -- they pass through unchanged."""
    harvest_circle_fields(path)
    fix(path)


if __name__ == "__main__":
    import sys
    _target = sys.argv[1] if len(sys.argv) > 1 else "/work/page1.pdf"
    finalize(_target)
    if len(sys.argv) > 2 and sys.argv[2] == "demo":
        demo(_target, _target.replace(".pdf", "_demo.pdf"), [
            "packet.doctor.dynamometer.jtech_report",
            "packet.doctor.obs.limps",
            "packet.doctor.obs.altered_gait",
            "packet.doctor.obs.limps_side",
            "packet.doctor.obs.device",
        ])
    print("done")
