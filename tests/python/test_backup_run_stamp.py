"""The off-box backup and the database dump must agree on ONE run stamp.

backup-offbox.sh computes a stamp, runs backup-databases.sh, then ships the dumps whose names carry its
stamp. backup-databases.sh used to compute its OWN stamp. The two are a moment apart, so a run that
straddled a second boundary named the dumps with one second and searched for the other: "no dumps matched
this run's stamp -- nothing shipped", and the night's backup failed. It worked only because the two calls
usually land in the same second.

The fix: the off-box script exports its stamp, and the dump script uses a stamp it is given -- after
checking it, because the stamp is written into a SQL statement.

These tests run the REAL scripts against a fake `docker`, `ssh` and `scp`, and a fake `date` that moves on
one second every time it is asked for a stamp, so the boundary is crossed on every run instead of
occasionally. Nothing real is touched: no container, no SQL Server, no network, no backup host.

They need a POSIX bash. CI runs on Linux. On Windows set TEST_BASH to Git Bash's plain shell,
C:/Program Files/Git/usr/bin/bash.exe, or the behavioural tests are skipped. NOT bin/bash.exe: that
launcher puts /usr/bin in front of PATH, so the real `date` wins over the fake and the race never happens.
"""

import os
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
HOSTING = REPO_ROOT / "scripts" / "hosting"
OFFBOX = HOSTING / "backup-offbox.sh"
DATABASES = HOSTING / "backup-databases.sh"
FIRST_STAMP = "20261013-010000"  # what the fake clock answers first; the off-box run takes it
SEP = "\x1f"

FAKE_DOCKER = r"""#!/usr/bin/env bash
{ printf '%s\x1f' "$@"; printf '\n'; } >> "$FAKE_LOG"
all="$*"
if [[ "$1" == compose ]]; then
  if [[ "$all" == *sys.databases* ]]; then printf 'CaseEvaluation\nCaseEvaluation_synthoffice\n'; exit 0; fi
  re="BACKUP DATABASE \[[^]]*\] TO DISK = N'/var/opt/mssql/backups/([^']+)'"
  if [[ "$all" =~ $re ]]; then printf 'synthetic dump' > "$FAKE_BACKUP_DIR/${BASH_REMATCH[1]}"; fi
fi
exit 0
"""

# Answers a stamp request with the next second each time; anything else goes to the real date.
FAKE_DATE = r"""#!/usr/bin/env bash
if [[ "$*" == "+%Y%m%d-%H%M%S" ]]; then
  n="$(cat "$FAKE_CLOCK" 2>/dev/null || echo 0)"
  echo $((n + 1)) > "$FAKE_CLOCK"
  printf '20261013-0100%02d\n' "$n"
  exit 0
fi
exec /usr/bin/date "$@"
"""

# scp <opts...> <src> <user@host:REMOTE_DIR/sub/name>: copy into the fake remote directory.
FAKE_SCP = r"""#!/usr/bin/env bash
src="${@: -2:1}"; dst="${@: -1}"; path="${dst#*:}"; rel="${path#C:/PortalBackups/}"
mkdir -p "$(dirname "$FAKE_REMOTE/$rel")" && cp "$src" "$FAKE_REMOTE/$rel"
"""

# ssh: answers the size check from the fake remote directory, the prune with PRUNED, all else with 0.
FAKE_SSH = r"""#!/usr/bin/env bash
all="$*"
re="Get-Item '([^']+)'"
if [[ "$all" =~ $re ]]; then
  rel="${BASH_REMATCH[1]#C:/PortalBackups/}"
  [[ -f "$FAKE_REMOTE/$rel" ]] && wc -c < "$FAKE_REMOTE/$rel" | tr -d ' '
  exit 0
fi
[[ "$all" == *Remove-Item* ]] && echo PRUNED
exit 0
"""


def _find_bash():
    explicit = os.environ.get("TEST_BASH")
    if explicit:
        return explicit
    if sys.platform == "win32":
        return None  # PATH's bash on Windows may be WSL, which cannot see this filesystem the same way
    return shutil.which("bash")


BASH = _find_bash()


def shell_path(path):
    """A path as the shell under test should see it. Git Bash reads "C:/x" in a tar archive name as a
    remote host, so on Windows it gets /c/x; on Linux this is the path unchanged."""
    text = pathlib.Path(path).as_posix()
    if sys.platform == "win32" and len(text) > 1 and text[1] == ":":
        return "/" + text[0].lower() + text[2:]
    return text


def code_lines(path):
    return [line for line in path.read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.lstrip().startswith("#")]


class StaticTests(unittest.TestCase):
    def test_the_offbox_script_exports_its_stamp_before_it_runs_the_dump(self):
        lines = code_lines(OFFBOX)
        export = next((i for i, l in enumerate(lines) if re.search(r"^\s*export\s+STAMP\b", l)), None)
        dump = next(i for i, l in enumerate(lines) if "backup-databases.sh" in l and "bash" in l)
        self.assertIsNotNone(export, "backup-offbox.sh must export STAMP so the dump names its files with it")
        self.assertLess(export, dump, "STAMP must be exported before backup-databases.sh runs")


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class BehaviourTests(unittest.TestCase):
    def setUp(self):
        self.tmp = pathlib.Path(tempfile.mkdtemp(prefix="stamp-test-"))
        self.bin = self.tmp / "bin"
        self.bin.mkdir()
        for name, body in (("docker", FAKE_DOCKER), ("date", FAKE_DATE), ("scp", FAKE_SCP), ("ssh", FAKE_SSH)):
            path = self.bin / name
            path.write_text(body, encoding="utf-8", newline="\n")
            path.chmod(0o755)
        self.backups = self.tmp / "backups"
        self.staging = self.tmp / "staging"
        self.remote = self.tmp / "remote"
        for d in (self.backups, self.staging, self.remote):
            d.mkdir()
        self.env_file = self.tmp / "env.prod"
        self.env_file.write_text(
            "MSSQL_SA_PASSWORD=synthetic-sa\nMINIO_ROOT_USER=synthetic-user\nMINIO_ROOT_PASSWORD=synthetic-pass\n",
            encoding="utf-8", newline="\n")
        self.key = self.tmp / "key"
        self.key.write_text("not a real key\n", encoding="utf-8")
        self.log = self.tmp / "docker.log"

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def run_script(self, script, *args, **extra_env):
        env = dict(os.environ)
        env.pop("STAMP", None)
        env.update({
            "PATH": str(self.bin) + os.pathsep + env.get("PATH", ""),
            "REPO_DIR": shell_path(REPO_ROOT),
            "ENV_FILE": shell_path(self.env_file),
            "BACKUP_DIR": shell_path(self.backups),
            "STAGING": shell_path(self.staging),
            "SSH_KEY": shell_path(self.key),
            "REMOTE_HOST": "portalbackup@backup-host.invalid",
            "REMOTE_DIR": "C:/PortalBackups",
            "FAKE_LOG": shell_path(self.log),
            "FAKE_BACKUP_DIR": shell_path(self.backups),
            "FAKE_CLOCK": shell_path(self.tmp / "clock"),
            "FAKE_REMOTE": shell_path(self.remote),
        })
        env.update(extra_env)
        return subprocess.run([BASH, shell_path(script), *args], cwd=REPO_ROOT, env=env, capture_output=True,
                              text=True, encoding="utf-8", errors="replace", timeout=180)

    def backup_calls(self):
        if not self.log.exists():
            return []
        return [line for line in self.log.read_text(encoding="utf-8").splitlines() if "BACKUP DATABASE" in line]

    # ------------------------------------------------------------------ the race itself

    def test_an_offbox_run_that_crosses_a_second_boundary_still_ships_its_dumps(self):
        # The fake clock moves on a second per stamp request, so without the shared stamp the dump would
        # name its files 010001 while the off-box script looked for 010000.
        result = self.run_script(OFFBOX)
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        shipped = sorted(p.name for p in (self.remote / "db").iterdir())
        self.assertEqual(shipped, [f"CaseEvaluation_{FIRST_STAMP}.bak", f"CaseEvaluation_synthoffice_{FIRST_STAMP}.bak"])
        self.assertTrue((self.remote / "minio" / f"minio_{FIRST_STAMP}.tar.gz").is_file())
        self.assertTrue((self.staging / "last-success").exists())

    # ------------------------------------------------------------------ the dump script on its own

    def test_the_dump_uses_a_stamp_it_is_given(self):
        result = self.run_script(DATABASES, STAMP="20991231-235959")
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        self.assertEqual(sorted(p.name for p in self.backups.iterdir()),
                         ["CaseEvaluation_20991231-235959.bak", "CaseEvaluation_synthoffice_20991231-235959.bak"])

    def test_the_dump_still_stamps_its_own_files_when_run_alone(self):
        result = self.run_script(DATABASES)
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        self.assertEqual(sorted(p.name for p in self.backups.iterdir()),
                         [f"CaseEvaluation_{FIRST_STAMP}.bak", f"CaseEvaluation_synthoffice_{FIRST_STAMP}.bak"])

    def test_a_malformed_stamp_is_refused_before_any_backup_runs(self):
        # The stamp is written into a SQL statement and a file name, so only the exact shape is accepted.
        for bad in ("20261013", "x'; DROP DATABASE [CaseEvaluation]; --", "../../etc/20261013-010000"):
            with self.subTest(stamp=bad):
                result = self.run_script(DATABASES, STAMP=bad)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("STAMP", result.stderr)
                self.assertEqual(self.backup_calls(), [])
                self.assertEqual(list(self.backups.iterdir()), [])


if __name__ == "__main__":
    unittest.main()
