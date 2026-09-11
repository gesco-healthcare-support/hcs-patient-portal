"""Tests for .claude/scripts/build-repo-map.py (#786).

This script produces `docs/repo-map/`, which `verify_structure.py` then checks
for freshness in CI. It had no tests.

Its failure mode is the quiet one: every extraction step returns a list, and an
empty list is a valid value. A regex that stops matching yields a map with no
symbols and no ranking, written successfully, reported as generated. Most of
what follows asserts that something was actually found, not merely that nothing
threw.
"""

import pathlib
import tempfile
import unittest
from collections import Counter

from gate_loader import load

repo_map = load("build_repo_map", ".claude/scripts/build-repo-map.py")


class _TempRepo(unittest.TestCase):
    """Redirects the module's REPO_ROOT at a scratch tree.

    `rel()` raises ValueError for paths outside REPO_ROOT, and several functions
    call it, so the root has to move with the fixture rather than the fixture
    living under the real repository.
    """

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self._tmp.name).resolve()
        self.addCleanup(self._tmp.cleanup)
        self.addCleanup(setattr, repo_map, "REPO_ROOT", repo_map.REPO_ROOT)
        repo_map.REPO_ROOT = self.root

    def write(self, relative, body):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(body, encoding="utf-8")
        return path


class IsExcludedTests(unittest.TestCase):
    def test_excludes_build_output(self):
        for part in ("bin", "obj", "node_modules"):
            with self.subTest(part=part):
                self.assertTrue(repo_map.is_excluded(pathlib.Path("a") / part / "x.cs"))

    def test_keeps_ordinary_source(self):
        self.assertFalse(repo_map.is_excluded(pathlib.Path("src/Domain/Thing.cs")))


class ReadTextSafeTests(_TempRepo):
    def test_reads_utf8(self):
        p = self.write("a.txt", "hello")
        self.assertEqual(repo_map.read_text_safe(p), "hello")

    def test_strips_a_bom(self):
        p = self.root / "bom.txt"
        p.write_bytes(b"\xef\xbb\xbfhello")
        self.assertEqual(repo_map.read_text_safe(p), "hello")

    def test_returns_none_for_undecodable_bytes(self):
        p = self.root / "bad.bin"
        p.write_bytes(bytes([0xFF, 0xFE, 0xFD, 0xFC]))
        self.assertIsNone(repo_map.read_text_safe(p))

    def test_returns_none_for_a_missing_file(self):
        self.assertIsNone(repo_map.read_text_safe(self.root / "absent.txt"))


class ExtractCsTests(_TempRepo):
    SOURCE = """using System;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class Appointment : AggregateRoot<Guid>
{
}

public interface IAppointmentRepository
{
}

public enum AppointmentKind
{
}
"""

    def test_finds_the_namespace(self):
        entry = repo_map.extract_cs(self.write("src/A.cs", self.SOURCE))
        self.assertEqual(
            entry["namespace"], "HealthcareSupport.CaseEvaluation.Appointments"
        )

    def test_finds_every_symbol_kind(self):
        entry = repo_map.extract_cs(self.write("src/A.cs", self.SOURCE))
        found = {(s["kind"], s["name"]) for s in entry["symbols"]}
        self.assertIn(("class", "Appointment"), found)
        self.assertIn(("interface", "IAppointmentRepository"), found)
        self.assertIn(("enum", "AppointmentKind"), found)

    def test_collects_usings_sorted_and_deduplicated(self):
        entry = repo_map.extract_cs(self.write("src/A.cs", self.SOURCE))
        self.assertEqual(
            entry["uses_namespaces"], ["System", "Volo.Abp.Domain.Entities"]
        )

    def test_reports_the_repo_relative_path_with_forward_slashes(self):
        entry = repo_map.extract_cs(self.write("src/deep/A.cs", self.SOURCE))
        self.assertEqual(entry["file"], "src/deep/A.cs")

    def test_a_file_with_no_namespace_yields_none_not_a_crash(self):
        entry = repo_map.extract_cs(self.write("src/B.cs", "public class Loose {}"))
        self.assertIsNone(entry["namespace"])
        self.assertTrue(entry["symbols"])

    def test_returns_none_when_the_file_cannot_be_read(self):
        p = self.root / "bad.cs"
        p.write_bytes(bytes([0xFF, 0xFE, 0xFD]))
        self.assertIsNone(repo_map.extract_cs(p))


class ExtractTsTests(_TempRepo):
    SOURCE = """import { Component } from '@angular/core';
import { AppointmentService } from './appointment.service';
import './side-effect';

export class AppointmentComponent {}
export interface AppointmentRow {}
export const APPOINTMENT_TOKEN = 1;
"""

    def test_finds_symbols(self):
        entry = repo_map.extract_ts(self.write("angular/a.ts", self.SOURCE))
        found = {(s["kind"], s["name"]) for s in entry["symbols"]}
        self.assertIn(("class", "AppointmentComponent"), found)
        self.assertIn(("interface", "AppointmentRow"), found)
        self.assertIn(("const", "APPOINTMENT_TOKEN"), found)

    def test_collects_imports_including_a_side_effect_import(self):
        entry = repo_map.extract_ts(self.write("angular/a.ts", self.SOURCE))
        self.assertEqual(
            entry["imports"],
            ["./appointment.service", "./side-effect", "@angular/core"],
        )

    def test_language_is_labelled(self):
        entry = repo_map.extract_ts(self.write("angular/a.ts", self.SOURCE))
        self.assertEqual(entry["language"], "typescript")


class ExtractCsprojRefsTests(_TempRepo):
    def test_resolves_a_relative_project_reference(self):
        self.write("src/Domain/Domain.csproj", "<Project />")
        proj = self.write(
            "src/Application/Application.csproj",
            '<Project><ItemGroup>'
            '<ProjectReference Include="..\\\\Domain\\\\Domain.csproj" />'
            "</ItemGroup></Project>",
        )
        self.assertEqual(
            repo_map.extract_csproj_refs(proj), ["src/Domain/Domain.csproj"]
        )

    def test_returns_empty_for_a_project_with_no_references(self):
        proj = self.write("src/A/A.csproj", "<Project />")
        self.assertEqual(repo_map.extract_csproj_refs(proj), [])

    def test_an_unreadable_project_yields_no_references_rather_than_raising(self):
        p = self.root / "bad.csproj"
        p.write_bytes(bytes([0xFF, 0xFE]))
        self.assertEqual(repo_map.extract_csproj_refs(p), [])


class NamespaceMapTests(unittest.TestCase):
    def test_groups_files_by_namespace(self):
        entries = [
            {"file": "a.cs", "namespace": "N1"},
            {"file": "b.cs", "namespace": "N1"},
            {"file": "c.cs", "namespace": "N2"},
        ]
        result = repo_map.build_namespace_to_files(entries)
        self.assertEqual(sorted(result["N1"]), ["a.cs", "b.cs"])
        self.assertEqual(result["N2"], ["c.cs"])

    def test_skips_files_with_no_namespace(self):
        result = repo_map.build_namespace_to_files([{"file": "a.cs", "namespace": None}])
        self.assertEqual(dict(result), {})


class RankCsFilesTests(unittest.TestCase):
    def test_a_used_namespace_credits_its_defining_file(self):
        entries = [
            {"file": "defs.cs", "namespace": "N1", "uses_namespaces": []},
            {"file": "u1.cs", "namespace": "N2", "uses_namespaces": ["N1"]},
            {"file": "u2.cs", "namespace": "N3", "uses_namespaces": ["N1"]},
        ]
        self.assertEqual(repo_map.rank_cs_files(entries)["defs.cs"], 2)

    def test_a_file_does_not_credit_itself(self):
        # The guard this asserts is `if defining_file != entry["file"]`. Without
        # a fixture where a file uses its OWN namespace, deleting that guard
        # would not change any result -- so the case is constructed explicitly.
        entries = [{"file": "self.cs", "namespace": "N1", "uses_namespaces": ["N1"]}]
        self.assertEqual(repo_map.rank_cs_files(entries), Counter())

    def test_an_unknown_namespace_credits_nobody(self):
        entries = [{"file": "a.cs", "namespace": "N1", "uses_namespaces": ["Nowhere"]}]
        self.assertEqual(repo_map.rank_cs_files(entries), Counter())


class RankTsFilesTests(_TempRepo):
    def test_counts_relative_imports_against_the_imported_file(self):
        entries = [
            {"file": "app/target.ts", "imports": []},
            {"file": "app/one.ts", "imports": ["./target"]},
            {"file": "app/two.ts", "imports": ["./target"]},
        ]
        self.assertEqual(repo_map.rank_ts_files(entries)["app/target.ts"], 2)

    def test_ignores_package_imports(self):
        entries = [
            {"file": "app/a.ts", "imports": ["@angular/core", "rxjs"]},
        ]
        self.assertEqual(repo_map.rank_ts_files(entries), Counter())

    def test_resolves_a_directory_import_to_its_index(self):
        entries = [
            {"file": "app/shared/index.ts", "imports": []},
            {"file": "app/a.ts", "imports": ["./shared"]},
        ]
        self.assertEqual(repo_map.rank_ts_files(entries)["app/shared/index.ts"], 1)

    def test_an_import_of_an_unknown_file_counts_nothing(self):
        entries = [{"file": "app/a.ts", "imports": ["./not-in-the-map"]}]
        self.assertEqual(repo_map.rank_ts_files(entries), Counter())


class ResolveRelativeImportTests(_TempRepo):
    def test_prefers_a_ts_file_over_a_directory_index(self):
        by_file = {"app/thing.ts": {}, "app/thing/index.ts": {}}
        result = repo_map._resolve_relative_ts_import(
            self.root / "app", "./thing", by_file
        )
        self.assertEqual(result, "app/thing.ts")

    def test_returns_none_when_nothing_matches(self):
        self.assertIsNone(
            repo_map._resolve_relative_ts_import(self.root / "app", "./ghost", {})
        )

    def test_an_import_escaping_the_repo_root_is_ignored(self):
        # rel() raises ValueError outside REPO_ROOT; the resolver must skip the
        # candidate rather than propagate it.
        self.assertIsNone(
            repo_map._resolve_relative_ts_import(
                self.root, "../../outside", {"x": {}}
            )
        )


class TopNTests(unittest.TestCase):
    def test_returns_the_highest_counts_first(self):
        c = Counter({"a": 5, "b": 9, "c": 1})
        self.assertEqual(repo_map.top_n(c, 2), [("b", 9), ("a", 5)])

    def test_asking_for_more_than_exists_returns_what_there_is(self):
        self.assertEqual(len(repo_map.top_n(Counter({"a": 1}), 10)), 1)

    def test_an_empty_counter_yields_nothing(self):
        self.assertEqual(repo_map.top_n(Counter(), 5), [])


def _sample_data():
    """A complete `data` dict, with every section populated.

    Deliberately NOT minimal. render_map_md builds each section in a loop, so an
    empty list still renders a valid heading and an empty table -- every loop
    body could be deleted and a minimal fixture would still pass. Each list here
    carries at least one row so the body is exercised.
    """
    return {
        "generated_at": "2026-09-11T00:00:00Z",
        "stacks": [{"name": "dotnet", "files": 42, "note": "backend"}],
        # `name` is the repo-relative .csproj PATH, not a bare project name --
        # that is what main() stores, and it matters: Path(...).stem strips the
        # last dot-suffix, so a bare "A.B.Application" would lose ".Application"
        # and collapse several projects onto one mermaid node. It does not,
        # because ".csproj" is what gets stripped. Checked against the real
        # docs/repo-map/map.md rather than assumed.
        "projects": [
            {
                "name": (
                    "src/HealthcareSupport.CaseEvaluation.Application/"
                    "HealthcareSupport.CaseEvaluation.Application.csproj"
                ),
                "references": [
                    "src/HealthcareSupport.CaseEvaluation.Domain/"
                    "HealthcareSupport.CaseEvaluation.Domain.csproj"
                ],
            },
            {
                "name": (
                    "src/HealthcareSupport.CaseEvaluation.Domain/"
                    "HealthcareSupport.CaseEvaluation.Domain.csproj"
                ),
                "references": [],
            },
        ],
        "top_cs_files": [("src/Domain/Appointment.cs", 31)],
        "top_ts_files": [("angular/src/app/app.component.ts", 12)],
        "stats": {
            "cs_files": 100,
            "cs_symbols": 200,
            "ts_files": 300,
            "ts_symbols": 400,
            "projects": 2,
        },
        "commands": {"build": "dotnet build"},
    }


class RenderMapMdTests(unittest.TestCase):
    """The generated document `verify_structure.py` later checks for freshness."""

    def setUp(self):
        self.out = repo_map.render_map_md(_sample_data())

    def test_starts_with_the_breadcrumb_and_title(self):
        self.assertTrue(self.out.startswith("[Home](../INDEX.md) > Repository Map"))
        self.assertIn("# Repository Map", self.out)

    def test_carries_the_generation_timestamp(self):
        self.assertIn("2026-09-11T00:00:00Z", self.out)

    def test_renders_every_section_heading(self):
        for heading in (
            "## Stacks Detected",
            "## .NET Projects",
            "## Project Dependency Graph",
            "## Top 15 Most-Referenced C# Files",
            "## Top 15 Most-Imported Angular Files",
            "## Summary Statistics",
            "## Commands",
        ):
            with self.subTest(heading=heading):
                self.assertIn(heading, self.out)

    def test_renders_a_stack_row(self):
        self.assertIn("- **dotnet** (42 files) -- backend", self.out)

    def test_project_references_are_shown_by_stem(self):
        # The reference column drops the path and the .csproj suffix...
        self.assertIn("| HealthcareSupport.CaseEvaluation.Domain |", self.out)
        # ...while the Project column keeps the full path it was given.
        self.assertIn("HealthcareSupport.CaseEvaluation.Application.csproj", self.out)

    def test_the_last_name_segment_survives_stem(self):
        # Path.stem strips only the final suffix, so ".Application" is kept and
        # ".csproj" is dropped. If `name` were ever changed to a bare project
        # name, every project would collapse onto one mermaid node.
        self.assertIn("HealthcareSupport_CaseEvaluation_Application", self.out)

    def test_a_project_with_no_references_renders_none_not_an_empty_cell(self):
        self.assertIn("(none)", self.out)

    def test_the_mermaid_graph_has_an_edge_with_dots_replaced(self):
        # Mermaid node ids cannot contain dots, so the stem is sanitised.
        self.assertIn("```mermaid", self.out)
        self.assertIn(
            "HealthcareSupport_CaseEvaluation_Application --> "
            "HealthcareSupport_CaseEvaluation_Domain",
            self.out,
        )

    def test_ranked_files_are_numbered_from_one(self):
        self.assertIn("| 1 | 31 | `src/Domain/Appointment.cs` |", self.out)
        self.assertIn("| 1 | 12 | `angular/src/app/app.component.ts` |", self.out)

    def test_every_summary_statistic_appears(self):
        for label, value in (
            ("C# files scanned", 100),
            ("C# public symbols", 200),
            ("TypeScript files scanned", 300),
            ("TypeScript exported symbols", 400),
            (".NET projects", 2),
        ):
            with self.subTest(label=label):
                self.assertIn("- %s: **%d**" % (label, value), self.out)

    def test_commands_are_rendered(self):
        self.assertIn("- **build:** `dotnet build`", self.out)

    def test_output_is_a_single_newline_joined_string(self):
        self.assertNotIn(chr(13), self.out)
        self.assertIsInstance(self.out, str)


if __name__ == "__main__":
    unittest.main()
