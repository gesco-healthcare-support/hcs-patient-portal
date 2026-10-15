"""Coverage for tools/packet-templates/shared/check_fields.py.

WHAT IT IS. A diagnostic that opens a generated packet and reports what the form layer
actually contains: page count, whether an AcroForm survived, how many fields there are,
the spread of field types, and the size of the first widget rectangles in points. It is
how someone answers "did the checkboxes come out the right size?" without opening a PDF
viewer, and it ships inside the packet-renderer image alongside the generators.

WHY IT READS 21/21 UNCOVERED, and why that is awkward to fix. It is a SCRIPT, not a
module: there are no functions, no `if __name__ == "__main__"` guard, and the reader is
constructed at module level against a hardcoded `/work/page1.pdf`. So there is nothing to
call -- the only way to execute its 21 statements is to import it, and importing it needs
`pypdf`, which `ci.yml`'s Python job does not install.

HOW THIS RUNS IT. `fake_pdf_libs.install_pypdf` puts a fake `pypdf` in `sys.modules`
primed with the reader the script will receive, and `packet_loader.load_module(fresh=True)`
re-executes the script body per fixture -- which is the only way a script with no seam can
be observed under more than one input. Its stdout is captured and asserted, because for a
diagnostic the printed report IS the behaviour.

NOT A REFACTOR. Wrapping the script in a `main()` would make it trivially testable and is
the better long-term shape, but it changes a file that ships in the renderer image, and
this change is about measuring what is there. Logged rather than done here.

All fixture data is synthetic: field names are packet form controls, and no PDF is read
from disk.
"""

from __future__ import annotations

import contextlib
import io
import unittest

import fake_pdf_libs as fake
import packet_loader


def run_script(reader: fake.FakeReader) -> str:
    """Execute check_fields.py against one fixture reader and return what it printed."""
    fake.install_pypdf(reader)
    buffer = io.StringIO()
    try:
        with contextlib.redirect_stdout(buffer):
            packet_loader.load_module("check_fields", packet_loader.CHECK_FIELDS, fresh=True)
    finally:
        fake.uninstall("pypdf", "check_fields")
    return buffer.getvalue()


def annot(name=None, rect=None, ft="/Btn"):
    entries: dict[str, object] = {"FT": ft}
    if name is not None:
        entries["T"] = name
    if rect is not None:
        entries["Rect"] = list(rect)
    return fake.Annotation(**entries)


def page(annots=None):
    return fake.ReaderPage() if annots is None else fake.ReaderPage({"/Annots": annots})


class CheckFieldsReportTest(unittest.TestCase):
    def test_it_reports_the_page_count(self):
        output = run_script(fake.FakeReader(pages=[page(), page(), page()]))
        self.assertIn("pages: 3", output)

    def test_it_reports_a_present_acroform(self):
        output = run_script(fake.FakeReader(pages=[page()], root={"/AcroForm": {}}))
        self.assertIn("AcroForm present: True", output)

    def test_it_reports_a_missing_acroform(self):
        """The interesting failure this diagnostic exists to catch: WeasyPrint produced a
        document whose form layer did not survive, so every field is gone."""
        output = run_script(fake.FakeReader(pages=[page()], root={}))
        self.assertIn("AcroForm present: False", output)

    def test_it_reports_the_field_count(self):
        reader = fake.FakeReader(
            pages=[page()],
            fields={"a": {"/FT": "/Btn"}, "b": {"/FT": "/Tx"}},
        )
        self.assertIn("field count: 2", run_script(reader))

    def test_a_document_with_no_fields_reports_zero_rather_than_raising(self):
        """`r.get_fields() or {}` -- pypdf returns None, not an empty dict, for a PDF with
        no form. Without the `or {}` this line would raise TypeError on len(None), which
        is precisely the flat-notice case the diagnostic is pointed at."""
        output = run_script(fake.FakeReader(pages=[page()], fields=None))
        self.assertIn("field count: 0", output)
        self.assertIn("by type: {}", output)

    def test_it_tallies_the_field_types(self):
        reader = fake.FakeReader(
            pages=[page()],
            fields={
                "one": {"/FT": "/Btn"},
                "two": {"/FT": "/Btn"},
                "three": {"/FT": "/Tx"},
            },
        )
        output = run_script(reader)
        self.assertIn("'/Btn': 2", output)
        self.assertIn("'/Tx': 1", output)


class CheckFieldsWidgetTableTest(unittest.TestCase):
    def test_it_prints_the_widget_dimensions_in_points(self):
        """The whole reason the script exists: a checkbox that came out 2pt wide is
        invisible on paper, and this table is how that gets noticed."""
        reader = fake.FakeReader(
            pages=[page([annot("packet.doctor.obs.limps", (10.0, 20.0, 22.5, 31.0))])]
        )
        output = run_script(reader)
        self.assertIn("packet.doctor.obs.limps", output)
        self.assertIn("w=  12.5", output)
        self.assertIn("h= 11.0", output)

    def test_it_reports_the_field_type_alongside_each_widget(self):
        reader = fake.FakeReader(
            pages=[page([annot("packet.doctor.name", (0.0, 0.0, 100.0, 12.0), ft="/Tx")])]
        )
        self.assertIn("/Tx", run_script(reader))

    def test_an_annotation_with_no_rectangle_is_skipped(self):
        """Not every annotation is a widget. Printing one without a /Rect would raise on
        the unpack, so the guard is what keeps the diagnostic usable on a real document."""
        reader = fake.FakeReader(pages=[page([annot("packet.doctor.x", rect=None)])])
        output = run_script(reader)
        self.assertIn("widget rectangles", output)
        self.assertNotIn("packet.doctor.x:", output)

    def test_an_annotation_with_no_name_is_skipped(self):
        reader = fake.FakeReader(pages=[page([annot(None, (0.0, 0.0, 10.0, 10.0))])])
        output = run_script(reader)
        self.assertNotIn("w=", output)

    def test_a_page_with_no_annotations_is_skipped(self):
        # `page.get("/Annots") or []` -- a page with no annotations at all.
        output = run_script(fake.FakeReader(pages=[page(), page()]))
        self.assertIn("widget rectangles", output)
        self.assertNotIn("w=", output)

    def test_the_listing_stops_after_twelve_widgets(self):
        """The `shown < 12` cap, which needs MORE than twelve widgets to be observable.

        A doctor packet carries hundreds; without the cap this diagnostic would bury its
        own summary lines under pages of output. Seeded with 14 so the cap is exercised
        rather than merely present.
        """
        widgets = [annot(f"packet.doctor.f{i}", (0.0, 0.0, float(i + 1), 10.0)) for i in range(14)]
        output = run_script(fake.FakeReader(pages=[page(widgets)]))
        self.assertEqual(output.count("w="), 12)
        self.assertIn("packet.doctor.f11", output)
        self.assertNotIn("packet.doctor.f12", output)
        self.assertNotIn("packet.doctor.f13", output)

    def test_the_cap_counts_across_pages_rather_than_per_page(self):
        # `shown` is initialised once, outside the page loop. A per-page counter would
        # print 20 rows here.
        first = [annot(f"packet.doctor.a{i}", (0.0, 0.0, 10.0, 10.0)) for i in range(10)]
        second = [annot(f"packet.doctor.b{i}", (0.0, 0.0, 10.0, 10.0)) for i in range(10)]
        output = run_script(fake.FakeReader(pages=[page(first), page(second)]))
        self.assertEqual(output.count("w="), 12)


class CheckFieldsTargetTest(unittest.TestCase):
    def test_it_reads_the_renderer_working_path(self):
        """The hardcoded target. It is `/work/page1.pdf` because that is where the
        packet-renderer image mounts the document being generated; a change here would
        make the diagnostic silently inspect nothing."""
        run_script(fake.FakeReader(pages=[page()]))
        self.assertEqual(fake._PdfReaderFactory.opened, ["/work/page1.pdf"])


if __name__ == "__main__":
    unittest.main()
