#!/usr/bin/env bash
# Produce the Python coverage report. ONE DEFINITION, TWO CONSUMERS (#787).
#
# `Coverage: Floors` (ci.yml) gates on this report and SonarCloud grades new
# Python code from it. The issue that asked for both was explicit about why this
# file exists rather than the command being written out twice:
#
#   "Both consumers must read one generated artefact. Two separate
#    `coverage xml` invocations that could diverge in flags, exclusions or
#    working directory recreate that drift by hand."
#
# `.coverage-exclusions` has already drifted between two consumers once, and the
# symptom was a ten-point disagreement nobody noticed until the figure was
# decomposed. The same shape of bug is available here through a stray flag, so
# the flags live in one place.
#
# Usage: scripts/python-coverage.sh [output.xml]
#
# The output path is an ARGUMENT and not a constant, deliberately. ci.yml wants
# `coverage.xml`, which is also what its upload step publishes; sonarcloud.yml
# could NOT use that name, because its .NET step wrote `coverage.xml` at the
# repository root for `sonar.cs.vscoveragexml.reportsPaths`. (Since #1032 the
# SonarCloud job reuses CI's artifact instead of running this script; the
# argument stays so a second caller can never collide on the name again.) One of the two
# would silently overwrite the other's report, and the loser would be graded
# against the winner's figures.
set -euo pipefail

OUT="${1:-coverage.xml}"

# Configuration deliberately stays in pyproject.toml -- notably `source`, which
# decides whether emitted paths are repo-relative or silently stripped. Passing
# it here would let a local run and CI disagree about it.
python -m coverage run -m unittest discover -s tests/python -p "test_*.py" -v
python -m coverage report
python -m coverage xml -o "$OUT"

# Assert on the ARTEFACT, not on the exit code of the command meant to produce
# it -- the rule ci.yml already applies to its own copy of this step.
#
# `[[` rather than `test`: this file is #!/usr/bin/env bash, and shelldre:S7688
# is the rule #841 swept out of all eleven shell scripts. Writing `test -s` here
# reintroduced it in the same week -- caught by SonarCloud on this PR, which is
# the "introduced findings while fixing findings" trap rather than a new one.
[[ -s "$OUT" ]]

# And assert on the SHAPE of that artefact, not only that it exists (#862).
#
# The denominator used to hold only the files the suite IMPORTED, so it moved
# whenever an import appeared or vanished and the only symptom was a percentage
# that had shifted. `include_namespace_packages` fixes the cause; this asserts the
# effect, because losing that setting produces NO error -- just a smaller
# denominator and a figure that flatters itself.
#
# The expected list is committed at tests/python/python-coverage-denominator.txt
# and is the same file tests/python/test_python_coverage_denominator.py checks
# against `git ls-files` plus the omit patterns. One list, two consumers, which is
# the shape `.coverage-exclusions` already uses here -- and the shape that file
# records having got wrong once, when sonarcloud.yml carried a hardcoded copy.
python - "$OUT" <<'DENOM'
import pathlib, sys, xml.etree.ElementTree as ET

report = sys.argv[1]
approved_file = pathlib.Path("tests/python/python-coverage-denominator.txt")
approved = sorted(
    line.strip()
    for line in approved_file.read_text(encoding="utf-8").splitlines()
    if line.strip() and not line.startswith("#")
)
actual = sorted(
    {c.get("filename").replace("\\", "/") for c in ET.parse(report).iter("class")}
)

if actual != approved:
    joined = sorted(set(actual) - set(approved))
    left = sorted(set(approved) - set(actual))
    print("The Python coverage denominator does not match the approved list.", file=sys.stderr)
    print("  joined: " + (", ".join(joined) or "(none)"), file=sys.stderr)
    print("  left:   " + (", ".join(left) or "(none)"), file=sys.stderr)
    print("", file=sys.stderr)
    print("If intended, regenerate " + str(approved_file) + " in this pull request", file=sys.stderr)
    print("and re-derive FLOOR_PYTHON from a fresh measurement. If NOT intended, the", file=sys.stderr)
    print("likely cause is include_namespace_packages being lost from pyproject.toml.", file=sys.stderr)
    raise SystemExit(1)

print("python coverage denominator matches the approved list (%d files)" % len(actual))
DENOM

echo "python coverage report written to $OUT"
