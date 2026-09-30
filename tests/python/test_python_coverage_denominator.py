"""The Python coverage denominator must change visibly, never silently (#862).

#862's second acceptance criterion: WHEN the Python denominator changes shape,
THE SYSTEM SHALL make the change visible rather than silently absorbing it into
the percentage.

Absorbing it is what used to happen. The denominator held only the files the
suite IMPORTED, so it moved whenever an import appeared or vanished, and the
only symptom was a percentage that had shifted for no reason anyone could point
at. `FLOOR_PYTHON`'s own comment in ci.yml records a file-count tripwire being
REMOVED for exactly that reason -- calibrated at 2, left in place when the real
count became 4, and therefore actively misleading.

A count was the wrong instrument. This asserts the SET, so a change names the
file that joined or left instead of moving a number. The expected set is a
committed file, so a legitimate change lands in a diff and has to be accepted
deliberately -- the same shape as the authorization snapshot in #707.

WHY THE PATTERNS ARE NOT REIMPLEMENTED HERE. `omit` is glob-like but it is
coverage.py's own dialect, not fnmatch and not pathlib. Writing a second matcher
would put two implementations of one rule in the repository, and this repo has
already paid for that twice -- `.coverage-exclusions` carrying a hardcoded copy
in sonarcloud.yml, and two `avatarColor` hashes that disagree. So the real
`coverage.files.GlobMatcher` decides, seeded from the real pyproject.toml.
"""

import pathlib
import subprocess
import unittest

from coverage.files import GlobMatcher
import coverage

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
APPROVED = pathlib.Path(__file__).resolve().parent / "python-coverage-denominator.txt"


def tracked_python_files() -> list[str]:
    """Every tracked .py file, repo-relative with forward slashes.

    `git ls-files` rather than a filesystem walk, for the reason #683 gives: a
    walk also finds build output and anything a local run left behind, and those
    are precisely what must not enter a coverage denominator.
    """
    out = subprocess.run(
        ["git", "ls-files", "--", "*.py"],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=True,
    ).stdout
    return sorted(line.strip().replace("\\", "/") for line in out.splitlines() if line.strip())


def omit_matcher() -> GlobMatcher:
    """coverage.py's own matcher, configured from the real pyproject.toml."""
    cov = coverage.Coverage(config_file=str(REPO_ROOT / "pyproject.toml"))
    return GlobMatcher(cov.config.run_omit, "omit")


def expected_denominator() -> list[str]:
    matcher = omit_matcher()
    return [p for p in tracked_python_files() if not matcher.match(p)]


class DenominatorShapeTests(unittest.TestCase):
    def test_denominator_matches_the_approved_list(self):
        actual = expected_denominator()
        approved = [
            line.strip()
            for line in APPROVED.read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.startswith("#")
        ]

        added = sorted(set(actual) - set(approved))
        removed = sorted(set(approved) - set(actual))

        self.assertEqual(
            (added, removed),
            ([], []),
            "The Python coverage denominator changed shape.\n\n"
            "This is not necessarily wrong -- it is the gate asking you to confirm it was\n"
            "intended (#862). If these are right, update\n"
            f"  {APPROVED.relative_to(REPO_ROOT).as_posix()}\n"
            "in THIS pull request, and re-derive FLOOR_PYTHON from a fresh measurement\n"
            "rather than leaving the old number against a new denominator.\n\n"
            f"  joined the denominator: {added or '(none)'}\n"
            f"  left the denominator:   {removed or '(none)'}",
        )

    def test_the_shipped_packet_generators_are_in_the_denominator(self):
        """The four files whose absence #862 was filed about.

        They ship: `tools/packet-templates` is COPIED INTO the packet-renderer
        image and run at build time, which is why phase 8.1 ruled its 882 lines
        stay IN the denominator deliberately. Before #862 they were absent
        anyway, because nothing imports them -- the ruling said one thing and the
        measurement did another. If this ever fails, `include_namespace_packages`
        has been lost and the figure silently stopped counting shipped code.
        """
        actual = set(expected_denominator())

        for shipped in (
            "tools/packet-templates/attorney/build_attorney.py",
            "tools/packet-templates/doctor/build_doctor.py",
            "tools/packet-templates/patient/build_patient.py",
            "tools/packet-templates/shared/check_fields.py",
        ):
            self.assertIn(shipped, actual)

    def test_include_namespace_packages_is_enabled(self):
        """The single setting the whole fix rests on.

        Asserted directly rather than inferred from a percentage, because losing
        it produces no error -- only a smaller denominator and a figure that
        looks better than the truth.
        """
        cov = coverage.Coverage(config_file=str(REPO_ROOT / "pyproject.toml"))
        self.assertTrue(
            cov.config.include_namespace_packages,
            "include_namespace_packages is off; unexecuted .py files will vanish from "
            "the denominator and coverage will read higher than it is.",
        )

    def test_test_code_is_not_in_the_denominator(self):
        """Test files must not grade themselves.

        Both suites are excluded, and the second only became reachable once
        discovery started finding unexecuted files -- before that,
        tools/packet-templates/tests was omitted in `.coverage-exclusions` and
        NOT in pyproject, and nothing surfaced the disagreement.
        """
        actual = set(expected_denominator())

        self.assertNotIn("tests/python/test_python_coverage_denominator.py", actual)
        self.assertNotIn("tools/packet-templates/tests/test_golden_output.py", actual)

    def test_the_approved_list_is_not_empty(self):
        """Guards the guard.

        If `git ls-files` returned nothing, or every pattern suddenly matched,
        every assertion above would pass over an empty set and report success
        while checking nothing.
        """
        self.assertGreater(len(expected_denominator()), 5)


if __name__ == "__main__":
    unittest.main()
