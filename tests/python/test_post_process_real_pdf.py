"""post_process.py run against the REAL pikepdf and pypdf, in a clean interpreter (#938).

WHY THIS EXISTS. `test_post_process.py` measures the module against `fake_pdf_libs`, a
hand-written stand-in. That number says the module calls the fake in the expected shape; it
cannot say the module works against the library that ships in the packet-renderer image
(pikepdf 10.7.2, `docker/packet-renderer/requirements.in`). This file closes that gap: it
builds a small real PDF, runs `finalize` over it, and reads the result back with a second
independent library.

WHY A SUBPROCESS. Other test modules put stubs for `pikepdf` into `sys.modules`; a fresh
interpreter is the only way to be sure the import here resolves to the installed package.

WHAT HAPPENS WITHOUT THE LIBRARIES. The default Python job installs neither, so these tests
SKIP -- and a skip is green. That is exactly how a check ends up reporting success having
checked nothing, so the dedicated workflow (`.github/workflows/packet-real-pdf.yml`) sets
REQUIRE_REAL_PDF_LIBS=1, under which a missing library is a FAILURE, not a skip.

COVERAGE. None of this is counted: it runs in a subprocess and the Python coverage job does
not install the libraries. The coverage figure for post_process.py therefore still describes
the fake, and this file is the honest answer to what that figure cannot say.

Synthetic field names only; no patient data.
"""

from __future__ import annotations

import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SHARED = REPO_ROOT / "tools" / "packet-templates" / "shared"

REQUIRED = os.environ.get("REQUIRE_REAL_PDF_LIBS") == "1"
_HAVE = all(importlib.util.find_spec(m) is not None for m in ("pikepdf", "pypdf"))

# Runs in the child. Builds the fixture, finalizes it, and prints a JSON report read back
# through pypdf (a different library from the one that wrote it).
_CHILD = r'''
import json, sys
sys.path.insert(0, sys.argv[2])
import pikepdf
from pikepdf import Pdf, Name, Dictionary, Array, String
import post_process

path = sys.argv[1]
pdf = Pdf.new()
pdf.add_blank_page(page_size=(612, 792))
page = pdf.pages[0].obj

def widget(name, rect, **kw):
    d = Dictionary(Type=Name.Annot, Subtype=Name.Widget, F=4, Rect=Array(rect), **kw)
    return pdf.make_indirect(d)

def ap_on(state):
    st = pikepdf.Stream(pdf, b"q Q")
    st[Name.Type] = Name.XObject; st[Name.Subtype] = Name.Form
    st[Name.BBox] = Array([0, 0, 10, 10])
    return Dictionary(N=Dictionary({"/" + state: st}))

check = widget("c", [10, 700, 20, 710], FT=Name.Btn, T="synthetic.check", Ff=0,
               AS=Name("/Off"), AP=ap_on("Yes"), MK=Dictionary(BC=Array([0])))
kid1 = widget("k1", [10, 650, 20, 660], T="synthetic.radio", Parent=None, AS=Name("/Off"), AP=ap_on("A"))
kid2 = widget("k2", [30, 650, 40, 660], T="synthetic.radio", AS=Name("/Off"), AP=ap_on("B"))
radio = pdf.make_indirect(Dictionary(FT=Name.Btn, T="synthetic.radio", Ff=(1 << 15) | (1 << 14),
                                     Kids=Array([kid1, kid2])))
kid1.Parent = radio; kid2.Parent = radio
text = widget("t", [10, 600, 200, 620], FT=Name.Tx, T="synthetic.text", Ff=0,
              DA=String("0 g /Helv 12 Tf"))
link = pdf.make_indirect(Dictionary(Type=Name.Annot, Subtype=Name.Link, Rect=Array([50, 500, 70, 520]),
                                    A=Dictionary(S=Name.URI, URI=String("cc:packet.doctor.spinal.synthetic"))))
page.Annots = Array([check, kid1, kid2, text, link])
pdf.Root.AcroForm = pdf.make_indirect(Dictionary(Fields=Array([check, radio, kid1, kid2, text]),
                                                 NeedAppearances=True))
pdf.save(path)

post_process.finalize(path)

from pypdf import PdfReader
r = PdfReader(path)
fields = r.get_fields() or {}
acro = r.trailer["/Root"]["/AcroForm"]
top = [f.get_object() for f in acro["/Fields"]]
rd = next(f for f in top if f.get("/T") == "synthetic.radio")
tx = next(f for f in top if f.get("/T") == "synthetic.text")
ck = next(f for f in top if f.get("/T") == "synthetic.check")
hl = [f for f in top if f.get("/T") == "packet.doctor.spinal.synthetic"]
kids = [k.get_object() for k in rd["/Kids"]]
print(json.dumps({
    "need_appearances": bool(acro["/NeedAppearances"].value),
    "radio_ff": int(rd["/Ff"]),
    "kid_has_T": [("/T" in k) for k in kids],
    "top_level_count": len(top),
    "top_level_names": sorted(str(f.get("/T")) for f in top if "/T" in f),
    "check_ap_keys": sorted(str(k) for k in ck["/AP"]["/N"].keys()),
    "check_has_mk": "/MK" in ck,
    "kid_ap_keys": [sorted(str(x) for x in k["/AP"]["/N"].keys()) for k in kids],
    "text_da": str(tx["/DA"]),
    "highlight_count": len(hl),
    "highlight_ccchoice": bool(hl[0].get("/CcChoice")) if hl else None,
    "highlight_ap_keys": sorted(str(k) for k in hl[0]["/AP"]["/N"].keys()) if hl else None,
    "link_left": sum(1 for a in r.pages[0]["/Annots"] if a.get_object().get("/Subtype") == "/Link"),
    "pypdf_field_count": len(fields),
}))
'''


@unittest.skipUnless(_HAVE or REQUIRED, "real pikepdf and pypdf not installed; see module docstring")
class FinalizeAgainstRealPikepdf(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not _HAVE:
            raise AssertionError(
                "REQUIRE_REAL_PDF_LIBS=1 but pikepdf/pypdf are not importable: this job "
                "exists to run against the real libraries, so a skip would be a false green."
            )
        with tempfile.TemporaryDirectory() as td:
            pdf_path = str(Path(td) / "fixture.pdf")
            proc = subprocess.run(
                [sys.executable, "-c", _CHILD, pdf_path, str(SHARED)],
                capture_output=True, text=True, timeout=120,
            )
        if proc.returncode != 0:
            raise AssertionError(
                f"finalize() failed against the real libraries:\n{proc.stderr}\n{proc.stdout}"
            )
        cls.report = json.loads(proc.stdout.strip().splitlines()[-1])

    def test_need_appearances_is_cleared(self):
        self.assertFalse(self.report["need_appearances"])

    def test_radio_group_can_be_deselected(self):
        self.assertEqual(self.report["radio_ff"] & (1 << 14), 0)
        self.assertNotEqual(self.report["radio_ff"] & (1 << 15), 0, "still a radio")

    def test_radio_kids_lose_their_own_name_and_the_top_level_copy(self):
        self.assertEqual(self.report["kid_has_T"], [False, False])
        self.assertEqual(self.report["top_level_names"].count("synthetic.radio"), 1)
        # check + radio parent + text + harvested highlight; the two radio kids are de-listed.
        self.assertEqual(self.report["top_level_count"], 4)

    def test_appearance_streams_are_replaced_with_off_and_on_states(self):
        self.assertEqual(self.report["check_ap_keys"], ["/Off", "/Yes"])
        self.assertEqual(self.report["kid_ap_keys"], [["/A", "/Off"], ["/B", "/Off"]])
        self.assertFalse(self.report["check_has_mk"])

    def test_single_line_text_font_size_is_zeroed(self):
        self.assertIn("0 Tf", self.report["text_da"])
        self.assertNotIn("12 Tf", self.report["text_da"])

    def test_circle_anchor_becomes_a_highlight_widget(self):
        self.assertEqual(self.report["highlight_count"], 1)
        self.assertTrue(self.report["highlight_ccchoice"])
        self.assertEqual(self.report["highlight_ap_keys"], ["/Off", "/Yes"])
        self.assertEqual(self.report["link_left"], 0)

    def test_an_independent_reader_still_parses_the_form(self):
        self.assertGreaterEqual(self.report["pypdf_field_count"], 4)


if __name__ == "__main__":
    unittest.main()
