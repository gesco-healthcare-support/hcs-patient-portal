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
# CANNOT use that name, because its .NET step already writes `coverage.xml` at
# the repository root for `sonar.cs.vscoveragexml.reportsPaths`. One of the two
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

echo "python coverage report written to $OUT"
