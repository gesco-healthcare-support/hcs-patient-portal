"""scripts/hosting/backup-azure-blob.sh: the off-server backup to Azure Blob.

Two kinds of guard, for two kinds of failure.

STATIC. The script must never delete anything in Azure. Documents and appointment data are kept seven
years, and a backup that mirrors a deletion destroys the one copy that was supposed to survive it. rclone
makes that a one-word edit -- `copy` to `sync` -- and every run would still succeed, so nothing but a test
would ever notice. This reads the script and refuses every destructive rclone verb.

BEHAVIOURAL. The script runs against a fake `docker` that records each call, a fake dump script and a
throwaway env file, so the order of operations is asserted rather than trusted: the manifest is uploaded
only after the upload is verified, a dry run writes nothing, this run's dumps are found by what the dump
created rather than by the clock, a failed check marks nothing, and a download that does not match the
manifest fails the restore proof. No container, network or Azure account is involved.

The behavioural half needs a POSIX bash. CI runs on Linux; on Windows set TEST_BASH to Git Bash
(for example C:/Program Files/Git/bin/bash.exe), or those tests are skipped.
"""

import hashlib
import os
import pathlib
import re
import secrets
import shutil
import subprocess
import tempfile
import unittest

from shell_harness import BASH

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "backup-azure-blob.sh"
SYSTEMD = REPO_ROOT / "scripts" / "hosting" / "systemd"

DESTRUCTIVE_RCLONE_VERBS = (
    "sync", "bisync", "move", "moveto", "delete", "deletefile", "purge", "rmdir", "rmdirs", "cleanup", "dedupe",
)
DUMP_STAMP = "20200101-000000"  # deliberately NOT now: the script must not derive the stamp from its own clock
SEP = "\x1f"


def code_lines(path):
    """The script's lines with comments removed, so prose that names a verb is not mistaken for a call."""
    lines = []
    for line in path.read_text(encoding="utf-8").splitlines():
        stripped = line.lstrip()
        if stripped.startswith("#"):
            continue
        if re.search(r"\s#", line):
            line = re.split(r"\s#", line, maxsplit=1)[0]
        if line.strip():
            lines.append(line)
    return lines


class StaticTests(unittest.TestCase):
    def test_the_script_exists_and_is_bash(self):
        self.assertTrue(SCRIPT.is_file(), "scripts/hosting/backup-azure-blob.sh is missing")
        self.assertTrue(SCRIPT.read_text(encoding="utf-8").startswith("#!/usr/bin/env bash\n"))

    def test_it_runs_rclone_only_through_the_rc_helper(self):
        # Every rclone call goes through rc(), so checking rc's verbs below checks them all.
        calls = [line for line in code_lines(SCRIPT) if re.search(r"^\s*rc\s+\w", line)]
        self.assertGreaterEqual(len(calls), 6, "expected copy/check/copyto calls through rc()")

    def test_no_destructive_rclone_verb_appears(self):
        for line in code_lines(SCRIPT):
            match = re.search(r"^\s*rc\s+(\w+)", line)
            if match:
                with self.subTest(call=line.strip()):
                    self.assertIn(match.group(1), ("copy", "copyto", "check"),
                                  "only copy, copyto and check may reach Azure: a backup must never delete")
            with self.subTest(line=line.strip()):
                self.assertNotRegex(line, r"--delete|--remove-source|--backup-dir")
        verbs = "|".join(DESTRUCTIVE_RCLONE_VERBS)
        for line in code_lines(SCRIPT):
            with self.subTest(line=line.strip()):
                self.assertNotRegex(line, rf"\brc\s+({verbs})\b")

    def test_no_storage_key_or_connection_string_is_configured(self):
        text = "\n".join(code_lines(SCRIPT))
        self.assertNotRegex(text, r"RCLONE_CONFIG_AZ_KEY|SAS_URL|ACCOUNT_KEY|ConnectionString",
                            "authentication is the managed identity; a key in this script defeats it")
        self.assertIn("RCLONE_CONFIG_AZ_USE_MSI=true", text)

    def test_the_rclone_image_is_pinned_by_digest(self):
        self.assertRegex(SCRIPT.read_text(encoding="utf-8"), r"rclone/rclone:[0-9.]+@sha256:[0-9a-f]{64}")

    def test_the_units_alert_on_failure_and_run_the_right_mode(self):
        nightly = (SYSTEMD / "hcs-portal-backup-azure.service").read_text(encoding="utf-8")
        weekly = (SYSTEMD / "hcs-portal-backup-azure-verify.service").read_text(encoding="utf-8")
        for unit in (nightly, weekly):
            self.assertIn("OnFailure=hcs-portal-backup-alert@%n.service", unit)
            self.assertIn("scripts/hosting/backup-azure-blob.sh", unit)
        self.assertNotIn("--verify-restore", nightly)
        self.assertIn("--verify-restore", weekly)
        for timer in ("hcs-portal-backup-azure.timer", "hcs-portal-backup-azure-verify.timer"):
            with self.subTest(timer=timer):
                self.assertIn("Persistent=true", (SYSTEMD / timer).read_text(encoding="utf-8"))


FAKE_DOCKER = r"""#!/usr/bin/env bash
# Records every call, one per line, arguments separated by \x1f.
{ printf '%s\x1f' "$@"; printf '\n'; } >> "$FAKE_DOCKER_LOG"
case "$1" in
  network) exit 0 ;;
  compose)
    all="$*"
    if [[ "$all" == *FILELISTONLY* ]]; then echo "CaseEvaluation"; echo "CaseEvaluation_log"; fi
    if [[ "$all" == *"COUNT(*)"* ]]; then echo "113"; fi
    exit 0 ;;
  run)
    host_backups=""; prev=""; rest=(); seen_image=false
    for a in "$@"; do
      if [[ "$prev" == "-v" && "$a" == *":/backups" ]]; then host_backups="${a%:/backups}"; fi
      if $seen_image; then rest+=("$a"); fi
      if [[ "$a" == rclone/rclone* ]]; then seen_image=true; fi
      prev="$a"
    done
    verb="${rest[0]}"
    if [[ "$verb" == "check" && -n "${FAKE_FAIL_CHECK:-}" ]]; then echo "1 differences found" >&2; exit 1; fi
    if [[ "$verb" == "copyto" && "${rest[1]}" == az:* && "${rest[2]}" == /backups/* ]]; then
      src="$host_backups/$(basename "${rest[1]}")"
      dst="$host_backups/$(basename "${rest[2]}")"
      cp "$src" "$dst"
      if [[ -n "${FAKE_CORRUPT_DOWNLOAD:-}" ]]; then printf 'x' >> "$dst"; fi
    fi
    exit 0 ;;
esac
exit 0
"""

FAKE_FLOCK = "#!/usr/bin/env bash\nexit 0\n"

FAKE_DUMP = """#!/usr/bin/env bash
# Stands in for backup-databases.sh: writes this run's dumps with a FIXED stamp, as the real one does
# with a stamp of its own.
set -euo pipefail
for db in ${FAKE_DATABASES}; do
  printf 'synthetic dump of %s' "$db" > "${BACKUP_DIR}/${db}_${FAKE_DUMP_STAMP}.bak"
done
"""


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class BehaviourTests(unittest.TestCase):
    def setUp(self):
        self.tmp = pathlib.Path(tempfile.mkdtemp(prefix="azbackup-test-"))
        self.repo = self.tmp / "repo"
        (self.repo / "scripts" / "hosting").mkdir(parents=True)
        (self.repo / "secrets").mkdir()
        (self.repo / "scripts" / "hosting" / "backup-databases.sh").write_text(FAKE_DUMP, encoding="utf-8", newline="\n")
        self.backups = self.tmp / "backups"
        self.backups.mkdir()
        self.markers = self.tmp / "markers"
        self.bin = self.tmp / "bin"
        self.bin.mkdir()
        for name, body in (("docker", FAKE_DOCKER), ("flock", FAKE_FLOCK)):
            path = self.bin / name
            path.write_text(body, encoding="utf-8", newline="\n")
            path.chmod(0o755)
        self.log = self.tmp / "docker.log"
        # Generated per run rather than written as literals: they are fake, and a credential-shaped literal in
        # the repository is indistinguishable from a leaked one to every secret scanner.
        self.minio_secret = secrets.token_hex(12)
        self.write_env(
            "AZURE_BACKUP_STORAGE_ACCOUNT=synthbackupacct\n"
            "AZURE_BACKUP_BUCKETS=bucket-a, bucket-b\n"
            f"MINIO_APP_ACCESS_KEY={secrets.token_hex(6)}\n"
            f"MINIO_APP_SECRET_KEY={self.minio_secret}\n"
            f"MSSQL_SA_PASSWORD={secrets.token_hex(12)}\n")

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def write_env(self, text):
        (self.repo / "secrets" / "env.prod").write_text(text, encoding="utf-8", newline="\n")

    def run_script(self, *args, databases="CaseEvaluation CaseEvaluation_synth-office", **extra_env):
        env = dict(os.environ)
        env.update({
            "PATH": str(self.bin) + os.pathsep + env.get("PATH", ""),
            "REPO_DIR": self.repo.as_posix(),
            "BACKUP_DIR": self.backups.as_posix(),
            "MARKER_DIR": self.markers.as_posix(),
            "FAKE_DOCKER_LOG": self.log.as_posix(),
            "FAKE_DATABASES": databases,
            "FAKE_DUMP_STAMP": DUMP_STAMP,
        })
        env.update(extra_env)
        return subprocess.run([BASH, SCRIPT.as_posix(), *args], env=env, capture_output=True,
                              text=True, encoding="utf-8", errors="replace", timeout=120)

    def calls(self):
        if not self.log.exists():
            return []
        return [line.split(SEP)[:-1] for line in self.log.read_text(encoding="utf-8").splitlines() if line]

    def rclone_calls(self):
        """The rclone argument list of every `docker run`, image and docker flags stripped."""
        out = []
        for call in self.calls():
            if call and call[0] == "run":
                idx = next(i for i, a in enumerate(call) if a.startswith("rclone/rclone"))
                out.append(call[idx + 1:])
        return out

    # ------------------------------------------------------------------ nightly

    def test_a_nightly_run_uploads_the_matched_set_then_the_manifest_then_the_documents(self):
        (self.backups / "CaseEvaluation_19990101-000000.bak").write_text("an older night", encoding="utf-8")

        result = self.run_script()
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)

        rc = self.rclone_calls()
        run_dir = f"az:db-backups/{DUMP_STAMP}"
        self.assertEqual(rc[0][:3], ["copy", "/backups", run_dir])
        self.assertIn(f"*_{DUMP_STAMP}.bak", rc[0], "only this run's dumps, never an older night's")
        self.assertEqual(rc[1][:3], ["check", "/backups", run_dir])
        self.assertIn("--one-way", rc[1])
        self.assertEqual(rc[2], ["copyto", f"/stage/{DUMP_STAMP}.sha256", f"{run_dir}/MANIFEST.sha256"],
                         "the manifest goes up only after the upload is verified")
        self.assertEqual(rc[3][:3], ["copy", "minio:bucket-a", "az:documents/bucket-a"])
        self.assertEqual(rc[4][:3], ["check", "minio:bucket-a", "az:documents/bucket-a"])
        self.assertEqual(rc[5][:3], ["copy", "minio:bucket-b", "az:documents/bucket-b"])
        self.assertEqual(rc[6][:3], ["check", "minio:bucket-b", "az:documents/bucket-b"])
        self.assertEqual(len(rc), 7)

        manifest = (self.markers / "azure-stage" / f"{DUMP_STAMP}.sha256").read_text(encoding="utf-8")
        for db in ("CaseEvaluation", "CaseEvaluation_synth-office"):
            digest = hashlib.sha256((self.backups / f"{db}_{DUMP_STAMP}.bak").read_bytes()).hexdigest()
            self.assertIn(f"{digest} *{db}_{DUMP_STAMP}.bak", manifest)
        self.assertNotIn("19990101", manifest)
        self.assertTrue((self.markers / "last-success").exists())
        self.assertFalse((self.markers / "last-restore-proof").exists())

    def test_credentials_reach_rclone_through_a_file_never_the_command_line(self):
        result = self.run_script()
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        for call in self.calls():
            joined = " ".join(call)
            self.assertNotIn(self.minio_secret, joined)
            self.assertNotIn("RCLONE_CONFIG_", joined)
            if call[0] == "run":
                self.assertIn("--env-file", call)
        self.assertEqual(list((self.markers / "azure-stage").glob("rclone-env.*")), [],
                         "the credential file must be removed on exit")
        self.assertNotIn(self.minio_secret, result.stdout + result.stderr)

    # ------------------------------------------------------------------ dry run

    def test_a_dry_run_dumps_nothing_uploads_nothing_and_marks_nothing(self):
        (self.backups / f"CaseEvaluation_{DUMP_STAMP}.bak").write_text("last night", encoding="utf-8")

        result = self.run_script("--dry-run")
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)

        self.assertEqual(sorted(p.name for p in self.backups.iterdir()), [f"CaseEvaluation_{DUMP_STAMP}.bak"],
                         "a dry run must not run the dump")
        rc = self.rclone_calls()
        self.assertTrue(rc)
        for call in rc:
            with self.subTest(call=call):
                self.assertEqual(call[0], "copy")
                self.assertIn("--dry-run", call)
        self.assertFalse((self.markers / "last-success").exists())

    def test_dry_run_and_restore_proof_cannot_be_combined(self):
        result = self.run_script("--dry-run", "--verify-restore")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.rclone_calls(), [])

    # ------------------------------------------------------------------ refusals

    def test_a_failed_check_writes_no_manifest_and_marks_nothing(self):
        result = self.run_script(FAKE_FAIL_CHECK="1")
        self.assertNotEqual(result.returncode, 0)
        verbs = [call[0] for call in self.rclone_calls()]
        self.assertEqual(verbs, ["copy", "check"], "nothing may follow a failed verification")
        self.assertFalse((self.markers / "last-success").exists())

    def test_dumps_with_two_stamps_are_refused_before_anything_is_uploaded(self):
        dump = self.repo / "scripts" / "hosting" / "backup-databases.sh"
        dump.write_text(FAKE_DUMP + 'printf x > "${BACKUP_DIR}/CaseEvaluation_other_20200101-000001.bak"\n',
                        encoding="utf-8", newline="\n")
        result = self.run_script()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("do not share one time stamp", result.stderr)
        self.assertEqual(self.rclone_calls(), [])

    def test_a_run_without_the_host_database_is_refused(self):
        result = self.run_script(databases="CaseEvaluation_synth-office")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("host database dump", result.stderr)
        self.assertEqual(self.rclone_calls(), [])

    def test_a_missing_storage_account_is_refused_before_anything_runs(self):
        self.write_env(f"MINIO_APP_ACCESS_KEY={secrets.token_hex(6)}\nMINIO_APP_SECRET_KEY={secrets.token_hex(12)}\n")
        result = self.run_script()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("AZURE_BACKUP_STORAGE_ACCOUNT", result.stderr)
        self.assertEqual(self.calls(), [])

    def test_an_unknown_argument_is_refused(self):
        result = self.run_script("--delete-everything")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.calls(), [])

    # ------------------------------------------------------------------ restore proof

    def test_the_restore_proof_restores_the_copy_downloaded_from_azure(self):
        result = self.run_script("--verify-restore")
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)

        rc = self.rclone_calls()
        self.assertEqual(rc[-1][0], "copyto")
        self.assertEqual(rc[-1][1], f"az:db-backups/{DUMP_STAMP}/CaseEvaluation_{DUMP_STAMP}.bak")
        restores = [c for c in self.calls() if c[0] == "compose" and any("RESTORE DATABASE" in a for a in c)]
        self.assertEqual(len(restores), 1)
        self.assertTrue((self.markers / "last-restore-proof").exists())
        self.assertTrue((self.markers / "last-success").exists())
        self.assertEqual([p.name for p in self.backups.glob("*.bakproof")], [],
                         "the downloaded copy is removed after the proof")

    def test_a_download_that_does_not_match_the_manifest_fails_the_proof(self):
        result = self.run_script("--verify-restore", FAKE_CORRUPT_DOWNLOAD="1")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("does not match its manifest", result.stderr)
        restores = [c for c in self.calls() if c[0] == "compose" and any("RESTORE DATABASE" in a for a in c)]
        self.assertEqual(restores, [])
        self.assertFalse((self.markers / "last-restore-proof").exists())
        self.assertFalse((self.markers / "last-success").exists())


if __name__ == "__main__":
    unittest.main()
