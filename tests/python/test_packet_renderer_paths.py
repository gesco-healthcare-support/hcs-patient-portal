"""The packet-renderer must import and stay honest outside the image (#785).

`docker/packet-renderer/app.py` locates the generators tree by trying two
candidates relative to itself, because the repository layout and the built image
layout genuinely differ -- the Dockerfile both renames and changes depth:

    COPY tools/packet-templates        /app/generators
    COPY docker/packet-renderer/app.py /app/app.py

    image: app.py at /app/app.py  -> generators at ./generators
    repo:  app.py at docker/packet-renderer/app.py
                                  -> generators at ../../tools/packet-templates

These tests assert BEHAVIOUR, not the source text that produces it. An earlier
version of this file checked that particular spellings appeared in `app.py`,
which made it fail against an equivalent implementation that spelled the same
candidates differently -- the test was coupled to one author's phrasing rather
than to the guarantee.

Importing `app.py` needs flask, weasyprint and pikepdf, none of which the
`Python: Test` job installs, so they are stubbed. That is safe HERE and would
not be elsewhere: nothing under test calls into any of them. The path
resolution, the template map and the missing-template scan are all pure
filesystem work, and `post_process` is imported but never invoked.
"""

import importlib.util
import pathlib
import sys
import tempfile
import types
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
APP_PY = REPO_ROOT / "docker" / "packet-renderer" / "app.py"
PACKET_TEMPLATES = REPO_ROOT / "tools" / "packet-templates"


def _install_stubs():
    """Minimal stand-ins for the three third-party imports app.py performs."""
    flask = types.ModuleType("flask")
    flask.Flask = lambda *a, **k: types.SimpleNamespace(
        get=lambda *a, **k: (lambda f: f),
        post=lambda *a, **k: (lambda f: f),
        run=lambda *a, **k: None,
    )
    flask.Response = object
    flask.jsonify = lambda *a, **k: (a, k)
    flask.request = None

    weasyprint = types.ModuleType("weasyprint")
    weasyprint.HTML = object

    pikepdf = types.ModuleType("pikepdf")
    for name in ("Pdf", "Name", "Array", "Dictionary", "String"):
        setattr(pikepdf, name, object)

    return {"flask": flask, "weasyprint": weasyprint, "pikepdf": pikepdf}


def load_app():
    """Import app.py fresh, with the third-party imports stubbed.

    Fresh each time: `GENERATORS_ROOT` is resolved at import, so a cached module
    would hide a resolution change rather than exercise it.
    """
    saved = {k: sys.modules.get(k) for k in ("flask", "weasyprint", "pikepdf", "post_process")}
    sys.modules.update(_install_stubs())
    try:
        spec = importlib.util.spec_from_file_location("packet_renderer_app", APP_PY)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module
    finally:
        for key, value in saved.items():
            if value is None:
                sys.modules.pop(key, None)
            else:
                sys.modules[key] = value


class ImportOutsideAnyContainerTests(unittest.TestCase):
    """#785's first acceptance criterion, asserted directly.

    WHEN app.py is imported from a test run outside any container, THE SYSTEM
    SHALL resolve post_process successfully.

    This is the test the first attempt at this PR did not have. It passed its
    own path-resolution checks while `import app` still died on the next
    absolute path down -- the eager template load -- so nothing exercised the
    criterion the issue is written against.
    """

    def test_the_module_imports(self):
        self.assertIsNotNone(load_app())

    def test_post_process_is_resolved(self):
        app = load_app()
        self.assertTrue(hasattr(app, "post_process"))

    def test_the_generators_root_is_the_repository_tree(self):
        self.assertEqual(load_app().GENERATORS_ROOT, PACKET_TEMPLATES.resolve())

    def test_the_resolved_root_actually_contains_post_process(self):
        # The behavioural form of "the candidate is correct", and the one that
        # survives any rewording of how the candidates are spelled.
        root = load_app().GENERATORS_ROOT
        self.assertTrue((root / "shared" / "post_process.py").is_file())


class ResolutionFailureTests(unittest.TestCase):
    """When neither layout holds, it must say so and name what it tried."""

    def test_raises_naming_every_path_it_tried(self):
        app = load_app()
        with tempfile.TemporaryDirectory() as tmp:
            empty = pathlib.Path(tmp)
            original = app._GENERATOR_CANDIDATES
            app._GENERATOR_CANDIDATES = (empty / "nowhere-a", empty / "nowhere-b")
            try:
                with self.assertRaises(RuntimeError) as caught:
                    app._resolve_generators_root()
            finally:
                app._GENERATOR_CANDIDATES = original
        message = str(caught.exception)
        self.assertIn("nowhere-a", message)
        self.assertIn("nowhere-b", message)
        self.assertIn("post_process.py", message)

    def test_picks_the_first_candidate_that_holds(self):
        app = load_app()
        with tempfile.TemporaryDirectory() as tmp:
            good = pathlib.Path(tmp) / "good"
            (good / "shared").mkdir(parents=True)
            (good / "shared" / "post_process.py").write_text("", encoding="utf-8")
            original = app._GENERATOR_CANDIDATES
            app._GENERATOR_CANDIDATES = (pathlib.Path(tmp) / "absent", good)
            try:
                self.assertEqual(app._resolve_generators_root(), good)
            finally:
                app._GENERATOR_CANDIDATES = original


class TemplatePathTests(unittest.TestCase):
    """No template path may be an absolute container path any more."""

    def setUp(self):
        self.app = load_app()

    def test_all_four_templates_are_mapped(self):
        self.assertEqual(
            sorted(self.app._TEMPLATE_FILES),
            ["attorney-ame", "attorney-pqme", "doctor", "patient"],
        )

    def test_every_template_path_sits_under_the_resolved_root(self):
        root = self.app.GENERATORS_ROOT
        for name, path in self.app._TEMPLATE_FILES.items():
            with self.subTest(template=name):
                self.assertEqual(pathlib.Path(path).parts[: len(root.parts)], root.parts)

    def test_no_template_path_is_the_hardcoded_container_root(self):
        # The concrete defect: "/app/generators/doctor/doctor.html" and friends,
        # which resolve inside the image and nowhere else.
        for name, path in self.app._TEMPLATE_FILES.items():
            with self.subTest(template=name):
                self.assertFalse(str(path).replace("\\", "/").startswith("/app/generators"))


class LazyTemplateLoadTests(unittest.TestCase):
    """Importing must not read the generated HTML, which a checkout lacks.

    This is the guarantee that makes the module importable at all outside the
    image: the HTML is gitignored and produced at image build, so an eager read
    cannot succeed in a repository checkout.
    """

    def test_import_does_not_populate_the_cache(self):
        self.assertEqual(load_app()._TEMPLATE_CACHE, {})

    def test_an_absent_template_is_reported_missing(self):
        """A negative guarantee, proven against a fixture that is genuinely absent.

        An earlier draft asserted the REPOSITORY lacked the generated HTML, on
        the reasoning that it is gitignored and built into the image. That is
        true of a fresh checkout and false on any machine where the packet
        golden test has been run -- it invokes the generators, which emit their
        HTML next to themselves. The test therefore passed in CI and failed
        locally, which is a property of the environment rather than of the code.
        A temporary directory removes the dependency.
        """
        app = load_app()
        with tempfile.TemporaryDirectory() as tmp:
            absent = pathlib.Path(tmp) / "never-written.html"
            original = app._TEMPLATE_FILES
            app._TEMPLATE_FILES = {"doctor": absent}
            try:
                self.assertEqual(app._missing_templates(), ["doctor"])
            finally:
                app._TEMPLATE_FILES = original

    def test_missing_templates_names_every_absent_one_not_just_the_first(self):
        app = load_app()
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            present = root / "patient.html"
            present.write_text("<html></html>", encoding="utf-8")
            original = app._TEMPLATE_FILES
            app._TEMPLATE_FILES = {
                "doctor": root / "a.html",
                "patient": present,
                "attorney-ame": root / "b.html",
            }
            try:
                self.assertEqual(
                    sorted(app._missing_templates()), ["attorney-ame", "doctor"]
                )
            finally:
                app._TEMPLATE_FILES = original

    def test_a_present_non_empty_template_is_not_reported_missing(self):
        app = load_app()
        with tempfile.TemporaryDirectory() as tmp:
            real = pathlib.Path(tmp) / "doctor.html"
            real.write_text("<html></html>", encoding="utf-8")
            original = app._TEMPLATE_FILES
            app._TEMPLATE_FILES = {"doctor": real}
            try:
                self.assertEqual(app._missing_templates(), [])
            finally:
                app._TEMPLATE_FILES = original

    def test_an_empty_template_file_counts_as_missing(self):
        # `is_file() and st_size > 0`: without a zero-byte fixture the size half
        # could be deleted and nothing would notice. A truncated template is
        # exactly what a half-built image produces.
        app = load_app()
        with tempfile.TemporaryDirectory() as tmp:
            empty = pathlib.Path(tmp) / "doctor.html"
            empty.write_text("", encoding="utf-8")
            original = app._TEMPLATE_FILES
            app._TEMPLATE_FILES = {"doctor": empty}
            try:
                self.assertEqual(app._missing_templates(), ["doctor"])
            finally:
                app._TEMPLATE_FILES = original

    def test_load_template_reads_and_then_caches(self):
        app = load_app()
        with tempfile.TemporaryDirectory() as tmp:
            real = pathlib.Path(tmp) / "doctor.html"
            real.write_text("first", encoding="utf-8")
            original = app._TEMPLATE_FILES
            app._TEMPLATE_FILES = {"doctor": real}
            try:
                self.assertEqual(app._load_template("doctor"), "first")
                # Changing the file must NOT change the answer: the second read
                # has to come from the cache, which is what makes the deferral
                # cost one stat per worker rather than one per render.
                real.write_text("second", encoding="utf-8")
                self.assertEqual(app._load_template("doctor"), "first")
            finally:
                app._TEMPLATE_FILES = original
                app._TEMPLATE_CACHE.clear()


class RepositoryLayoutTests(unittest.TestCase):
    """The two files the resolution is expressed between must stay put."""

    def test_app_py_is_where_the_dockerfile_copies_from(self):
        self.assertTrue(APP_PY.is_file())

    def test_post_process_is_where_the_repo_candidate_points(self):
        self.assertTrue((PACKET_TEMPLATES / "shared" / "post_process.py").is_file())

    def test_no_absolute_container_path_remains_in_the_import_block(self):
        # Source-level, and deliberately the ONLY source-level assertion left:
        # it names the defect rather than a spelling of the fix, so it survives
        # any rewording that does not reintroduce the bug.
        source = APP_PY.read_text(encoding="utf-8")
        code = "\n".join(
            line for line in source.splitlines() if not line.lstrip().startswith("#")
        )
        self.assertNotIn('sys.path.insert(0, "/app/generators/shared")', code)


if __name__ == "__main__":
    unittest.main()
