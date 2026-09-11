"""The two report parsers.

Both key coverage by line number rather than counting records, which
de-duplicates a file appearing more than once -- several `<class>` elements for
one source in Cobertura, or repeated `DA:` records in lcov. A line covered by
one suite and missed by another is covered, so the MAXIMUM hit count wins. Both
properties are asserted, because both are silent when wrong: they inflate or
deflate a denominator without producing an error.
"""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from gate_loader import gate

BACKSLASH = chr(92)

LCOV_TWO_FILES = """SF:src/app/a.ts
DA:1,1
DA:2,0
DA:3,5
end_of_record
SF:src/app/b.ts
DA:1,0
end_of_record
"""

LCOV_REPEATED_LINE = """SF:src/app/a.ts
DA:1,0
DA:1,4
DA:2,3
DA:2,0
end_of_record
"""

# THE ORDERING HERE IS LOAD-BEARING. Line 2 appears in both classes, and the
# HIGHER hit count comes FIRST.
#
# Written the other way round -- 0 first, 7 second -- `max()` and a plain
# last-write-wins assignment produce the identical answer, so the test passes
# with the de-duplication deleted. That is the same blindness as an empty
# fixture, arriving through ORDERING rather than absence: seen to fail caught it
# here, the assertion did not.
COBERTURA_TWO_CLASSES_ONE_FILE = """<?xml version="1.0" ?>
<coverage>
  <packages>
    <package name="p">
      <classes>
        <class filename="src/App/Thing.cs">
          <lines>
            <line number="1" hits="1"/>
            <line number="2" hits="7"/>
          </lines>
        </class>
        <class filename="src/App/Thing.cs">
          <lines>
            <line number="2" hits="0"/>
            <line number="3" hits="0"/>
          </lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
"""


class _TempDirCase(unittest.TestCase):
    """Gives each test an isolated directory, replacing pytest's `tmp_path`."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp_path = Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)

    def write(self, name: str, text: str) -> Path:
        target = self.tmp_path / name
        target.write_text(text, encoding="utf-8")
        return target


class TestParseLcov(_TempDirCase):
    def test_returns_per_line_hits_keyed_by_file(self):
        report = self.write("lcov.info", LCOV_TWO_FILES)
        self.assertEqual(
            gate.parse_lcov(report, ""),
            {"src/app/a.ts": {1: 1, 2: 0, 3: 5}, "src/app/b.ts": {1: 0}},
        )

    def test_a_repeated_line_takes_the_maximum_hit_count(self):
        """Covered by one suite and missed by another is COVERED."""
        report = self.write("lcov.info", LCOV_REPEATED_LINE)
        self.assertEqual(gate.parse_lcov(report, ""), {"src/app/a.ts": {1: 4, 2: 3}})

    def test_applies_the_prefix_to_every_file(self):
        report = self.write("lcov.info", LCOV_TWO_FILES)
        self.assertEqual(
            set(gate.parse_lcov(report, "angular")),
            {"angular/src/app/a.ts", "angular/src/app/b.ts"},
        )

    def test_normalises_windows_separators_from_karma(self):
        report = self.write(
            "lcov.info",
            "SF:src" + BACKSLASH + "app" + BACKSLASH + "a.ts\nDA:1,1\nend_of_record\n",
        )
        self.assertEqual(set(gate.parse_lcov(report, "angular")), {"angular/src/app/a.ts"})

    def test_a_malformed_DA_record_is_skipped_not_fatal(self):
        report = self.write("lcov.info", "SF:a.ts\nDA:notanumber,1\nDA:2,1\nend_of_record\n")
        self.assertEqual(gate.parse_lcov(report, ""), {"a.ts": {2: 1}})

    def test_an_empty_report_yields_no_files(self):
        report = self.write("lcov.info", "")
        self.assertEqual(gate.parse_lcov(report, ""), {})


class TestParseCobertura(_TempDirCase):
    def test_merges_classes_that_share_a_filename(self):
        """Partial classes and generic instantiations must not inflate the total."""
        report = self.write("cov.xml", COBERTURA_TWO_CLASSES_ONE_FILE)
        result = gate.parse_cobertura(report, "")
        self.assertEqual(list(result), ["src/App/Thing.cs"])
        # Line 2 appears in both classes, 7 then 0. Maximum wins; 3 lines, not 4.
        self.assertEqual(result["src/App/Thing.cs"], {1: 1, 2: 7, 3: 0})

    def test_a_class_without_a_filename_is_skipped(self):
        report = self.write(
            "cov.xml",
            '<coverage><class><lines><line number="1" hits="1"/></lines></class></coverage>',
        )
        self.assertEqual(gate.parse_cobertura(report, ""), {})

    def test_a_line_without_a_number_is_skipped(self):
        report = self.write(
            "cov.xml",
            '<coverage><class filename="a.cs"><lines>'
            '<line hits="1"/><line number="2" hits="1"/>'
            "</lines></class></coverage>",
        )
        self.assertEqual(gate.parse_cobertura(report, ""), {"a.cs": {2: 1}})

    def test_missing_hits_attribute_defaults_to_zero(self):
        report = self.write(
            "cov.xml",
            '<coverage><class filename="a.cs"><lines>'
            '<line number="1"/></lines></class></coverage>',
        )
        self.assertEqual(gate.parse_cobertura(report, ""), {"a.cs": {1: 0}})

    def test_applies_the_prefix(self):
        report = self.write("cov.xml", COBERTURA_TWO_CLASSES_ONE_FILE)
        self.assertEqual(
            list(gate.parse_cobertura(report, "prefixed")), ["prefixed/src/App/Thing.cs"]
        )


if __name__ == "__main__":
    unittest.main()
