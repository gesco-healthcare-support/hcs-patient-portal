"""deploy-release.sh takes its deploy branch and its backup unit from the environment.

The office server deploys from `development` and backs up with hcs-portal-backup.service, and both were written
into the script. The Azure VM backs up with hcs-portal-backup-azure.service, and may deploy from another branch,
so both became settings whose DEFAULTS are the office server's: the box that runs this script today needs no new
variable and behaves exactly as before.

Two things are pinned here:

  - the static half: neither value is written into the script's code anywhere except its default (and, for the
    unit, the allow-list), so a later edit cannot quietly re-hard-code one of them;
  - the behavioural half: a misspelled unit or a branch name carrying shell syntax STOPS the deploy before it
    touches git, the backup or Docker, and both the defaults and the Azure values get past that check.

The behavioural tests run the real script in an empty directory standing in for the checkout. It stops at its
next check, the missing BASE_DOMAIN, before any git, sudo, systemctl or docker command, so nothing real is
touched.

They need a POSIX bash. CI runs on Linux. On Windows set TEST_BASH to Git Bash's plain shell,
C:/Program Files/Git/usr/bin/bash.exe, or the behavioural tests are skipped.
"""

import os
import pathlib
import shutil
import subprocess
import sys
import tempfile
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "deploy-release.sh"
SHA_A = "a" * 40
SHA_B = "b" * 40


def _find_bash():
    explicit = os.environ.get("TEST_BASH")
    if explicit:
        return explicit
    if sys.platform == "win32":
        return None  # PATH's bash on Windows may be WSL, which cannot see this filesystem the same way
    return shutil.which("bash")


BASH = _find_bash()


def code_lines():
    return [line for line in SCRIPT.read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.lstrip().startswith("#")]


class SettingsAreNotHardCoded(unittest.TestCase):
    def test_the_branch_appears_in_code_only_as_the_default(self):
        hits = [line for line in code_lines() if "development" in line]
        self.assertEqual(hits, ["DEPLOY_BRANCH=${DEPLOY_BRANCH:-development}"])

    def test_the_backup_unit_appears_in_code_only_as_the_default_and_the_allow_list(self):
        hits = [line.strip() for line in code_lines() if "hcs-portal-backup" in line]
        self.assertEqual(len(hits), 3, hits)
        self.assertIn("BACKUP_UNIT=${BACKUP_UNIT:-hcs-portal-backup.service}", hits)
        self.assertTrue(all("BACKUP_UNIT" in line or line.startswith("hcs-portal-backup.service|") for line in hits),
                        hits)

    def test_the_settings_are_checked_before_the_first_git_or_backup_command(self):
        lines = code_lines()
        check = next(i for i, l in enumerate(lines) if l.strip().startswith('case "$BACKUP_UNIT" in'))
        first_git = next(i for i, l in enumerate(lines) if l.strip().startswith("git ") or "$(git " in l)
        first_backup = next(i for i, l in enumerate(lines) if "systemctl start" in l)
        self.assertLess(check, first_git)
        self.assertLess(check, first_backup)


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class SettingsAreValidated(unittest.TestCase):
    def setUp(self):
        self.home = tempfile.mkdtemp(prefix="deploy-settings-")
        os.mkdir(os.path.join(self.home, "hcs-patient-portal"))  # an empty stand-in for the checkout

    def tearDown(self):
        shutil.rmtree(self.home, ignore_errors=True)

    def run_script(self, **settings):
        env = dict(os.environ, HOME=self.home)
        env.pop("DEPLOY_BRANCH", None)
        env.pop("BACKUP_UNIT", None)
        env.update(settings)
        result = subprocess.run([BASH, str(SCRIPT), SHA_A, "api", SHA_B], env=env, capture_output=True,
                                encoding="utf-8", errors="replace", timeout=60)
        return result.returncode, result.stdout + result.stderr

    def assert_got_past_the_settings(self, output):
        self.assertIn("DEPLOY STOPPED: BASE_DOMAIN missing", output)
        self.assertNotIn("BACKUP_UNIT must be", output)
        self.assertNotIn("DEPLOY_BRANCH is not", output)

    def test_the_defaults_are_accepted(self):
        code, output = self.run_script()
        self.assertEqual(code, 1)
        self.assert_got_past_the_settings(output)

    def test_the_azure_values_are_accepted(self):
        code, output = self.run_script(DEPLOY_BRANCH="production", BACKUP_UNIT="hcs-portal-backup-azure.service")
        self.assertEqual(code, 1)
        self.assert_got_past_the_settings(output)

    def test_a_misspelled_backup_unit_stops_the_deploy(self):
        code, output = self.run_script(BACKUP_UNIT="hcs-portal-backup-azure")
        self.assertEqual(code, 1)
        self.assertIn("DEPLOY STOPPED: BACKUP_UNIT must be", output)
        self.assertNotIn("BASE_DOMAIN", output)

    def test_a_branch_carrying_shell_syntax_stops_the_deploy(self):
        for branch in ("main; touch x", "-q", "$(id)", ""):
            with self.subTest(branch=branch):
                code, output = self.run_script(DEPLOY_BRANCH=branch)
                self.assertEqual(code, 1)
                if branch:
                    self.assertIn("DEPLOY STOPPED: DEPLOY_BRANCH is not a plain branch name", output)
                else:
                    # An empty value falls back to the default, exactly as an unset one does.
                    self.assert_got_past_the_settings(output)


if __name__ == "__main__":
    unittest.main()
