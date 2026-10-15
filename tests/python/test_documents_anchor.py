"""The documents scroll anchor must exist on BOTH appointment detail templates (#805).

`AppointmentViewComponent.openUploadDocuments()` does:

    document.getElementById('appointment-documents-anchor')
      ?.scrollIntoView({ behavior: 'smooth', block: 'start' });

The INTERNAL template declared that id from the start. The EXTERNAL one did not --
it rendered `<app-appointment-documents>` with no id at all. The lookup returned
null, the `?.` swallowed it, and a button wired to the method would have scrolled
nowhere and logged nothing.

WHY THIS IS A TEST AND NOT A REVIEW NOTE. A missing scroll target produces no
symptom: no error, no console warning, no failed request. Nothing but a test can
notice it, which is how the divergence survived long enough for the method's own
comment to describe the opposite.

WHY IT LIVES HERE RATHER THAN IN A .spec.ts. The guarantee is "the id is declared
where the lookup expects it", which is a fact about the template source. Karma's
esbuild pipeline has no loader for `.html`, so a spec cannot import the template;
and rendering either detail component needs an appointment, a route and a dozen
stubbed services, whose own setup could drift and hide the thing being asserted.
Reading the two files states exactly the guarantee and nothing else.
"""

from __future__ import annotations

import pathlib
import re
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
COMPONENTS = REPO_ROOT / "angular" / "src" / "app" / "appointments" / "appointment" / "components"

ANCHOR_ID = "appointment-documents-anchor"
# The id as it appears ON AN ELEMENT, not merely somewhere in the file -- the
# method's own comment mentions the id, and a substring search would match that.
ANCHOR_ATTR = re.compile(r'id\s*=\s*"' + re.escape(ANCHOR_ID) + r'"')
DOCUMENTS_ELEMENT = re.compile(
    r"<app-appointment-documents\b.*?(?:/>|</app-appointment-documents>)", re.S
)

TEMPLATES = {
    "external": COMPONENTS / "external-appointment-detail.component.html",
    "internal": COMPONENTS / "internal-appointment-detail.component.html",
}


class DocumentsAnchorTests(unittest.TestCase):
    def _read(self, which: str) -> str:
        path = TEMPLATES[which]
        self.assertTrue(path.is_file(), f"{which} template missing at {path}")
        return path.read_text(encoding="utf-8")

    def test_both_templates_declare_the_anchor(self):
        for which in TEMPLATES:
            with self.subTest(template=which):
                self.assertRegex(
                    self._read(which),
                    ANCHOR_ATTR,
                    f"{which} template does not declare id=\"{ANCHOR_ID}\"; "
                    "openUploadDocuments() would scroll nowhere silently",
                )

    def test_the_anchor_sits_on_the_documents_component(self):
        # A bare id-exists check would pass if someone moved the id onto an
        # unrelated wrapper, and the shortcut would then scroll to the wrong
        # place -- which is still no error and still no symptom.
        for which in TEMPLATES:
            with self.subTest(template=which):
                element = DOCUMENTS_ELEMENT.search(self._read(which))
                self.assertIsNotNone(
                    element, f"{which}: no <app-appointment-documents> element found"
                )
                self.assertRegex(
                    element.group(0),
                    ANCHOR_ATTR,
                    f"{which}: the anchor id is not on the documents component",
                )

    def test_the_anchor_is_declared_exactly_once_per_template(self):
        # Duplicate ids make getElementById's answer arbitrary: it returns
        # whichever the parser saw first, so the shortcut silently picks one.
        for which in TEMPLATES:
            with self.subTest(template=which):
                self.assertEqual(
                    len(ANCHOR_ATTR.findall(self._read(which))),
                    1,
                    f"{which}: the anchor id must appear exactly once",
                )

    def test_the_method_still_looks_the_id_up(self):
        # The complement. If openUploadDocuments stops using getElementById --
        # replaced by a ViewChild, say -- these template assertions would keep
        # passing while guarding nothing. This fails instead, so whoever makes
        # that change has to decide what happens to this file.
        source = (COMPONENTS / "appointment-view.component.ts").read_text(encoding="utf-8")
        self.assertIn(
            f"getElementById('{ANCHOR_ID}')",
            source,
            "openUploadDocuments no longer resolves the anchor by id; "
            "the template assertions above now guard nothing",
        )


if __name__ == "__main__":
    unittest.main()
