"""Coverage for tools/packet-templates/shared/post_process.py.

WHAT THIS MODULE DOES, and why it is worth testing. WeasyPrint bakes the packet's
checkbox and radio outlines into the page content, then adds its own form widgets on top.
`post_process` repairs the result: it clears NoToggleToOff so a selected radio can be
cleared, replaces every appearance stream with a print-friendly mark, converts the
circle-the-choice anchors into highlight widgets, de-lists duplicated radio kids, and
zeroes the font size on single-line text fields so a long entry shrinks instead of
clipping. It runs on every generated packet -- `finalize` is the renderer's entry point --
and the document it produces bills parties and can reach opposing counsel and the WCAB.

WHY IT READS 155/183 UNCOVERED. Importing it covers the 28 module-level statements, which
`test_packet_renderer_paths.py` already does incidentally ("post_process is imported but
never invoked"). Nothing invokes it, because `pikepdf` is not installed on the runner --
`ci.yml`'s Python job installs `coverage` and nothing else. `fake_pdf_libs` supplies the
object layer instead, following the `sys.modules` stubbing this repo already uses at
test_packet_renderer_paths.py:52.

WHAT IS ASSERTED. The arithmetic (appearance geometry), the branch choices (which painter
a widget gets, which fields are skipped), and the MUTATIONS -- flags cleared, keys
deleted, arrays rewritten. Several of those are removals, so their fixtures deliberately
seed the thing being removed; a fixture without it would pass with the removal deleted.

No patient data: these are blank form widgets with synthetic field names.
"""

from __future__ import annotations

import contextlib
import io
import unittest

import fake_pdf_libs as fake
import packet_loader

Name = fake.Name
Dictionary = fake.Dictionary
Array = fake.Array


def setUpModule():
    """Install the fake pikepdf before the module under test imports it."""
    fake.install_pikepdf()
    global post_process
    post_process = packet_loader.load_module("post_process", packet_loader.POST_PROCESS)


def tearDownModule():
    fake.uninstall("pikepdf")
    fake.Pdf.reset_registry()


def make_widget(rect=(0.0, 0.0, 20.0, 20.0), on: str | None = "/Yes", **entries):
    """A widget annotation with an existing /AP so `onstate_of` has a state to find."""
    widget = Dictionary(Rect=Array([float(v) for v in rect]), **entries)
    if on is not None:
        appearances = Dictionary()
        appearances[Name("/Off")] = Dictionary()
        appearances[Name(on)] = Dictionary()
        widget[Name.AP] = Dictionary(N=appearances)
    return widget


def make_pdf(fields=None, pages=None, with_acroform=True):
    pdf = fake.Pdf()
    if with_acroform:
        acro = Dictionary(Fields=Array(fields or []))
        pdf.Root.AcroForm = acro
    if pages:
        pdf.pages = pages
    return pdf


def run_quietly(callable_, *args, **kwargs):
    """Call something that prints a summary, returning (result, printed text)."""
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer):
        result = callable_(*args, **kwargs)
    return result, buffer.getvalue()


class GeometryTest(unittest.TestCase):
    """The appearance painters. Pure arithmetic producing PDF content-stream operators."""

    def test_circle_starts_at_the_rightmost_point(self):
        path = post_process.circle(5, 5, 7)
        self.assertTrue(path.startswith("12.00 5.00 m "))

    def test_circle_is_drawn_with_four_bezier_segments(self):
        # A circle approximated with fewer than four cubic segments visibly flattens.
        self.assertEqual(post_process.circle(5, 5, 7).count(" c"), 4)

    def test_checkbox_on_paints_a_centred_filled_square(self):
        self.assertEqual(
            post_process.cb_on(20, 20),
            "0 0 0 rg 5.40 5.40 9.20 9.20 re f",
        )

    def test_checkbox_mark_stays_inside_the_baked_outline(self):
        """The outline is WeasyPrint's page content; the mark must sit within it.

        0.27 inset with a 0.46 extent leaves 0.27 clear on the far side, so the mark can
        never overdraw the box the page already painted -- which is the bug the module's
        own comment describes (a second box nested inside the first).
        """
        width = height = 40.0
        mark = post_process.cb_on(width, height).split()
        x, y, w, h = (float(v) for v in mark[4:8])
        self.assertGreater(x, 0)
        self.assertGreater(y, 0)
        self.assertLess(x + w, width)
        self.assertLess(y + h, height)

    def test_radio_on_paints_a_filled_dot(self):
        painted = post_process.rb_on(20, 20)
        self.assertTrue(painted.startswith("0 0 0 rg "))
        self.assertTrue(painted.rstrip().endswith(" f"))
        self.assertEqual(painted.count(" c"), 4)

    def test_radio_dot_is_inset_from_the_baked_circle(self):
        # r = min(w,h)/2 - 1.2, then 0.55 of that. A dot as large as the circle would
        # cover the outline rather than sit inside it.
        painted = post_process.rb_on(20, 20)
        first_x = float(painted.split()[4])
        self.assertLess(first_x, 20.0)
        self.assertGreater(first_x, 10.0)

    def test_both_off_painters_share_the_same_blank(self):
        self.assertEqual(post_process.cb_off(20, 20), post_process.rb_off(20, 20))

    def test_the_off_state_is_a_no_op_mark_and_not_an_empty_stream(self):
        """Load-bearing, and the module says why: poppler falls back to drawing default
        checkbox chrome when the Off appearance is EMPTY. An invisible 0.01pt white rect
        keeps it blank while suppressing that fallback, so "" would be a regression that
        only shows up in one viewer."""
        blank = post_process.cb_off(20, 20)
        self.assertNotEqual(blank.strip(), "")
        self.assertIn("1 1 1 rg", blank)
        self.assertIn("re f", blank)

    def test_off_painters_ignore_the_box_size(self):
        # The two parameters exist for call-site symmetry (the module notes S1172); the
        # blank must not vary with them.
        self.assertEqual(post_process.cb_off(5, 5), post_process.cb_off(500, 500))

    def test_ellipse_uses_the_padded_radius_on_a_normal_sized_box(self):
        # w/2 - pad = 8.7 beats the 8.5 floor, so the padded value is used.
        path = post_process.ellipse_path(20, 20)
        self.assertTrue(path.startswith("18.70 10.00 m "))

    def test_ellipse_falls_back_to_a_proportional_floor_on_a_small_box(self):
        """The other branch of `max`. On a 4pt glyph box `w/2 - pad` is 0.7, which would
        draw a highlight too small to see; the 0.85 proportional floor gives 1.7."""
        path = post_process.ellipse_path(4, 4)
        self.assertTrue(path.startswith("3.70 2.00 m "))

    def test_ellipse_is_drawn_with_four_bezier_segments(self):
        self.assertEqual(post_process.ellipse_path(20, 20).count(" c"), 4)

    def test_highlight_fields_are_the_spinal_markers(self):
        self.assertTrue(post_process.is_highlight("packet.doctor.spinal.cervical.t"))

    def test_jtech_checkboxes_are_not_highlight_fields(self):
        """The one exception the predicate exists to carve out: the J-Tech boxes live
        under the same prefix but are ordinary checkboxes, so they must get the square
        mark rather than a yellow ellipse."""
        self.assertFalse(post_process.is_highlight("packet.doctor.spinal.jtech_report"))

    def test_fields_outside_the_spinal_prefix_are_not_highlights(self):
        self.assertFalse(post_process.is_highlight("packet.doctor.obs.limps"))


class XObjectTest(unittest.TestCase):
    def test_make_xobj_builds_a_form_xobject_sized_to_the_widget(self):
        pdf = fake.Pdf()
        stream = post_process.make_xobj(pdf, 12.345, 6.789, "q Q")
        self.assertEqual(str(stream[Name.Type]), "/XObject")
        self.assertEqual(str(stream[Name.Subtype]), "/Form")
        self.assertEqual(list(stream[Name.BBox]), [0, 0, 12.35, 6.79])
        self.assertEqual(stream.read_text(), "q Q")

    def test_make_xobj_alpha_carries_a_translucent_graphics_state(self):
        """The highlight must not hide the glyph underneath -- the glyph is WeasyPrint
        page text and shows THROUGH the ellipse. That only works at ca < 1."""
        pdf = fake.Pdf()
        stream = post_process.make_xobj_alpha(pdf, 20, 20, "q Q")
        gs1 = stream[Name.Resources][Name.ExtGState][Name.GS1]
        self.assertEqual(gs1[Name.ca], 0.40)
        self.assertEqual(gs1[Name.CA], 1.0)


class OnStateTest(unittest.TestCase):
    def test_the_on_state_is_read_from_the_existing_appearance(self):
        self.assertEqual(post_process.onstate_of(make_widget(on="/On")), "/On")

    def test_a_widget_with_no_appearance_defaults_to_yes(self):
        self.assertEqual(post_process.onstate_of(make_widget(on=None)), "/Yes")

    def test_a_widget_whose_only_state_is_off_defaults_to_yes(self):
        """The loop has to SKIP /Off rather than return it -- returning "/Off" as the on
        state would make the widget permanently unselectable."""
        widget = Dictionary(Rect=Array([0.0, 0.0, 20.0, 20.0]))
        appearances = Dictionary()
        appearances[Name("/Off")] = Dictionary()
        widget[Name.AP] = Dictionary(N=appearances)
        self.assertEqual(post_process.onstate_of(widget), "/Yes")


class AppearanceAssignmentTest(unittest.TestCase):
    def test_set_ap_gives_a_checkbox_the_square_mark(self):
        pdf = fake.Pdf()
        widget = make_widget()
        post_process.set_ap(pdf, widget, False)
        self.assertIn("re f", widget[Name.AP][Name.N][Name("/Yes")].read_text())

    def test_set_ap_gives_a_radio_the_dot(self):
        pdf = fake.Pdf()
        widget = make_widget()
        post_process.set_ap(pdf, widget, True)
        self.assertEqual(widget[Name.AP][Name.N][Name("/Yes")].read_text().count(" c"), 4)

    def test_set_ap_writes_both_states(self):
        pdf = fake.Pdf()
        widget = make_widget()
        post_process.set_ap(pdf, widget, False)
        states = {str(k) for k in widget[Name.AP][Name.N].keys()}
        self.assertEqual(states, {"/Off", "/Yes"})

    def test_set_ap_suppresses_the_widget_border(self):
        """WeasyPrint draws its own border from /BS + /MK. Left in place, it renders as a
        small box nested inside the checkbox -- the module names this as the bug."""
        pdf = fake.Pdf()
        widget = make_widget()
        post_process.set_ap(pdf, widget, False)
        self.assertEqual(widget[Name.BS][Name.W], 0)

    def test_set_ap_deletes_an_existing_appearance_characteristics_dictionary(self):
        """A REMOVAL, so the fixture seeds /MK for it to remove. Without the seed this
        test would pass with the `del` deleted -- the empty-fixture trap."""
        pdf = fake.Pdf()
        widget = make_widget(MK=Dictionary(BG=Array([1, 1, 1])))
        self.assertIn(Name.MK, widget)  # load-bearing precondition, not setup noise
        post_process.set_ap(pdf, widget, False)
        self.assertNotIn(Name.MK, widget)

    def test_set_hl_ap_paints_a_translucent_ellipse_on_the_on_state(self):
        pdf = fake.Pdf()
        widget = make_widget()
        post_process.set_hl_ap(pdf, widget)
        painted = widget[Name.AP][Name.N][Name("/Yes")].read_text()
        self.assertIn("/GS1 gs", painted)
        self.assertIn(post_process.HL_RGB, painted)

    def test_set_hl_ap_leaves_the_off_state_blank(self):
        pdf = fake.Pdf()
        widget = make_widget()
        post_process.set_hl_ap(pdf, widget)
        off = widget[Name.AP][Name.N][Name("/Off")].read_text()
        self.assertNotIn("/GS1 gs", off)
        self.assertIn("1 1 1 rg", off)

    def test_set_hl_ap_deletes_an_existing_mk(self):
        pdf = fake.Pdf()
        widget = make_widget(MK=Dictionary(BG=Array([1, 1, 1])))
        self.assertIn(Name.MK, widget)
        post_process.set_hl_ap(pdf, widget)
        self.assertNotIn(Name.MK, widget)

    def test_appearance_boxes_match_the_widget_rectangle(self):
        pdf = fake.Pdf()
        widget = make_widget(rect=(10.0, 20.0, 34.0, 38.0))
        post_process.set_ap(pdf, widget, False)
        self.assertEqual(list(widget[Name.AP][Name.N][Name("/Yes")][Name.BBox]), [0, 0, 24.0, 18.0])

    def test_a_reversed_rectangle_still_yields_a_positive_box(self):
        # abs() on both spans. A negative BBox would make the mark vanish.
        pdf = fake.Pdf()
        widget = make_widget(rect=(34.0, 38.0, 10.0, 20.0))
        post_process.set_ap(pdf, widget, False)
        self.assertEqual(list(widget[Name.AP][Name.N][Name("/Yes")][Name.BBox]), [0, 0, 24.0, 18.0])


class EnsureAcroFormTest(unittest.TestCase):
    def test_an_acroform_is_created_when_the_pdf_has_none(self):
        """The AttorneyCE notices are FLAT -- no form fields at all. Without this the
        renderer's finalize would raise on them, taking out a document that needs no
        post-processing in the first place."""
        pdf = make_pdf(with_acroform=False)
        acro = post_process._ensure_acroform(pdf)
        self.assertEqual(list(acro.Fields), [])
        self.assertIn(Name("/AcroForm"), pdf.Root)

    def test_a_fields_array_is_added_to_an_acroform_that_lacks_one(self):
        pdf = fake.Pdf()
        pdf.Root.AcroForm = Dictionary()
        acro = post_process._ensure_acroform(pdf)
        self.assertEqual(list(acro.Fields), [])

    def test_an_existing_acroform_is_returned_untouched(self):
        existing = make_widget()
        pdf = make_pdf(fields=[existing])
        acro = post_process._ensure_acroform(pdf)
        self.assertEqual(list(acro.Fields), [existing])


class FixTest(unittest.TestCase):
    """`fix` is the module's core. Each branch gets a field shaped to reach it."""

    def setUp(self):
        fake.Pdf.reset_registry()

    def _fix(self, pdf, path="/work/page1.pdf"):
        fake.Pdf.register(path, pdf)
        _, printed = run_quietly(post_process.fix, path)
        return printed

    def test_need_appearances_is_turned_off(self):
        """WeasyPrint defaults NeedAppearances to True, which tells the viewer to
        REGENERATE appearances -- discarding the marks this module just wrote and hiding
        circle-the-choice entirely."""
        pdf = make_pdf(fields=[])
        pdf.Root.AcroForm.NeedAppearances = True
        self._fix(pdf)
        self.assertFalse(pdf.Root.AcroForm.NeedAppearances)

    def test_a_plain_checkbox_is_counted_and_painted(self):
        checkbox = make_widget(FT=Name("/Btn"), T="packet.doctor.obs.limps")
        printed = self._fix(make_pdf(fields=[checkbox]))
        self.assertIn("checkboxes=1", printed)
        self.assertIn("radios=0", printed)
        self.assertIn("re f", checkbox[Name.AP][Name.N][Name("/Yes")].read_text())

    def test_a_pushbutton_is_skipped_entirely(self):
        """A pushbutton has no on/off state, so painting one would be meaningless -- and
        it must not inflate the checkbox count either."""
        button = make_widget(FT=Name("/Btn"), Ff=post_process.PUSHBUTTON, T="packet.doctor.print")
        printed = self._fix(make_pdf(fields=[button]))
        self.assertIn("checkboxes=0", printed)

    def test_a_non_button_field_is_left_to_the_autosize_pass(self):
        text = Dictionary(FT=Name("/Tx"), T="packet.doctor.name")
        printed = self._fix(make_pdf(fields=[text]))
        self.assertIn("checkboxes=0", printed)
        self.assertNotIn(Name.AP, text)

    def test_notoggletooff_is_cleared_so_a_radio_can_be_deselected(self):
        """A REMOVAL of a flag, so the fixture sets it. With NoToggleToOff left on, a
        patient who ticks the wrong radio cannot clear it -- the paper form allows that
        and so must the PDF."""
        kid = make_widget()
        group = Dictionary(
            FT=Name("/Btn"),
            Ff=post_process.RADIO | post_process.NOTOGGLE,
            T="packet.doctor.obs.device",
            Kids=Array([kid]),
        )
        self.assertTrue(int(group.get("/Ff")) & post_process.NOTOGGLE)
        self._fix(make_pdf(fields=[group]))
        self.assertFalse(int(group[Name.Ff]) & post_process.NOTOGGLE)
        # The RADIO flag itself must survive -- clearing it would stop the group behaving
        # as a radio at all.
        self.assertTrue(int(group[Name.Ff]) & post_process.RADIO)

    def test_a_radio_without_notoggle_keeps_its_flags(self):
        kid = make_widget()
        group = Dictionary(
            FT=Name("/Btn"), Ff=post_process.RADIO, T="packet.doctor.obs.gait", Kids=Array([kid])
        )
        self._fix(make_pdf(fields=[group]))
        self.assertEqual(int(group[Name.Ff]), post_process.RADIO)

    def test_radio_kids_lose_the_mis_tagged_title(self):
        """A REMOVAL, so the kid is seeded WITH /T.

        WeasyPrint gives each kid its own /T equal to the group name, which yields a
        malformed 'group.group' fully-qualified name and pypdf 'already parsed' warnings.
        Kids are widgets, not sub-fields, so /T has to go.
        """
        kid = make_widget(T="packet.doctor.obs.device")
        self.assertIn(Name.T, kid)  # load-bearing precondition
        group = Dictionary(
            FT=Name("/Btn"), Ff=post_process.RADIO, T="packet.doctor.obs.device", Kids=Array([kid])
        )
        self._fix(make_pdf(fields=[group]))
        self.assertNotIn(Name.T, kid)

    def test_radio_kids_are_painted_with_the_dot(self):
        kid = make_widget()
        group = Dictionary(
            FT=Name("/Btn"), Ff=post_process.RADIO, T="packet.doctor.obs.device", Kids=Array([kid])
        )
        printed = self._fix(make_pdf(fields=[group]))
        self.assertIn("radios=1", printed)
        self.assertEqual(kid[Name.AP][Name.N][Name("/Yes")].read_text().count(" c"), 4)

    def test_duplicated_radio_kids_are_dropped_from_the_top_level_fields(self):
        """A REMOVAL that an empty fixture cannot prove.

        WeasyPrint lists each radio kid BOTH under the parent /Kids and again as a
        top-level AcroForm.Fields entry. The fixture therefore seeds the kid in BOTH
        places -- which is the shape the code exists to repair. Seed it only under /Kids
        and this test passes with the de-listing deleted.
        """
        pdf = fake.Pdf()
        kid = make_widget()
        pdf.make_indirect(kid)  # only INDIRECT kids are collected by objgen
        group = Dictionary(
            FT=Name("/Btn"), Ff=post_process.RADIO, T="packet.doctor.obs.device", Kids=Array([kid])
        )
        pdf.Root.AcroForm = Dictionary(Fields=Array([group, kid]))
        self.assertEqual(len(pdf.Root.AcroForm.Fields), 2)  # the duplicate is present

        self._fix(pdf)

        remaining = list(pdf.Root.AcroForm.Fields)
        self.assertEqual(remaining, [group])
        self.assertNotIn(kid, remaining)

    def test_a_direct_radio_kid_is_not_delisted(self):
        # Only indirect kids carry an objgen to match on; a direct one is not a duplicate
        # reference and must survive the filter.
        kid = make_widget()
        group = Dictionary(
            FT=Name("/Btn"), Ff=post_process.RADIO, T="packet.doctor.obs.a", Kids=Array([kid])
        )
        pdf = make_pdf(fields=[group])
        self._fix(pdf)
        self.assertEqual(list(pdf.Root.AcroForm.Fields), [group])

    def test_a_tagged_radio_group_is_painted_as_a_highlight_not_a_radio(self):
        kid = make_widget()
        group = Dictionary(
            FT=Name("/Btn"),
            Ff=post_process.RADIO,
            T="packet.doctor.spinal.cervical",
            Kids=Array([kid]),
            CcChoice=True,
        )
        printed = self._fix(make_pdf(fields=[group]))
        self.assertIn("highlights=1", printed)
        self.assertIn("radios=0", printed)
        self.assertIn("/GS1 gs", kid[Name.AP][Name.N][Name("/Yes")].read_text())

    def test_a_tagged_checkbox_is_painted_as_a_highlight(self):
        checkbox = make_widget(FT=Name("/Btn"), T="packet.doctor.spinal.x", CcChoice=True)
        printed = self._fix(make_pdf(fields=[checkbox]))
        self.assertIn("highlights=1", printed)
        self.assertIn("checkboxes=0", printed)
        self.assertIn("/GS1 gs", checkbox[Name.AP][Name.N][Name("/Yes")].read_text())

    def test_a_checkbox_with_kids_paints_every_kid(self):
        first, second = make_widget(), make_widget()
        group = Dictionary(FT=Name("/Btn"), T="packet.doctor.obs.multi", Kids=Array([first, second]))
        self._fix(make_pdf(fields=[group]))
        for kid in (first, second):
            self.assertIn("re f", kid[Name.AP][Name.N][Name("/Yes")].read_text())

    def test_a_single_line_text_field_has_its_font_size_zeroed(self):
        """Auto-size: "/Helv 9 Tf" becomes "/Helv 0 Tf", which makes a viewer shrink a
        long entry to fit instead of clipping it. Seeded with a NON-zero size, or the
        substitution would have nothing to change."""
        text = Dictionary(FT=Name("/Tx"), T="packet.doctor.name", DA="/Helv 9 Tf 0 g")
        printed = self._fix(make_pdf(fields=[text]))
        self.assertIn("autosized=1", printed)
        self.assertEqual(str(text[Name("/DA")]), "/Helv 0 Tf 0 g")

    def test_a_multiline_text_field_keeps_its_fixed_size(self):
        """The other branch, and a real rule: multiline fields WRAP, so shrinking their
        text would make a long answer unreadable rather than fitting it."""
        text = Dictionary(
            FT=Name("/Tx"), T="packet.doctor.notes", Ff=1 << 12, DA="/Helv 9 Tf 0 g"
        )
        printed = self._fix(make_pdf(fields=[text]))
        self.assertIn("autosized=0", printed)
        self.assertEqual(str(text[Name("/DA")]), "/Helv 9 Tf 0 g")

    def test_a_text_field_with_no_default_appearance_is_skipped(self):
        text = Dictionary(FT=Name("/Tx"), T="packet.doctor.plain")
        printed = self._fix(make_pdf(fields=[text]))
        self.assertIn("autosized=0", printed)

    def test_an_already_auto_sized_field_is_not_counted_twice(self):
        # `new != str(da)` guards the counter, so running fix twice must not inflate it.
        text = Dictionary(FT=Name("/Tx"), T="packet.doctor.name", DA="/Helv 0 Tf 0 g")
        printed = self._fix(make_pdf(fields=[text]))
        self.assertIn("autosized=0", printed)

    def test_only_the_first_font_size_is_rewritten(self):
        # count=1 on the substitution. A /DA carrying a later numeric operand must keep it.
        text = Dictionary(FT=Name("/Tx"), T="packet.doctor.name", DA="/Helv 9 Tf 12 Tf")
        self._fix(make_pdf(fields=[text]))
        self.assertEqual(str(text[Name("/DA")]), "/Helv 0 Tf 12 Tf")

    def test_the_document_is_saved(self):
        pdf = make_pdf(fields=[])
        self._fix(pdf, "/work/out.pdf")
        self.assertEqual(pdf.saved_to, ["/work/out.pdf"])

    def test_the_input_is_opened_for_overwrite(self):
        pdf = make_pdf(fields=[])
        self._fix(pdf)
        self.assertTrue(pdf.opened_with.get("allow_overwriting_input"))

    def test_a_flat_pdf_with_no_form_passes_through_unchanged(self):
        pdf = make_pdf(with_acroform=False)
        printed = self._fix(pdf)
        self.assertIn("radios=0 checkboxes=0 highlights=0 autosized=0", printed)
        self.assertEqual(pdf.saved_to, ["/work/page1.pdf"])

    def test_a_mixed_document_counts_each_category_separately(self):
        """One document with every shape at once, so the counters cannot be satisfied by
        a single branch running repeatedly."""
        checkbox = make_widget(FT=Name("/Btn"), T="packet.doctor.obs.limps")
        radio_kid = make_widget()
        radio = Dictionary(
            FT=Name("/Btn"), Ff=post_process.RADIO, T="packet.doctor.obs.d", Kids=Array([radio_kid])
        )
        highlight = make_widget(FT=Name("/Btn"), T="packet.doctor.spinal.y", CcChoice=True)
        text = Dictionary(FT=Name("/Tx"), T="packet.doctor.name", DA="/Helv 9 Tf")
        printed = self._fix(make_pdf(fields=[checkbox, radio, highlight, text]))
        self.assertIn("radios=1", printed)
        self.assertIn("checkboxes=1", printed)
        self.assertIn("highlights=1", printed)
        self.assertIn("autosized=1", printed)


class ActivateTest(unittest.TestCase):
    def test_a_bare_checkbox_is_turned_on(self):
        widget = make_widget(on="/On")
        post_process.activate(widget)
        self.assertEqual(str(widget[Name.V]), "/On")
        self.assertEqual(str(widget[Name.AS]), "/On")

    def test_a_radio_group_selects_its_first_option_only(self):
        """Selecting two kids in one group would be an invalid radio state."""
        first, second, third = make_widget(), make_widget(), make_widget()
        group = Dictionary(Kids=Array([first, second, third]))
        post_process.activate(group)
        self.assertEqual(str(group[Name.V]), "/Yes")
        self.assertEqual(str(first[Name.AS]), "/Yes")
        self.assertEqual(str(second[Name.AS]), "/Off")
        self.assertEqual(str(third[Name.AS]), "/Off")


class DemoTest(unittest.TestCase):
    def setUp(self):
        fake.Pdf.reset_registry()

    def test_only_the_named_fields_are_activated(self):
        wanted = make_widget(T="packet.doctor.obs.limps")
        other = make_widget(T="packet.doctor.obs.altered_gait")
        pdf = make_pdf(fields=[wanted, other])
        fake.Pdf.register("/work/src.pdf", pdf)

        post_process.demo("/work/src.pdf", "/work/dst.pdf", ["packet.doctor.obs.limps"])

        self.assertEqual(str(wanted[Name.V]), "/Yes")
        self.assertNotIn(Name.V, other)

    def test_the_demo_is_written_to_the_destination(self):
        pdf = make_pdf(fields=[])
        fake.Pdf.register("/work/src.pdf", pdf)
        post_process.demo("/work/src.pdf", "/work/dst.pdf", [])
        self.assertEqual(pdf.saved_to, ["/work/dst.pdf"])


class HarvestCircleFieldsTest(unittest.TestCase):
    def setUp(self):
        fake.Pdf.reset_registry()

    @staticmethod
    def _link(uri, rect=(10.0, 20.0, 30.0, 40.0)):
        return Dictionary(
            Subtype=Name("/Link"), A=Dictionary(URI=uri), Rect=Array([float(v) for v in rect])
        )

    def _harvest(self, pdf, path="/work/page1.pdf"):
        fake.Pdf.register(path, pdf)
        return run_quietly(post_process.harvest_circle_fields, path)

    def test_a_cc_link_becomes_a_button_widget(self):
        page = fake.Page(Dictionary(Annots=Array([self._link("cc:packet.doctor.spinal.x")])))
        pdf = make_pdf(fields=[], pages=[page])
        added, printed = self._harvest(pdf)
        self.assertEqual(added, 1)
        self.assertIn("circle fields harvested: 1", printed)
        widget = list(pdf.Root.AcroForm.Fields)[0]
        self.assertEqual(str(widget[Name.Subtype]), "/Widget")
        self.assertEqual(str(widget[Name.FT]), "/Btn")
        self.assertEqual(widget[Name.T], "packet.doctor.spinal.x")

    def test_the_harvested_widget_is_tagged_for_the_highlight_appearance(self):
        """CcChoice is the tag `fix` reads to choose the yellow ellipse over a square
        mark. Untagged, the circle-the-choice glyphs would get checkbox marks drawn over
        them."""
        page = fake.Page(Dictionary(Annots=Array([self._link("cc:packet.doctor.spinal.x")])))
        pdf = make_pdf(fields=[], pages=[page])
        self._harvest(pdf)
        self.assertTrue(list(pdf.Root.AcroForm.Fields)[0][Name("/CcChoice")])

    def test_the_widget_takes_the_glyph_rectangle(self):
        page = fake.Page(
            Dictionary(Annots=Array([self._link("cc:x", rect=(10.0, 20.0, 30.0, 40.0))]))
        )
        pdf = make_pdf(fields=[], pages=[page])
        self._harvest(pdf)
        widget = list(pdf.Root.AcroForm.Fields)[0]
        self.assertEqual(list(widget[Name.Rect]), [10.0, 20.0, 30.0, 40.0])

    def test_a_reversed_rectangle_is_normalised(self):
        """The anchor rect can arrive with its corners in either order; an un-normalised
        rect gives a negative-extent widget the viewer will not draw."""
        page = fake.Page(
            Dictionary(Annots=Array([self._link("cc:x", rect=(30.0, 40.0, 10.0, 20.0))]))
        )
        pdf = make_pdf(fields=[], pages=[page])
        self._harvest(pdf)
        widget = list(pdf.Root.AcroForm.Fields)[0]
        self.assertEqual(list(widget[Name.Rect]), [10.0, 20.0, 30.0, 40.0])

    def test_the_link_annotation_is_replaced_rather_than_kept_alongside(self):
        """A REMOVAL: the original Link must not survive, or the glyph stays clickable as
        a dead `cc:` hyperlink on top of the new widget. Seeded as the only annot, and
        asserted by identity."""
        link = self._link("cc:packet.doctor.spinal.x")
        page_obj = Dictionary(Annots=Array([link]))
        pdf = make_pdf(fields=[], pages=[fake.Page(page_obj)])
        self._harvest(pdf)
        remaining = list(page_obj[Name.Annots])
        self.assertEqual(len(remaining), 1)
        self.assertIsNot(remaining[0], link)

    def test_a_non_cc_link_is_preserved(self):
        """A genuine hyperlink in the packet must survive the harvest untouched."""
        ordinary = self._link("https://example.test/help")
        page_obj = Dictionary(Annots=Array([ordinary]))
        pdf = make_pdf(fields=[], pages=[fake.Page(page_obj)])
        added, _ = self._harvest(pdf)
        self.assertEqual(added, 0)
        self.assertEqual(list(page_obj[Name.Annots]), [ordinary])

    def test_a_non_link_annotation_is_preserved(self):
        other = Dictionary(Subtype=Name("/Popup"), Rect=Array([0.0, 0.0, 1.0, 1.0]))
        page_obj = Dictionary(Annots=Array([other]))
        pdf = make_pdf(fields=[], pages=[fake.Page(page_obj)])
        added, _ = self._harvest(pdf)
        self.assertEqual(added, 0)
        self.assertEqual(list(page_obj[Name.Annots]), [other])

    def test_a_link_with_no_action_is_preserved(self):
        # `act is not None` guard: a Link without /A would otherwise raise.
        bare = Dictionary(Subtype=Name("/Link"), Rect=Array([0.0, 0.0, 1.0, 1.0]))
        page_obj = Dictionary(Annots=Array([bare]))
        pdf = make_pdf(fields=[], pages=[fake.Page(page_obj)])
        added, _ = self._harvest(pdf)
        self.assertEqual(added, 0)
        self.assertEqual(list(page_obj[Name.Annots]), [bare])

    def test_a_page_with_no_annotations_is_skipped(self):
        pdf = make_pdf(fields=[], pages=[fake.Page(Dictionary())])
        added, _ = self._harvest(pdf)
        self.assertEqual(added, 0)

    def test_every_page_is_harvested(self):
        pages = [
            fake.Page(Dictionary(Annots=Array([self._link("cc:a")]))),
            fake.Page(Dictionary(Annots=Array([self._link("cc:b"), self._link("cc:c")]))),
        ]
        pdf = make_pdf(fields=[], pages=pages)
        added, _ = self._harvest(pdf)
        self.assertEqual(added, 3)
        self.assertEqual(len(list(pdf.Root.AcroForm.Fields)), 3)

    def test_the_document_is_saved_after_harvesting(self):
        pdf = make_pdf(fields=[], pages=[fake.Page(Dictionary())])
        self._harvest(pdf, "/work/harvest.pdf")
        self.assertEqual(pdf.saved_to, ["/work/harvest.pdf"])


class FinalizeTest(unittest.TestCase):
    """The renderer's entry point: harvest, then fix, in that order."""

    def setUp(self):
        fake.Pdf.reset_registry()

    def test_a_harvested_circle_field_ends_up_with_the_highlight_appearance(self):
        """The two halves have to compose: harvest tags the widget, fix reads the tag.

        This is the only test that proves the ORDER. Run fix before harvest and the
        widget exists but was never painted, which no single-function test would catch.
        """
        link = Dictionary(
            Subtype=Name("/Link"),
            A=Dictionary(URI="cc:packet.doctor.spinal.cervical.t"),
            Rect=Array([10.0, 20.0, 30.0, 40.0]),
        )
        pdf = make_pdf(fields=[], pages=[fake.Page(Dictionary(Annots=Array([link])))])
        fake.Pdf.register("/work/page1.pdf", pdf)

        _, printed = run_quietly(post_process.finalize, "/work/page1.pdf")

        self.assertIn("circle fields harvested: 1", printed)
        self.assertIn("highlights=1", printed)
        widget = list(pdf.Root.AcroForm.Fields)[0]
        self.assertIn("/GS1 gs", widget[Name.AP][Name.N][Name("/Yes")].read_text())

    def test_a_flat_notice_passes_through_without_raising(self):
        """The AttorneyCE packets have no form fields at all. finalize must be safe on
        them -- the module's docstring promises exactly this."""
        pdf = make_pdf(with_acroform=False, pages=[fake.Page(Dictionary())])
        fake.Pdf.register("/work/flat.pdf", pdf)

        _, printed = run_quietly(post_process.finalize, "/work/flat.pdf")

        self.assertIn("circle fields harvested: 0", printed)
        self.assertIn("radios=0 checkboxes=0 highlights=0 autosized=0", printed)

    def test_finalize_saves_once_per_stage(self):
        pdf = make_pdf(fields=[], pages=[fake.Page(Dictionary())])
        fake.Pdf.register("/work/page1.pdf", pdf)
        run_quietly(post_process.finalize, "/work/page1.pdf")
        self.assertEqual(pdf.saved_to, ["/work/page1.pdf", "/work/page1.pdf"])


if __name__ == "__main__":
    unittest.main()
