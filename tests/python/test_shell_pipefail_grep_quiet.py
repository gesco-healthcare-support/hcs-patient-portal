"""No pipefail script may pipe into a quiet grep.

Under `set -o pipefail` a pipeline fails when ANY stage fails. `grep -q` exits as soon as it finds a match, so a
writer that is still writing then hits a closed pipe and dies of SIGPIPE, and the pipeline reports failure even
though grep found what it was looking for. bash's own printf writes line by line, which makes this a real race
rather than a theoretical one: on 2026-10-23 `printf '%s\\n' "$sans" | grep -qxF -- "$n"` in renew-tls-cert.sh
reported a name the certificate did cover as missing, failing CI, and in a reproduction under CPU contention it
gave 1 false miss in 1,500 runs. On the server the same line would fail a real renewal and send a false alert.

The fix is a here-string (`grep -q ... <<< "$text"`), which has no pipe. This test keeps the pattern from coming
back in any tracked script that turns pipefail on.
"""

import pathlib
import re
import subprocess
import unittest

REPO = pathlib.Path(__file__).resolve().parents[2]

# A set line that turns pipefail on, e.g. `set -euo pipefail` or `set -o pipefail`.
PIPEFAIL = re.compile(r"^\s*set\s+(-[A-Za-z]*\s+)*-[A-Za-z]*o\s+pipefail\b|^\s*set\s+-o\s+pipefail\b", re.MULTILINE)

# A pipe into grep whose options make it quiet: -q anywhere in a short-option cluster, or --quiet / --silent.
PIPE_INTO_QUIET_GREP = re.compile(
    r"\|\s*grep\b(?:\s+(?:-[A-Za-z]+|--[a-z-]+))*?\s+(?:-[A-Za-z]*q[A-Za-z]*\b|--quiet\b|--silent\b)"
)


def code_lines(text):
    """(line number, line) for every line that is not a whole-line comment."""
    for number, line in enumerate(text.splitlines(), start=1):
        if not line.lstrip().startswith("#"):
            yield number, line


def offending_lines(text):
    """The lines of a script that pipe into a quiet grep, or none when the script never turns pipefail on."""
    if not PIPEFAIL.search(text):
        return []
    return [(number, line.strip()) for number, line in code_lines(text) if PIPE_INTO_QUIET_GREP.search(line)]


def tracked_shell_scripts():
    out = subprocess.run(
        ["git", "ls-files", "-z", "--", "*.sh"],
        cwd=REPO,
        capture_output=True,
        check=True,
    ).stdout.decode("utf-8")
    return [REPO / name for name in out.split("\0") if name]


class TheDetectorItself(unittest.TestCase):
    """Known positives and negatives, so a regex that matches nothing cannot pass the repository scan by accident."""

    def test_the_line_that_failed_ci_is_caught(self):
        script = "set -euo pipefail\n    printf '%s\\n' \"$sans\" | grep -qxF -- \"$n\" || out+=x\n"
        self.assertEqual([2], [number for number, _ in offending_lines(script)])

    def test_other_quiet_spellings_are_caught(self):
        for line in ('echo "$x" | grep -q foo', 'echo "$x" | grep -Fq foo', 'echo "$x" | grep -E -q foo',
                     'echo "$x" | grep --quiet foo', 'docker ps | grep -i --silent web'):
            with self.subTest(line=line):
                self.assertEqual(1, len(offending_lines("set -o pipefail\n" + line + "\n")))

    def test_a_here_string_and_a_loud_grep_are_allowed(self):
        script = ("set -euo pipefail\n"
                  "grep -qxF -- \"$n\" <<< \"$sans\" || true\n"
                  "printf '%s' \"$meta\" | grep -o '\"k\": *\"[^\"]*\"' | head -1\n"
                  "grep -q foo \"$file\"\n")
        self.assertEqual([], offending_lines(script))

    def test_comments_and_scripts_without_pipefail_are_not_flagged(self):
        self.assertEqual([], offending_lines("set -euo pipefail\n# printf x | grep -q y\n"))
        self.assertEqual([], offending_lines("set -eu\nprintf x | grep -q y\n"))


class NoTrackedScriptPipesIntoAQuietGrep(unittest.TestCase):

    def test_no_pipefail_script_pipes_into_a_quiet_grep(self):
        scripts = tracked_shell_scripts()
        self.assertTrue(any(p.name == "renew-tls-cert.sh" for p in scripts),
                        "the scan found no renew-tls-cert.sh, so it is not looking at the repository")
        found = []
        for path in scripts:
            text = path.read_text(encoding="utf-8", errors="replace")
            for number, line in offending_lines(text):
                found.append(f"{path.relative_to(REPO).as_posix()}:{number}: {line}")
        self.assertEqual([], found,
                         "Under pipefail, a pipe into grep -q can fail after grep has found its match (the writer "
                         "dies of SIGPIPE). Use a here-string instead: grep -q PATTERN <<< \"$text\".\n"
                         + "\n".join(found))


if __name__ == "__main__":
    unittest.main()
