"""Tests for .claude/scripts/check-claude-md.py.

This script gates the per-folder CLAUDE.md files in CI. Those files are read
FIRST and trusted MOST by AI agents, so a stale one is more dangerous than
stale prose, not less: an agent reads code correctly, but cannot notice that a
file is lying to it.

The single most important test in here is NonEmptyAssertionTests. On its first
real run the script reported 181 findings, of which a third were bugs in the
script itself rather than stale documentation. The failure mode that would have
been far worse is the silent one -- a walk that matches nothing and reports a
clean repository. That is what the non-empty assertion exists to prevent, and
what the test pins.

Each test below was written against code where the rule did not yet hold, and
was seen to fail naming its rule, before the rule was made to pass.
"""

import os
import time
import unittest

from gate_loader import load

check_claude_md = load("check_claude_md", ".claude/scripts/check-claude-md.py")


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as handle:
        handle.write(text)


def kinds(findings):
    return [kind for _rel, kind, _detail in findings]


def details(findings):
    return " ".join(detail for _rel, _kind, detail in findings)


class TempRepo(unittest.TestCase):
    """A throwaway tree, because every rule is about a file's relationship to its siblings."""

    def setUp(self):
        import tempfile

        self._tmp = tempfile.TemporaryDirectory()
        self.root = self._tmp.name
        self.addCleanup(self._tmp.cleanup)

    def check(self, folder_rel, doc_text, sources=None):
        folder = os.path.join(self.root, folder_rel)
        os.makedirs(folder, exist_ok=True)
        for name, body in (sources or {}).items():
            write(os.path.join(folder, name), body)
        doc = os.path.join(folder, "CLAUDE.md")
        write(doc, doc_text)
        index = check_claude_md.build_index(self.root)
        return check_claude_md.check(doc, self.root, index)


class OrphanTests(TempRepo):
    """A CLAUDE.md with no code beside it describes nothing."""

    def test_folder_with_no_source_is_an_orphan(self):
        findings = self.check("Books", "# Books\n\nDescribes the book feature.\n")
        self.assertEqual(kinds(findings), ["ORPHAN"])

    def test_folder_with_a_source_file_is_not_an_orphan(self):
        findings = self.check(
            "Real", "# Real\n\nProse only.\n", {"Thing.cs": "// nothing public\n"}
        )
        self.assertNotIn("ORPHAN", kinds(findings))

    def test_orphan_suppresses_the_other_rules(self):
        """With no code to compare against, further findings would be noise."""
        findings = self.check("Books", "# Books\n\nSee `Nope.cs` for details.\n")
        self.assertEqual(kinds(findings), ["ORPHAN"])


class DanglingTests(TempRepo):
    def test_names_a_file_that_does_not_exist(self):
        findings = self.check(
            "Feature", "# Feature\n\nSee `Gone.cs`.\n", {"Here.cs": "// x\n"}
        )
        self.assertIn("DANGLING", kinds(findings))
        self.assertIn("Gone.cs", details(findings))

    def test_a_file_that_exists_is_not_flagged(self):
        findings = self.check(
            "Feature", "# Feature\n\nSee `Here.cs`.\n", {"Here.cs": "// x\n"}
        )
        self.assertNotIn("DANGLING", kinds(findings))

    def test_globs_and_placeholders_are_not_paths(self):
        """`{entity}.component.ts` and `*.Partial.cs` are patterns, not claims."""
        doc = "# F\n\nSee `{entity}.component.ts` and `*.Partial.cs` and `<feature>/`.\n"
        findings = self.check("Feature", doc, {"Here.cs": "// x\n"})
        self.assertNotIn("DANGLING", kinds(findings))

    def test_a_bare_extension_is_not_a_path(self):
        findings = self.check(
            "Feature", "# F\n\nThe `.html` body files.\n", {"Here.cs": "// x\n"}
        )
        self.assertNotIn("DANGLING", kinds(findings))

    def test_full_paths_are_required_so_shorthand_is_a_finding(self):
        """Adrian's decision 2026-09-28: `Domain/`-style shorthand is not accepted."""
        findings = self.check(
            "Feature", "# F\n\nLives under `Domain/`.\n", {"Here.cs": "// x\n"}
        )
        self.assertIn("DANGLING", kinds(findings))
        self.assertIn("Domain/", details(findings))


class OmissionTests(TempRepo):
    def test_a_public_type_never_mentioned_is_flagged(self):
        findings = self.check(
            "Feature", "# Feature\n\nProse.\n", {"Widget.cs": "public class Widget { }\n"}
        )
        self.assertIn("OMISSION", kinds(findings))
        self.assertIn("Widget", details(findings))

    def test_a_mentioned_type_is_not_flagged(self):
        findings = self.check(
            "Feature",
            "# Feature\n\n`Widget` does the thing.\n",
            {"Widget.cs": "public class Widget { }\n"},
        )
        self.assertNotIn("OMISSION", kinds(findings))

    def test_the_opt_out_heading_excludes_a_type(self):
        """Opting out must be a visible edit in the diff, not silence."""
        doc = "# Feature\n\nProse.\n\n## Not documented here\n\n- `Widget`\n"
        findings = self.check("Feature", doc, {"Widget.cs": "public class Widget { }\n"})
        self.assertNotIn("OMISSION", kinds(findings))

    def test_generated_mapper_types_are_not_demanded(self):
        """One real mappers file declares ~100 types; demanding prose for each is noise."""
        findings = self.check(
            "Feature",
            "# Feature\n\nProse.\n",
            {"ThingMappers.cs": "public class AToBMapper { }\n", "Real.cs": "// x\n"},
        )
        self.assertNotIn("OMISSION", kinds(findings))

    def test_records_and_interfaces_count_as_public_types(self):
        findings = self.check(
            "Feature",
            "# Feature\n\nProse.\n",
            {"Shapes.cs": "public interface IThing { }\npublic sealed record Pair(int A);\n"},
        )
        self.assertIn("IThing", details(findings))
        self.assertIn("Pair", details(findings))


class RestatementTests(TempRepo):
    def test_a_copied_method_body_is_flagged(self):
        doc = (
            "# Feature\n\n`Widget` does it.\n\n```csharp\n"
            "public void Go() {\n    DoThing();\n}\n```\n"
        )
        findings = self.check("Feature", doc, {"Widget.cs": "public class Widget { }\n"})
        self.assertIn("RESTATEMENT", kinds(findings))

    def test_a_short_illustrative_fence_is_not_flagged(self):
        doc = "# Feature\n\n`Widget` does it.\n\n```csharp\nvar x = Widget.Default;\n```\n"
        findings = self.check("Feature", doc, {"Widget.cs": "public class Widget { }\n"})
        self.assertNotIn("RESTATEMENT", kinds(findings))

    def test_a_shell_fence_is_never_a_restatement(self):
        doc = "# Feature\n\n`Widget`.\n\n```bash\npublic() { echo hi; }\n```\n"
        findings = self.check("Feature", doc, {"Widget.cs": "public class Widget { }\n"})
        self.assertNotIn("RESTATEMENT", kinds(findings))


class MethodBodyPatternTests(unittest.TestCase):
    """The Rule 4 pattern must stay linear in its input and keep what it accepts.

    An earlier form let two quantifiers match the same whitespace, so a keyword followed by
    a long run of spaces and no brace made the engine try every split point: 1.2 s at 32,000
    characters, growing quadratically. The bound below is far above the linear form's time
    and far below the quadratic form's, so it separates them without being timing-sensitive.
    """

    def test_a_long_whitespace_run_is_rejected_in_linear_time(self):
        text = "public" + " " * 100_000 + "x"
        started = time.perf_counter()
        match = check_claude_md.METHOD_BODY.search(text)
        elapsed = time.perf_counter() - started
        self.assertIsNone(match)
        self.assertLess(elapsed, 1.0, f"METHOD_BODY took {elapsed:.2f}s on 100,000 spaces")

    def test_a_signature_with_a_brace_on_the_same_line_matches(self):
        self.assertIsNotNone(check_claude_md.METHOD_BODY.search("public void Go() {"))

    def test_blank_lines_between_keyword_and_signature_still_match(self):
        self.assertIsNotNone(check_claude_md.METHOD_BODY.search("public\n\n  void Go() {"))

    def test_a_signature_without_a_brace_does_not_match(self):
        self.assertIsNone(check_claude_md.METHOD_BODY.search("public void Go()"))

    def test_a_brace_on_the_next_line_after_the_signature_does_not_match(self):
        self.assertIsNone(check_claude_md.METHOD_BODY.search("public void Go()\n{"))


class NonEmptyAssertionTests(unittest.TestCase):
    """THE load-bearing test. A checker that examines nothing must not report clean.

    Without this, a broken walk, a renamed directory or a bad path argument all
    produce the most dangerous possible output: exit 0 over an empty file list,
    indistinguishable from a healthy repository.
    """

    def test_a_tree_with_no_claude_md_fails_rather_than_passing(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            write(os.path.join(tmp, "src", "Thing.cs"), "public class Thing { }\n")
            exit_code = check_claude_md.main(["check-claude-md.py", tmp])
        self.assertEqual(exit_code, 1, "an empty scan must fail, never report clean")

    def test_a_missing_directory_is_a_usage_error_not_a_pass(self):
        exit_code = check_claude_md.main(
            ["check-claude-md.py", os.path.join("no", "such", "dir")]
        )
        self.assertEqual(exit_code, 2)


class CountTests(TempRepo):
    """A count with no command beside it is a claim that ages invisibly.

    Adrian's rule: a number carries the command that produces it, or it is not a
    number anyone can re-check. This rule was dropped from the first build because
    an imprecise version cries wolf, and a gate that cries wolf gets skipped --
    which is the same argument that kept the checker blocking rather than advisory.

    So the bar here is precision, and most of these tests exist to prove it stays
    QUIET rather than to prove it fires.
    """

    SRC = {"Widget.cs": "public class Widget { }\n"}

    def doc(self, body):
        return "# Feature\n\n`Widget` does it.\n\n" + body + "\n"

    def test_a_count_attached_to_a_repo_noun_is_flagged(self):
        findings = self.check("Feature", self.doc("There are 45 entities in this folder."), self.SRC)
        self.assertIn("COUNT", kinds(findings))
        self.assertIn("45 entities", details(findings))

    def test_each_countable_noun_is_recognised(self):
        for noun in ("files", "tests", "endpoints", "entities", "permissions",
                     "jobs", "projects", "migrations", "services", "roles"):
            with self.subTest(noun=noun):
                findings = self.check(
                    "Feature", self.doc("We ship 12 %s here." % noun), self.SRC
                )
                self.assertIn("COUNT", kinds(findings), noun)

    def test_a_count_with_a_command_on_the_same_line_is_quiet(self):
        body = "There are 45 entities (`grep -rc IMultiTenant src | wc -l`)."
        findings = self.check("Feature", self.doc(body), self.SRC)
        self.assertNotIn("COUNT", kinds(findings))

    def test_a_count_with_a_command_on_a_nearby_line_is_quiet(self):
        body = "There are 45 entities.\n\nMeasured with:\n\n```bash\ngrep -rc IMultiTenant src\n```"
        findings = self.check("Feature", self.doc(body), self.SRC)
        self.assertNotIn("COUNT", kinds(findings))

    def test_the_per_line_opt_out_silences_one_line_only(self):
        body = (
            "There are 45 entities. <!-- count-ok: stated by Adrian, not derivable -->\n\n"
            "There are 12 jobs."
        )
        findings = self.check("Feature", self.doc(body), self.SRC)
        counts = [d for _r, k, d in findings if k == "COUNT"]
        self.assertEqual(len(counts), 1, counts)
        self.assertIn("12 jobs", counts[0])

    def test_a_number_not_attached_to_a_countable_noun_is_quiet(self):
        """Versions, ports, dates, line numbers and prose numbers are not counts."""
        for body in ("Runs on port 8080.", "ABP 10.0.2 is the version.",
                     "See `Widget.cs:45` for the guard.", "Added 2026-05-15.",
                     "Max length is 63 characters.", "Cron is 08:15 PT daily."):
            with self.subTest(body=body):
                findings = self.check("Feature", self.doc(body), self.SRC)
                self.assertNotIn("COUNT", kinds(findings), body)

    def test_a_backticked_FILENAME_does_not_silence_a_count(self):
        """The defect that made this rule useless on its first run.

        All three real count claims in the repository sit in table rows next to a
        backticked filename, and an early version read any inline span as "the
        command backing this count". It was therefore silent on 3 of 3 genuine
        cases -- worse than absent, because silence reads as a clean result.
        """
        body = "| `AppointmentEmployerDetailsAppService.cs` -- 8 methods; mixed auth |"
        findings = self.check("Feature", self.doc(body), self.SRC)
        self.assertIn("COUNT", kinds(findings))

    def test_a_backticked_route_or_identifier_does_not_silence_a_count(self):
        body = "| Manual controller `api/app/doctor-availabilities`, 11 routes |"
        findings = self.check("Feature", self.doc(body), self.SRC)
        self.assertIn("COUNT", kinds(findings))

    def test_a_real_command_still_silences_it(self):
        """The distinction is a command with arguments, not any backticked span."""
        for command in ("`grep -rc IMultiTenant src | wc -l`",
                        "`ls docs/decisions/*.md | wc -l`",
                        "`git grep -c TODO`"):
            with self.subTest(command=command):
                findings = self.check(
                    "Feature", self.doc("There are 45 entities (%s)." % command), self.SRC
                )
                self.assertNotIn("COUNT", kinds(findings), command)

    def test_a_count_inside_a_fence_is_quiet(self):
        """Sample output is not a claim the document is making."""
        body = "```text\nScanned 49 files, 3 findings\n```"
        findings = self.check("Feature", self.doc(body), self.SRC)
        self.assertNotIn("COUNT", kinds(findings))


class MainReportingTests(TempRepo):
    """The report itself is code, and a report that cannot be read is a gate nobody uses."""

    def _run_main(self):
        import contextlib
        import io as _io

        buffer = _io.StringIO()
        with contextlib.redirect_stdout(buffer):
            exit_code = check_claude_md.main(["check-claude-md.py", self.root])
        return exit_code, buffer.getvalue()

    def test_a_clean_tree_reports_ok_and_exits_zero(self):
        self.check(
            "Feature",
            "# Feature\n\n`Widget` does the thing.\n",
            {"Widget.cs": "public class Widget { }\n"},
        )
        exit_code, output = self._run_main()
        self.assertEqual(exit_code, 0)
        self.assertIn("OK", output)
        self.assertIn("Scanned 1", output)

    def test_findings_are_grouped_under_their_file_and_exit_one(self):
        self.check(
            "Feature", "# Feature\n\nSee `Gone.cs`.\n", {"Widget.cs": "public class Widget { }\n"}
        )
        exit_code, output = self._run_main()
        self.assertEqual(exit_code, 1)
        self.assertIn("finding(s)", output)
        self.assertIn("DANGLING", output)
        self.assertIn("OMISSION", output)
        self.assertIn("Feature/CLAUDE.md", output.replace("\\", "/"))

    def test_the_report_names_the_token_so_the_fix_is_obvious(self):
        self.check("Feature", "# F\n\nSee `Gone.cs`.\n", {"Widget.cs": "// x\n"})
        _exit_code, output = self._run_main()
        self.assertIn("Gone.cs", output)

    def test_defaults_to_the_working_directory_when_no_argument_is_given(self):
        original = os.getcwd()
        os.chdir(self.root)
        try:
            self.check("Feature", "# F\n\nProse.\n", {"Widget.cs": "// x\n"})
            exit_code = check_claude_md.main(["check-claude-md.py"])
        finally:
            os.chdir(original)
        self.assertIn(exit_code, (0, 1))


class GitignoredPathTests(TempRepo):
    """A CLAUDE.md SHOULD tell you to create a gitignored file; flagging that punishes it."""

    def test_is_ignored_is_false_outside_a_git_repo_and_does_not_raise(self):
        self.assertFalse(
            check_claude_md.is_ignored(os.path.join(self.root, "whatever.json"), self.root)
        )

    def test_a_gitignored_file_is_not_reported_as_dangling(self):
        import subprocess

        try:
            subprocess.run(
                ["git", "init", "-q"], cwd=self.root, check=True,
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            )
        except (OSError, subprocess.SubprocessError):  # pragma: no cover
            self.skipTest("git unavailable")
        write(os.path.join(self.root, ".gitignore"), "appsettings.Local.json\n")
        findings = self.check(
            "Feature",
            "# F\n\nCopy `appsettings.Local.json` into place.\n",
            {"Here.cs": "// x\n"},
        )
        self.assertNotIn("DANGLING", kinds(findings))


class IndexTests(TempRepo):
    """The index must record generated directories, which are skipped for SCANNING only.

    An early version used one list for both, so a CLAUDE.md that correctly named
    Migrations/ or proxy/ was reported as dangling.
    """

    def test_a_generated_directory_still_resolves(self):
        os.makedirs(os.path.join(self.root, "Feature", "Migrations"), exist_ok=True)
        findings = self.check(
            "Feature", "# F\n\nSee `Migrations/`.\n", {"Here.cs": "// x\n"}
        )
        self.assertNotIn("DANGLING", kinds(findings))

    def test_build_output_is_not_walked(self):
        write(os.path.join(self.root, "obj", "CLAUDE.md"), "# junk\n")
        found = check_claude_md.find_claude_files(self.root)
        self.assertEqual(found, [])


if __name__ == "__main__":  # pragma: no cover
    unittest.main()
