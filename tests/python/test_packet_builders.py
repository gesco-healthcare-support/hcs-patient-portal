"""Coverage for the three packet generators: build_doctor, build_patient, build_attorney.

WHY THESE READ 0% TODAY, and what this file changes. `tools/packet-templates/tests/
test_golden_output.py` already runs all three on every pull request -- but through
`subprocess.run([sys.executable, script])`. A subprocess is invisible to the parent
`coverage run`, so the 689 statements in these files have been exercised on every PR while
reporting zero. pyproject.toml's `[tool.coverage.run]` note names the same four files as
"absent from the denominator entirely" until `include_namespace_packages` was added; they
are in it now, at 0%. This suite runs the same code IN-PROCESS, so the run is measured.

WHAT IT ASSERTS, beyond execution. The golden suite is a hash: it proves the output has
not CHANGED, and deliberately says nothing about what the output IS -- a builder that
emitted an empty document would pass it forever once the hash was recorded. The
assertions here are the complementary half: the document is well-formed, every registered
page contributes, the helper primitives produce the markup their callers assume, and the
LF-only write the golden suite depends on actually happens.

NOT A SECOND GOLDEN TEST. Nothing here pins a byte or a hash. That job is done, once, in
test_golden_output.py, and duplicating it would mean two files to update for one
deliberate template edit.

All fixture data is synthetic. These generators take no input: they emit BLANK forms, so
no patient data reaches them in production or here.
"""

from __future__ import annotations

import re
import unittest

import packet_loader


class DoctorHelperTest(unittest.TestCase):
    """The markup primitives every page of the doctor packet is assembled from.

    These are pure string functions, so they are asserted exactly. `_slug` gets the most
    attention: it derives the FIELD NAME from the display label, so a change in it
    silently renames form fields in a document that reaches the WCAB.
    """

    @classmethod
    def setUpClass(cls):
        cls.mod = packet_loader.load_module("build_doctor", packet_loader.DOCTOR)

    def test_colgroup_converts_twips_to_percentages_that_sum_to_100(self):
        html = self.mod._colgroup([1, 1, 2])
        self.assertEqual(
            html,
            '<colgroup><col style="width:25.000%"><col style="width:25.000%">'
            '<col style="width:50.000%"></colgroup>',
        )

    def test_colgroup_preserves_proportions_rather_than_equalising(self):
        # The docstring promises the ORIGINAL twip proportions survive. Equal-width
        # columns would be the obvious wrong implementation and this rules it out.
        widths = [float(w) for w in re.findall(r"width:([\d.]+)%", self.mod._colgroup([100, 300]))]
        self.assertEqual(widths, [25.0, 75.0])
        self.assertAlmostEqual(sum(widths), 100.0, places=2)

    def test_slug_strips_span_tags_from_a_label(self):
        self.assertEqual(
            self.mod._slug('<span class="fit">Cervical Compression</span>'),
            "cervical_compression",
        )

    def test_slug_strips_html_entities(self):
        # Real data: ORTHO_L carries "Braggard&#8217;s" (a right single quote entity).
        # Left in, the field name would carry the entity's digits.
        self.assertEqual(self.mod._slug("Braggard&#8217;s"), "braggards")

    def test_slug_collapses_runs_of_punctuation_into_single_underscores(self):
        self.assertEqual(self.mod._slug("Straight  Leg -- Raise"), "straight_leg_raise")

    def test_slug_does_not_leave_a_leading_or_trailing_underscore(self):
        self.assertEqual(self.mod._slug("  Flexion!  "), "flexion")

    def test_slug_of_pure_punctuation_is_empty_rather_than_underscores(self):
        self.assertEqual(self.mod._slug("+/-"), "")

    def test_txt_emits_a_named_text_input(self):
        self.assertEqual(self.mod._txt("packet.doctor.x"), '<input type="text" name="packet.doctor.x">')

    def test_jtech_defaults_to_range_of_motion(self):
        self.assertIn("J-Tech Report (Range of Motion)", self.mod._jtech("packet.doctor.j"))

    def test_jtech_takes_an_explicit_measurement_name(self):
        self.assertIn("J-Tech Report (Grip)", self.mod._jtech("packet.doctor.j", "Grip"))

    def test_cc_emits_an_anchor_not_a_form_input(self):
        """The whole circle-the-choice mechanism depends on this being an ANCHOR.

        post_process.harvest_circle_fields finds these by their `cc:` link annotation and
        replaces each with a highlight widget. Emitting an <input> here would make
        WeasyPrint bake a control box and the harvest would find nothing.
        """
        html = self.mod._cc("packet.doctor.spinal.x", "T", 16)
        self.assertIn('href="cc:packet.doctor.spinal.x"', html)
        self.assertTrue(html.startswith("<a "))
        self.assertNotIn("<input", html)

    def test_pm_single_offers_exactly_plus_and_minus(self):
        html = self.mod.pm_single("packet.doctor.o.x")
        self.assertIn("cc:packet.doctor.o.x.plus", html)
        self.assertIn("cc:packet.doctor.o.x.minus", html)
        self.assertEqual(html.count("<a "), 2)

    def test_ths_offers_tenderness_hypertonicity_spasm(self):
        html = self.mod.ths("packet.doctor.palp.x")
        for suffix in (".t", ".h", ".s"):
            self.assertIn(f"cc:packet.doctor.palp.x{suffix}", html)
        self.assertEqual(html.count("<a "), 3)

    def test_single_t_offers_only_tenderness(self):
        html = self.mod.single_t("packet.doctor.palp.y")
        self.assertEqual(html.count("<a "), 1)
        self.assertIn("cc:packet.doctor.palp.y.t", html)

    def test_swa_offers_the_three_pulse_grades(self):
        html = self.mod.swa("packet.doctor.vasc.x")
        self.assertEqual(html.count("<a "), 3)
        self.assertIn("cc:packet.doctor.vasc.x.a", html)

    def test_reflex_offers_grades_zero_through_five(self):
        html = self.mod.reflex("packet.doctor.reflex.x")
        self.assertEqual(html.count("<a "), 6)
        for grade in range(6):
            self.assertIn(f"cc:packet.doctor.reflex.x.{grade}", html)
        # 6 is not a reflex grade; an off-by-one in range() would add it.
        self.assertNotIn("cc:packet.doctor.reflex.x.6", html)

    def test_sensation_offers_up_normal_down(self):
        html = self.mod.sensation("packet.doctor.sens.x")
        self.assertEqual(html.count("<a "), 3)
        self.assertIn("cc:packet.doctor.sens.x.up", html)
        self.assertIn("cc:packet.doctor.sens.x.dn", html)

    def test_ribcage_keeps_its_parentheses_as_literal_text(self):
        # The parens are page furniture, not markup: "T ( P L A )". If they were ever
        # emitted as anchors they would become clickable highlight fields of their own.
        html = self.mod.ribcage("packet.doctor.rib.x")
        self.assertEqual(html.count("<a "), 4)
        self.assertIn("(", html)
        self.assertIn(")", html)


class DoctorPageTest(unittest.TestCase):
    """Every registered page renders, and the assembled document is well-formed."""

    @classmethod
    def setUpClass(cls):
        cls.mod = packet_loader.load_module("build_doctor", packet_loader.DOCTOR)

    def test_every_registered_page_renders_non_empty_markup(self):
        self.assertEqual(len(self.mod.PAGES), 8)
        for page in self.mod.PAGES:
            with self.subTest(page=page.__name__):
                html = page()
                self.assertIsInstance(html, str)
                self.assertGreater(len(html.strip()), 0)

    def test_pages_are_distinct_documents_rather_than_one_repeated(self):
        # A registration bug that listed the same function eight times would still
        # produce eight pages and still pass a "renders non-empty" check.
        rendered = [page() for page in self.mod.PAGES]
        self.assertEqual(len(set(rendered)), len(rendered))

    def test_the_spinal_page_carries_circle_the_choice_anchors(self):
        """post_process.is_highlight keys on the `packet.doctor.spinal.` prefix, so the
        two files have to agree about it. This is the producing half."""
        html = self.mod.page2()
        self.assertIn("cc:packet.doctor.spinal.", html)

    def test_nerve_inspection_and_neuro_tables_render(self):
        for builder in (self.mod._nerve_table, self.mod._inspection_table, self.mod._neuro_table):
            with self.subTest(table=builder.__name__):
                self.assertIn("<table", builder())


class DoctorInlineImagesTest(unittest.TestCase):
    """`_inline_images` decides what travels to the renderer service.

    The service has no access to the repository, so anything left as a relative path
    arrives broken. Each of the four branches is exercised.
    """

    @classmethod
    def setUpClass(cls):
        cls.mod = packet_loader.load_module("build_doctor", packet_loader.DOCTOR)

    def test_absolute_http_sources_are_left_alone(self):
        for scheme in ("http://x.test/a.png", "https://x.test/a.png", "data:image/png;base64,AAAA"):
            with self.subTest(scheme=scheme):
                html = f'<img src="{scheme}">'
                self.assertEqual(self.mod._inline_images(html), html)

    def test_a_missing_local_file_is_left_alone_rather_than_raising(self):
        # A broken path must not abort the build -- the document still renders, with one
        # missing image, which is recoverable. Raising here would lose the whole packet.
        html = '<img src="no-such-asset-7f3a.png">'
        self.assertEqual(self.mod._inline_images(html), html)

    def test_an_existing_local_file_is_embedded_as_a_data_uri(self):
        """The positive branch, which the two negative cases above cannot reach.

        The target is a repo file reached relatively from the module's own directory --
        `_inline_images` embeds any local file it is pointed at, and using a checked-in
        one keeps the test from depending on a binary fixture that could go missing.
        """
        html = self.mod._inline_images('<img src="../shared/check_fields.py">')
        self.assertIn("src=\"data:", html)
        self.assertIn(";base64,", html)
        self.assertNotIn("../shared/check_fields.py", html)

    def test_an_unguessable_mime_type_falls_back_to_image_png(self):
        """The `or "image/png"` fallback, which needs a file mimetypes CANNOT classify.

        The first version of this test pointed at a `.py` file and failed: mimetypes
        resolves that to `text/x-python`, so the fallback was never reached and the test
        was asserting something the code had no reason to do. `.sha256` is genuinely
        unknown to mimetypes -- verified with
        `python -c "import mimetypes; print(mimetypes.guess_type('golden.sha256'))"`,
        which prints `(None, None)`.

        Worth recording that the fallback is therefore REACHABLE, not dead code: any
        asset without a registered suffix takes it.
        """
        html = self.mod._inline_images('<img src="../tests/golden.sha256">')
        self.assertIn("data:image/png;base64,", html)


class DoctorBuildTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mod = packet_loader.load_module("build_doctor", packet_loader.DOCTOR)

    def test_build_writes_a_complete_html_document(self):
        with packet_loader.scratch_cwd() as work:
            self.mod.build()
            produced = work / "doctor.html"
            self.assertTrue(produced.is_file())
            text = produced.read_text(encoding="utf-8")
        self.assertTrue(text.startswith("<!DOCTYPE html>"))
        self.assertIn("<title>Doctor Packet</title>", text)
        self.assertTrue(text.rstrip().endswith("</html>"))

    def test_build_emits_one_page_div_per_registered_page(self):
        with packet_loader.scratch_cwd() as work:
            self.mod.build()
            text = (work / "doctor.html").read_text(encoding="utf-8")
        self.assertEqual(text.count('<div class="page">'), len(self.mod.PAGES))

    def test_build_writes_lf_line_endings_only(self):
        """The `newline="\\n"` argument, asserted rather than only commented.

        The golden suite hashes these bytes WITHOUT normalising CRLF, and its docstring
        explains why: a normalising hash would hide exactly this regression. Dropping the
        argument makes Python's text mode emit CRLF on Windows, so the developer copy and
        the packet-renderer image stop being the same bytes.

        HONEST LIMIT: on Linux -- which is where CI runs -- text mode already writes LF,
        so this assertion cannot fail there even with the argument deleted. It is a real
        guard on Windows, where the builders are edited, and a no-op on the runner. It is
        kept because that is where the mistake gets made, not where it gets caught.
        """
        with packet_loader.scratch_cwd() as work:
            self.mod.build()
            raw = (work / "doctor.html").read_bytes()
        self.assertNotIn(b"\r\n", raw)


class PatientBuilderTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mod = packet_loader.load_module("build_patient", packet_loader.PATIENT)

    def test_tok_wraps_a_merge_token_verbatim(self):
        """Tokens are substituted downstream by the renderer, so the marker must carry
        the token TEXT unchanged -- escaping or rewriting it would break the merge."""
        self.assertEqual(
            self.mod._tok("##Patient.FirstName##"),
            '<span class="tok">##Patient.FirstName##</span>',
        )

    def test_every_registered_page_renders_non_empty_markup(self):
        self.assertEqual(len(self.mod.PAGES), 15)
        for name, page in self.mod.PAGES:
            with self.subTest(page=name):
                html = page()
                self.assertIsInstance(html, str)
                self.assertGreater(len(html.strip()), 0)

    def test_registered_page_names_are_unique(self):
        names = [name for name, _ in self.mod.PAGES]
        self.assertEqual(sorted(names), sorted(set(names)))

    def test_adl_rows_render_for_every_catalogued_activity(self):
        """ADL drives the activities-of-daily-living grid. Each row must carry its own
        slug, or two activities would write into one field."""
        self.assertGreater(len(self.mod.ADL), 0)
        rendered = self.mod.page_adl()
        self.assertIn("<table", rendered)

    def test_adl_row_renders_a_labelled_row_for_one_activity(self):
        row = self.mod._adl_row("selfcare", "Bathing", "bathing")
        self.assertIn("Bathing", row)
        self.assertIn("bathing", row)

    def test_the_no_interference_option_is_offered(self):
        """"Does not interfere" is the zero-impairment answer on the AMA pain grid.

        It lives in `page_ama2` (build_patient.py:1020-1028), NOT in the ADL grid -- the
        first version of this test asserted it against `page_adl()` and failed. A grid
        that lost this option would force every patient to claim some impairment.

        The LITERAL is asserted, not `self.mod._NO_INTERFERE`. Designing the mutation pass
        exposed the earlier form as vacuous: the constant sits on both sides of the
        comparison, so editing it moved the expectation with the output and the assertion
        could not fail. Pinning the wording is also the actual guarantee -- this string is
        a patient-facing answer on a medical-legal form.
        """
        self.assertIn("Does not interfere", self.mod.page_ama2())
        self.assertEqual(self.mod._NO_INTERFERE, "Does not interfere")

    def test_build_writes_a_complete_document_with_every_page(self):
        with packet_loader.scratch_cwd() as work:
            self.mod.build()
            produced = work / "patient.html"
            self.assertTrue(produced.is_file())
            text = produced.read_text(encoding="utf-8")
            raw = produced.read_bytes()
        self.assertTrue(text.startswith("<!DOCTYPE html>"))
        self.assertTrue(text.rstrip().endswith("</html>"))
        self.assertNotIn(b"\r\n", raw)

    def test_build_output_contains_each_page_marker(self):
        """Every registered page contributes to the assembled document.

        The fragment is compared AFTER `_inline_images`, which is what the first version
        of this test got wrong: three pages (cover, pregnancy, deu1) open with an
        `<img src="images/...">`, and `build()` rewrites those to base64 data URIs, so the
        raw fragment is not present in the finished file. Inlining is a per-`src`
        substitution and independent of surrounding text, so inlining one page alone
        yields the same bytes it contributes to the whole.
        """
        with packet_loader.scratch_cwd() as work:
            self.mod.build()
            text = (work / "patient.html").read_text(encoding="utf-8")
        for name, page in self.mod.PAGES:
            with self.subTest(page=name):
                fragment = self.mod._inline_images(page()).strip()
                self.assertIn(fragment[:80], text)

    def test_inline_images_leaves_remote_sources_untouched(self):
        html = '<img src="https://cdn.test/logo.png">'
        self.assertEqual(self.mod._inline_images(html), html)

    def test_inline_images_embeds_a_local_file(self):
        html = self.mod._inline_images('<img src="../shared/check_fields.py">')
        self.assertIn(";base64,", html)


class AttorneyBuilderTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mod = packet_loader.load_module("build_attorney", packet_loader.ATTORNEY)

    def test_tok_wraps_a_merge_token(self):
        self.assertEqual(self.mod.tok("##Others.DateNow##"), '<span class="tok">##Others.DateNow##</span>')

    def test_letterhead_and_footer_render(self):
        self.assertIn("FELLOW, AMERICAN ACADEMY OF ORTHOPAEDIC SURGEONS", self.mod._letterhead())
        self.assertIn("SCHEDULING:", self.mod._lfoot())

    def test_the_three_notice_pages_render(self):
        for builder in (self.mod.attorney_notice, self.mod.patient_notice, self.mod.qme_form):
            with self.subTest(page=builder.__name__):
                html = builder()
                self.assertIsInstance(html, str)
                self.assertGreater(len(html.strip()), 0)

    def test_qcell_renders_a_labelled_cell(self):
        cell = self.mod._qcell("##Q.One##", "Question one")
        self.assertIn("Question one", cell)

    def test_qcell_takes_a_column_span(self):
        self.assertIn("2", self.mod._qcell("##Q.One##", "Question one", span=2))

    def test_ame_ime_builds_two_letter_pages(self):
        with packet_loader.scratch_cwd() as work:
            self.mod.build("ame_ime")
            text = (work / "ame_ime.html").read_text(encoding="utf-8")
        self.assertEqual(text.count('<div class="page letterpage">'), 2)
        self.assertIn("<title>ame_ime</title>", text)

    def test_pqme_adds_the_qme_form_as_a_third_page(self):
        """The two targets differ ONLY in that third page. Asserting the difference is
        what stops the two branches from quietly converging."""
        with packet_loader.scratch_cwd() as work:
            self.mod.build("pqme")
            text = (work / "pqme.html").read_text(encoding="utf-8")
        self.assertEqual(text.count('<div class="page letterpage">'), 2)
        self.assertEqual(text.count('<div class="page ">'), 1)
        self.assertIn("<title>pqme</title>", text)

    def test_an_unknown_target_is_refused_rather_than_writing_a_document(self):
        """The negative guarantee: `build` raises on an unrecognised target.

        Seeded so the refusal can actually be observed -- the scratch directory is checked
        for output afterwards, because a version that raised AFTER writing would still
        satisfy a bare assertRaises.
        """
        with packet_loader.scratch_cwd() as work:
            with self.assertRaises(SystemExit) as caught:
                self.mod.build("not-a-real-target")
            written = sorted(p.name for p in work.iterdir())
        self.assertIn("not-a-real-target", str(caught.exception))
        self.assertEqual(written, [])

    def test_both_targets_write_lf_only(self):
        with packet_loader.scratch_cwd() as work:
            self.mod.build("ame_ime")
            self.mod.build("pqme")
            for name in ("ame_ime.html", "pqme.html"):
                with self.subTest(document=name):
                    self.assertNotIn(b"\r\n", (work / name).read_bytes())


if __name__ == "__main__":
    unittest.main()
