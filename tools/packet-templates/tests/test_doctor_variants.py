"""The doctor packet variant table: what each variant is allowed to change.

The golden test pins the exact bytes. This pins the INTENT, so a reviewer reading a
future golden diff can tell a deliberate variant change from an accident: the standard
packet stays all-portrait, and the landscape variant rotates exactly its declared pages
and nothing else.
"""

from __future__ import annotations

import importlib.util
import os
import re
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(os.path.dirname(HERE), "doctor", "build_doctor.py")


def _load():
    spec = importlib.util.spec_from_file_location("build_doctor", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _page_classes(html: str) -> list[str]:
    return re.findall(r'<div class="(page(?: land)?)">', html)


class DoctorVariantTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.mod = _load()

    def test_standard_variant_has_no_landscape_pages_or_named_page(self) -> None:
        html = self.mod.build_variant(self.mod.VARIANTS["doctor"])
        self.assertEqual(["page"] * len(self.mod.PAGES), _page_classes(html))
        self.assertNotIn("landscape", html)

    def test_landscape_variant_rotates_exactly_the_declared_pages(self) -> None:
        variant = self.mod.VARIANTS["doctor-landscape"]
        html = self.mod.build_variant(variant)
        expected = [
            "page land" if n in variant["landscape_pages"] else "page"
            for n in range(1, len(self.mod.PAGES) + 1)
        ]
        self.assertEqual(expected, _page_classes(html))
        self.assertEqual(5, expected.count("page land"))
        self.assertIn("@page land { size: Letter landscape;", html)

    def test_variants_share_the_same_body_apart_from_orientation(self) -> None:
        std = self.mod.build_variant(self.mod.VARIANTS["doctor"])
        land = self.mod.build_variant(self.mod.VARIANTS["doctor-landscape"])
        strip = lambda h: re.sub(r"<style>.*?</style>", "", h.replace("page land", "page"), flags=re.S)
        self.assertEqual(strip(std), strip(land))

    def test_each_variant_writes_its_own_file(self) -> None:
        outputs = [v["output"] for v in self.mod.VARIANTS.values()]
        self.assertEqual(len(outputs), len(set(outputs)))


if __name__ == "__main__":
    unittest.main()
