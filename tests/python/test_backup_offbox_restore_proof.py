"""scripts/hosting/backup-offbox.sh --verify-restore: the weekly restore proof on the office server.

The proof restores the newest host dump into a scratch database. It used to run `fail` (which is `exit 1`)
before the `DROP DATABASE`, so a RESTORE that ran the disk out of space left the scratch database RESTORING
with gigabytes behind it, and dropping a RESTORING database leaves its files on disk anyway. These tests pin:

  - the scratch database is dropped AND its files removed on every exit, success or failure;
  - the cleanup never replaces the script's own exit status;
  - a disk too small for the restore is refused BEFORE the RESTORE is issued.

The real scripts run against a fake `docker`, `ssh`, `scp` and `date`. Nothing real is touched. They need a POSIX
bash: CI runs on Linux; on Windows set TEST_BASH to C:/Program Files/Git/usr/bin/bash.exe or they are skipped.
"""

import os
import pathlib
import secrets
import shutil
import subprocess
import tempfile
import unittest

from shell_harness import BASH, shell_path
from test_backup_run_stamp import FAKE_DATE, FAKE_SCP, FAKE_SSH

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
OFFBOX = REPO_ROOT / "scripts" / "hosting" / "backup-offbox.sh"
STAMP = "20261013-010000"

FAKE_DOCKER = r"""#!/usr/bin/env bash
{ printf '%s\x1f' "$@"; printf '\n'; } >> "$FAKE_LOG"
all="$*"
if [[ "$1" == compose ]]; then
  if [[ "$all" == *sys.databases* ]]; then printf 'CaseEvaluation\n'; exit 0; fi
  re="BACKUP DATABASE \[[^]]*\] TO DISK = N'/var/opt/mssql/backups/([^']+)'"
  if [[ "$all" =~ $re ]]; then printf 'synthetic dump' > "$FAKE_BACKUP_DIR/${BASH_REMATCH[1]}"; fi
  if [[ "$all" == *FILELISTONLY* ]]; then
    echo "CaseEvaluation|/var/opt/mssql/data/CaseEvaluation.mdf|D|PRIMARY|${FAKE_DATA_BYTES:-8388608}|35184372080640"
    echo "CaseEvaluation_log|/var/opt/mssql/data/CaseEvaluation_log.ldf|L||${FAKE_LOG_BYTES:-8388608}|2199023255552"
  fi
  if [[ "$all" == *"df -B1"* ]]; then echo "Avail"; echo "${FAKE_AVAIL_BYTES:-107374182400}"; fi
  if [[ "$all" == *"RESTORE DATABASE"* && -n "${FAKE_FAIL_RESTORE:-}" ]]; then
    echo "Msg 3257: There is insufficient free space" >&2; exit 1
  fi
  if [[ "$all" == *"DROP DATABASE"* && -n "${FAKE_FAIL_DROP:-}" ]]; then exit 7; fi
  if [[ "$all" == *"COUNT(*)"* ]]; then echo "113"; fi
fi
exit 0
"""


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class RestoreProofTests(unittest.TestCase):
    def setUp(self):
        self.tmp = pathlib.Path(tempfile.mkdtemp(prefix="offbox-proof-test-"))
        self.bin = self.tmp / "bin"
        self.bin.mkdir()
        for name, body in (("docker", FAKE_DOCKER), ("date", FAKE_DATE), ("scp", FAKE_SCP), ("ssh", FAKE_SSH)):
            path = self.bin / name
            path.write_text(body, encoding="utf-8", newline="\n")
            path.chmod(0o755)
        self.backups, self.staging, self.remote = (self.tmp / n for n in ("backups", "staging", "remote"))
        for d in (self.backups, self.staging, self.remote):
            d.mkdir()
        self.env_file = self.tmp / "env.prod"
        self.env_file.write_text(
            f"MSSQL_SA_PASSWORD={secrets.token_hex(12)}\nMINIO_ROOT_USER={secrets.token_hex(6)}\n"
            f"MINIO_ROOT_PASSWORD={secrets.token_hex(12)}\n", encoding="utf-8", newline="\n")
        self.key = self.tmp / "key"
        self.key.write_text("not a real key\n", encoding="utf-8")
        self.log = self.tmp / "docker.log"

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def run_script(self, *args, **extra_env):
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
        return subprocess.run([BASH, shell_path(OFFBOX), *args], cwd=REPO_ROOT, env=env, capture_output=True,
                              text=True, encoding="utf-8", errors="replace", timeout=180)

    def calls(self):
        if not self.log.exists():
            return []
        return [line for line in self.log.read_text(encoding="utf-8").splitlines() if line]

    def with_text(self, text):
        return [c for c in self.calls() if text in c.replace("\x1f", " ")]

    def test_a_successful_proof_drops_the_scratch_database_and_removes_its_files(self):
        result = self.run_script("--verify-restore")
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        self.assertEqual(len(self.with_text("RESTORE DATABASE")), 1)
        self.assertEqual(len(self.with_text("DROP DATABASE [RestoreProof_")), 1)
        removals = self.with_text("rm -f /var/opt/mssql/data/")
        self.assertEqual(len(removals), 1)
        self.assertIn(f"RestoreProof_{STAMP}", removals[0])
        self.assertTrue((self.staging / "last-restore-proof").exists())
        self.assertTrue((self.staging / "last-success").exists())

    def test_without_the_flag_no_scratch_work_happens(self):
        result = self.run_script()
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        self.assertEqual(self.with_text("RESTORE"), [])
        self.assertEqual(self.with_text("DROP DATABASE"), [])
        self.assertFalse((self.staging / "last-restore-proof").exists())

    def test_a_failed_restore_still_drops_the_scratch_database_and_removes_its_files(self):
        result = self.run_script("--verify-restore", FAKE_FAIL_RESTORE="1")
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("RESTORE FAILED", result.stderr)
        self.assertEqual(len(self.with_text("DROP DATABASE [RestoreProof_")), 1)
        self.assertEqual(len(self.with_text("rm -f /var/opt/mssql/data/")), 1)
        self.assertFalse((self.staging / "last-restore-proof").exists())
        self.assertFalse((self.staging / "last-success").exists())

    def test_cleanup_never_replaces_the_scripts_own_exit_status(self):
        # The DROP fails with 7 inside the trap; the run's own status (1) must survive, and the files still go.
        result = self.run_script("--verify-restore", FAKE_FAIL_RESTORE="1", FAKE_FAIL_DROP="1")
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertEqual(len(self.with_text("rm -f /var/opt/mssql/data/")), 1)

    def test_too_little_free_disk_refuses_the_restore_before_it_is_issued(self):
        # Needs 16 MB * 1.1 + 1 GiB; offer exactly 1 GiB.
        result = self.run_script("--verify-restore", FAKE_AVAIL_BYTES=str(1024 * 1048576))
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("not enough free disk", result.stderr)
        self.assertIn("16 MB", result.stderr)
        self.assertIn("1024 MB is free", result.stderr)
        self.assertEqual(self.with_text("RESTORE DATABASE"), [], "no RESTORE on a disk that cannot hold it")
        self.assertEqual(len(self.with_text("DROP DATABASE [RestoreProof_")), 1, "the trap still runs")
        self.assertFalse((self.staging / "last-success").exists())

    def test_the_free_space_check_sums_every_file_of_the_backup(self):
        # 2 x 600 MB needs 2344 MB; 2000 MB is refused although one file alone would fit.
        result = self.run_script("--verify-restore", FAKE_DATA_BYTES=str(600 * 1048576),
                                 FAKE_LOG_BYTES=str(600 * 1048576), FAKE_AVAIL_BYTES=str(2000 * 1048576))
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("1200 MB", result.stderr)
        self.assertEqual(self.with_text("RESTORE DATABASE"), [])


if __name__ == "__main__":
    unittest.main()
