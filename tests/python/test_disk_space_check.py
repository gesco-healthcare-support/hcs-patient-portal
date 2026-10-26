"""scripts/hosting/disk-space-check.sh emails when / or /srv fills past a line, and says what is using the space.

What these tests pin:

  - below the warning line nothing is sent and nothing is recorded;
  - crossing the warning line emails once; the same level the next day sends nothing more;
  - rising to the critical line emails again; staying there emails again only after DISK_REMIND_DAYS;
  - falling back below the warning line clears the record, so the next rise emails again;
  - two paths on one filesystem are checked once;
  - the email names the SQL Server volume, the MinIO volume and the backups folder with their sizes, Docker's own
    usage, and warns never to prune images;
  - an alert that cannot be sent records nothing (the next run retries) and fails the run;
  - a dry run prints the message and records nothing; bad settings are refused.

`df`, `docker` and `curl` are fakes on PATH. `df` prints what $FAKE_DIR/df/<basename of the path> holds; `curl`
copies the uploaded message to $FAKE_DIR/mail-<n> (or fails when $FAKE_DIR/curl_fails exists).
"""

import os
import pathlib
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest

from shell_harness import BASH, shell_path

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "disk-space-check.sh"
ALERT = REPO_ROOT / "scripts" / "hosting" / "backup-alert.sh"
SERVICE = REPO_ROOT / "scripts" / "hosting" / "systemd" / "hcs-portal-disk-space.service"
TIMER = REPO_ROOT / "scripts" / "hosting" / "systemd" / "hcs-portal-disk-space.timer"
RUNBOOK = REPO_ROOT / "docs" / "runbooks" / "hosting-backup-restore.md"

FAKE_DF = r"""#!/usr/bin/env bash
path="${@: -1}"
echo "Filesystem 1024-blocks Used Available Capacity Mounted on"
cat "$FAKE_DIR/df/$(basename "$path")"
"""

FAKE_DOCKER = r"""#!/usr/bin/env bash
case "$1 $2" in
  "volume ls")
    case "$*" in
      *com.docker.compose.volume=sqldata*) echo hcs-patient-portal_sqldata ;;
      *com.docker.compose.volume=miniodata*) echo hcs-patient-portal_miniodata ;;
    esac
    exit 0 ;;
  "volume inspect")
    case "${@: -1}" in
      *_sqldata) echo "$FAKE_DIR/vol/sqldata" ;;
      *_miniodata) echo "$FAKE_DIR/vol/miniodata" ;;
    esac
    exit 0 ;;
  "system df")
    echo "Images: 13.2GB (2.1GB (15%) reclaimable)"
    echo "Build Cache: 21.4GB (21.4GB reclaimable)"
    exit 0 ;;
esac
echo "fake docker: unexpected: $*" >&2
exit 2
"""

FAKE_CURL = r"""#!/usr/bin/env bash
[[ -f "$FAKE_DIR/curl_fails" ]] && { echo "fake curl: connection refused" >&2; exit 7; }
file=""
while [[ $# -gt 0 ]]; do
  if [[ "$1" = "--upload-file" ]]; then file="$2"; shift; fi
  shift
done
n=$(( $(ls "$FAKE_DIR" | grep -c '^mail-' || true) + 1 ))
cp "$file" "$FAKE_DIR/mail-$n"
"""

DAY = 86400


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class DiskSpaceCheck(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="disk-space-"))
        for sub in ("bin", "df", "state", "paths/root", "paths/srv", "repo/backups", "vol/sqldata", "vol/miniodata"):
            (self.dir / sub).mkdir(parents=True)
        for name, text in (("df", FAKE_DF), ("docker", FAKE_DOCKER), ("curl", FAKE_CURL)):
            path = self.dir / "bin" / name
            path.write_text(text, encoding="utf-8", newline="\n")
            path.chmod(path.stat().st_mode | stat.S_IEXEC)
        (self.dir / "vol" / "sqldata" / "data.mdf").write_bytes(b"\0" * 300_000)
        (self.dir / "vol" / "miniodata" / "object").write_bytes(b"\0" * 20_000)
        (self.dir / "repo" / "backups" / "set.bak").write_bytes(b"\0" * 100_000)
        (self.dir / "env.prod").write_text(
            "SMTP_HOST=mail.example.test\nSMTP_FROM_ADDRESS=portal@example.test\n"
            "BACKUP_ALERT_RECIPIENTS=ops@example.test\n"
            f"BACKUP_DIR={shell_path(self.dir / 'repo' / 'backups')}\n",
            encoding="utf-8", newline="\n")
        self.set_df("root", 20, device="/dev/sda1", mount="/")
        self.set_df("srv", 17, device="/dev/sdb1", mount="/srv")

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def set_df(self, name, percent, device, mount):
        total = 262_144_000
        used = total * percent // 100
        (self.dir / "df" / name).write_text(
            f"{device} {total} {used} {total - used} {percent}% {mount}\n", encoding="utf-8", newline="\n")

    def run_check(self, **extra):
        env = dict(os.environ, FAKE_DIR=shell_path(self.dir), ENV_FILE=shell_path(self.dir / "env.prod"),
                   STATE_DIR=shell_path(self.dir / "state"),
                   DISK_PATHS=shell_path(self.dir / "paths" / "root") + " " + shell_path(self.dir / "paths" / "srv"))
        env.pop("BACKUP_ALERT_DRY_RUN", None)
        env.update(extra)
        env["PATH"] = (shell_path(self.dir / "bin") + ":/usr/bin:/bin" if sys.platform == "win32"
                       else str(self.dir / "bin") + os.pathsep + os.environ.get("PATH", ""))
        result = subprocess.run([BASH, str(SCRIPT)], env=env, capture_output=True, encoding="utf-8",
                                errors="replace", timeout=120)
        return result.returncode, result.stdout + result.stderr

    def mails(self):
        return [p.read_text(encoding="utf-8", errors="replace")
                for p in sorted(self.dir.glob("mail-*"), key=lambda p: int(p.name.split("-")[1]))]

    def state(self, key):
        path = self.dir / "state" / key
        return path.read_text(encoding="utf-8").split() if path.exists() else None

    def age_state(self, key, days):
        level, sent = self.state(key)
        (self.dir / "state" / key).write_text(f"{level} {int(sent) - days * DAY}\n", encoding="utf-8", newline="\n")

    def test_below_the_warning_line_nothing_is_sent_or_recorded(self):
        code, out = self.run_check()
        self.assertEqual(code, 0, out)
        self.assertEqual(self.mails(), [])
        self.assertIn("/srv OK, 17% used", out)
        self.assertEqual(list((self.dir / "state").iterdir()), [])

    def test_crossing_the_warning_line_emails_once_then_waits(self):
        self.set_df("srv", 72, device="/dev/sdb1", mount="/srv")
        code, out = self.run_check()
        self.assertEqual(code, 0, out)
        mails = self.mails()
        self.assertEqual(len(mails), 1)
        self.assertIn("Subject: Patient Portal disk: /srv is 72% full on ", mails[0])
        self.assertNotIn("(critical)", mails[0])
        self.assertEqual(self.state("fs_srv")[0], "1")

        code, out = self.run_check()
        self.assertEqual(code, 0, out)
        self.assertEqual(len(self.mails()), 1, "the same level the next day sends nothing more")
        self.assertIn("already emailed", out)

    def test_rising_to_the_critical_line_emails_again(self):
        self.set_df("srv", 72, device="/dev/sdb1", mount="/srv")
        self.run_check()
        self.set_df("srv", 86, device="/dev/sdb1", mount="/srv")
        code, out = self.run_check()
        self.assertEqual(code, 0, out)
        mails = self.mails()
        self.assertEqual(len(mails), 2)
        self.assertIn("Subject: Patient Portal disk: /srv is 86% full (critical) on ", mails[1])
        self.assertIn("every save in the portal fails", mails[1])
        self.assertEqual(self.state("fs_srv")[0], "2")

    def test_a_disk_left_full_is_reminded_after_the_reminder_period(self):
        self.set_df("srv", 90, device="/dev/sdb1", mount="/srv")
        self.run_check()
        self.age_state("fs_srv", 6)
        self.run_check()
        self.assertEqual(len(self.mails()), 1, "six days on: no reminder yet")
        self.age_state("fs_srv", 2)
        self.run_check()
        self.assertEqual(len(self.mails()), 2, "past seven days: reminded")

    def test_falling_back_below_the_line_clears_the_record_so_the_next_rise_emails(self):
        self.set_df("srv", 75, device="/dev/sdb1", mount="/srv")
        self.run_check()
        self.set_df("srv", 40, device="/dev/sdb1", mount="/srv")
        self.run_check()
        self.assertIsNone(self.state("fs_srv"))
        self.set_df("srv", 71, device="/dev/sdb1", mount="/srv")
        self.run_check()
        self.assertEqual(len(self.mails()), 2)

    def test_dropping_from_critical_to_warning_then_climbing_back_emails_again(self):
        self.set_df("srv", 88, device="/dev/sdb1", mount="/srv")
        self.run_check()
        self.set_df("srv", 80, device="/dev/sdb1", mount="/srv")
        self.run_check()
        self.assertEqual(len(self.mails()), 1, "falling to the warning level is not news")
        self.set_df("srv", 87, device="/dev/sdb1", mount="/srv")
        self.run_check()
        self.assertEqual(len(self.mails()), 2, "climbing back to critical is")

    def test_each_filesystem_is_checked_once(self):
        self.set_df("root", 91, device="/dev/sdb1", mount="/srv")
        self.set_df("srv", 91, device="/dev/sdb1", mount="/srv")
        code, out = self.run_check()
        self.assertEqual(code, 0, out)
        self.assertEqual(len(self.mails()), 1)
        self.assertIn("already checked; skipped", out)

    def test_the_email_says_what_is_using_the_space_and_warns_never_to_prune_images(self):
        self.set_df("root", 78, device="/dev/sda1", mount="/")
        self.run_check()
        mail = self.mails()[0]
        self.assertRegex(mail, r"SQL Server volume \(the databases\): +\d+\.\d (KiB|MiB)")
        self.assertRegex(mail, r"MinIO volume \(the documents\): +\d+\.\d (KiB|MiB)")
        self.assertRegex(mail, r"backups folder \(local backup sets\): +\d+\.\d (KiB|MiB)")
        self.assertIn("Build Cache: 21.4GB", mail)
        self.assertIn("NEVER run docker image prune or docker system prune", mail)
        self.assertIn("docker builder prune -f --filter until=48h", mail)
        self.assertNotIn("data.mdf", mail, "sizes only, never file names")
        self.assertNotIn("set.bak", mail)

    def test_an_alert_that_cannot_be_sent_records_nothing_and_fails_the_run(self):
        self.set_df("srv", 95, device="/dev/sdb1", mount="/srv")
        (self.dir / "curl_fails").write_text("", encoding="utf-8")
        code, out = self.run_check()
        self.assertEqual(code, 1, out)
        self.assertIsNone(self.state("fs_srv"), "the next run must try again")
        (self.dir / "curl_fails").unlink()
        code, out = self.run_check()
        self.assertEqual(code, 0, out)
        self.assertEqual(len(self.mails()), 1)

    def test_a_dry_run_prints_the_message_and_records_nothing(self):
        self.set_df("srv", 93, device="/dev/sdb1", mount="/srv")
        code, out = self.run_check(BACKUP_ALERT_DRY_RUN="1")
        self.assertEqual(code, 0, out)
        self.assertIn("DRY RUN: would send", out)
        self.assertIn("Subject: Patient Portal disk: /srv is 93% full (critical)", out)
        self.assertEqual(self.mails(), [])
        self.assertIsNone(self.state("fs_srv"))

    def test_a_path_that_does_not_exist_is_skipped(self):
        code, out = self.run_check(DISK_PATHS=shell_path(self.dir / "paths" / "nowhere"))
        self.assertEqual(code, 0, out)
        self.assertIn("does not exist on this machine; skipped", out)

    def test_bad_settings_are_refused(self):
        for extra in ({"DISK_WARN_PERCENT": "90", "DISK_CRIT_PERCENT": "85"},
                      {"DISK_WARN_PERCENT": "seventy"},
                      {"DISK_REMIND_DAYS": "0"}):
            code, out = self.run_check(**extra)
            self.assertEqual(code, 2, f"{extra}: {out}")


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class TheAlertNamesADiskAlertAsOne(unittest.TestCase):
    def test_the_disk_kind_has_its_own_subject_and_footer(self):
        d = pathlib.Path(tempfile.mkdtemp(prefix="disk-alert-"))
        try:
            (d / "env.prod").write_text("SMTP_HOST=mail.example.test\nSMTP_FROM_ADDRESS=portal@example.test\n"
                                        "BACKUP_ALERT_RECIPIENTS=ops@example.test\n", encoding="utf-8", newline="\n")
            env = dict(os.environ, ENV_FILE=shell_path(d / "env.prod"), BACKUP_ALERT_DRY_RUN="1")
            result = subprocess.run([BASH, str(ALERT), "disk", "/srv is 71% full", "detail text"], env=env,
                                    capture_output=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("Subject: Patient Portal disk: /srv is 71% full on ", result.stdout)
            self.assertIn("by the daily disk space check", result.stdout)
            self.assertNotIn("backup", result.stdout.split("Subject:")[1].splitlines()[0].lower())
        finally:
            shutil.rmtree(d, ignore_errors=True)


class TheUnitsAndScriptShipCorrectly(unittest.TestCase):
    def test_git_tracks_the_script_executable(self):
        mode = subprocess.run(["git", "ls-files", "-s", str(SCRIPT.relative_to(REPO_ROOT))], cwd=REPO_ROOT,
                              capture_output=True, encoding="utf-8").stdout.split(" ")[0]
        self.assertEqual(mode, "100755")

    def test_the_service_runs_the_script_as_root(self):
        unit = SERVICE.read_text(encoding="utf-8")
        self.assertIn("Type=oneshot", unit)
        self.assertIn("User=root", unit)
        self.assertIn("ExecStart=/bin/bash /home/apadmin/hcs-patient-portal/scripts/hosting/disk-space-check.sh", unit)

    def test_the_timer_fires_daily_and_catches_up_after_downtime(self):
        timer = TIMER.read_text(encoding="utf-8")
        self.assertRegex(timer, r"OnCalendar=\*-\*-\* \d\d:\d\d:\d\d")
        self.assertIn("Persistent=true", timer)
        self.assertIn("WantedBy=timers.target", timer)

    def test_the_runbook_installs_the_timer(self):
        text = RUNBOOK.read_text(encoding="utf-8")
        self.assertIn("hcs-portal-disk-space.timer", text)
        self.assertIn("systemctl enable --now hcs-portal-disk-space.timer", text)


if __name__ == "__main__":
    unittest.main()
