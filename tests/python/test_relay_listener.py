"""scripts/hosting/relay-listener.sh keeps the Azure Relay listener attached to the reverse proxy.

What these tests pin:

  - the listener runs in the reverse proxy's network namespace (`--network container:<proxy id>`) and forwards the
    hybrid connection to 127.0.0.1:443 -- nginx -- so the MinIO vhost can stay closed to the internet;
  - by default it signs in with the VM's managed identity (no key anywhere); the SAS fallback travels in an env
    file, never on the command line;
  - it refuses any azbridge image but the pinned one, and refuses to start with missing settings;
  - when a deploy recreates the proxy, the listener is re-attached to the NEW proxy;
  - a listener that keeps dying fails the script (so systemd marks the unit failed and it alerts);
  - the alert for this unit is worded as a relay failure, not a backup one.

`docker` is a fake that keeps the proxy's id, the listener's state and the image id in files.
"""

import os
import pathlib
import secrets
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest

from shell_harness import BASH, shell_path

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "relay-listener.sh"
ALERT = REPO_ROOT / "scripts" / "hosting" / "backup-alert.sh"
UNIT = REPO_ROOT / "scripts" / "hosting" / "systemd" / "hcs-portal-relay.service"
SEP = "\x1f"
PROXY_A = "a" * 64
PROXY_B = "b" * 64
IMAGE_ID = "sha256:" + "c" * 64

# State in $FAKE_DIR: proxy_ids (one id per line, consumed one per inspect, last one sticks), relay_state, image_id,
# runs. A listener that stays up forever would keep the script watching forever, so by default every listener exits
# at once (FAKE_LISTENER_DIES_FROM_RUN=1) and MAX_CYCLES=1 ends the run after the first start.
FAKE_DOCKER = r"""#!/usr/bin/env bash
{ printf '%s\x1f' "$@"; printf '\n'; } >> "$FAKE_DIR/docker.log"
d="$FAKE_DIR"
case "$1 $2" in
  "image inspect") cat "$d/image_id"; exit 0 ;;
esac
case "$1" in
  inspect)
    if [[ "$*" == *"{{if .State.Running}}{{.Id}}{{end}}"* ]]; then
      first="$(head -1 "$d/proxy_ids")"
      [[ "$(wc -l < "$d/proxy_ids")" -gt 1 ]] && sed -i '1d' "$d/proxy_ids"
      printf '%s' "$first"; exit 0
    fi
    if [[ "$*" == *"{{.State.Running}}"* ]]; then cat "$d/relay_state" 2>/dev/null || echo false; exit 0; fi
    ;;
  run)
    n=$(( $(cat "$d/runs" 2>/dev/null || echo 0) + 1 )); echo "$n" > "$d/runs"
    # From run number FAKE_LISTENER_DIES_FROM_RUN on, the listener exits at once; before it, it stays up.
    if (( n >= ${FAKE_LISTENER_DIES_FROM_RUN:-1} )); then echo false > "$d/relay_state"; else echo true > "$d/relay_state"; fi
    echo cid; exit 0 ;;
  rm) echo false > "$d/relay_state"; exit 0 ;;
  logs) echo "azbridge: unauthorized"; exit 0 ;;
esac
echo "fake docker: unexpected: $*" >&2
exit 2
"""


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class RelayListener(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="relay-listener-"))
        (self.dir / "bin").mkdir()
        path = self.dir / "bin" / "docker"
        path.write_text(FAKE_DOCKER, encoding="utf-8", newline="\n")
        path.chmod(path.stat().st_mode | stat.S_IEXEC)
        (self.dir / "image_id").write_text(IMAGE_ID, encoding="utf-8", newline="\n")
        self.set_proxy_ids(PROXY_A)
        self.write_env()

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def set_proxy_ids(self, *ids):
        (self.dir / "proxy_ids").write_text("\n".join(ids) + "\n", encoding="utf-8", newline="\n")

    def write_env(self, **overrides):
        values = {
            "RELAY_NAMESPACE": "relay-portal-test1",
            "RELAY_HYBRID_CONNECTION": "case-tracker-documents",
            "RELAY_AZBRIDGE_IMAGE": "azbridge:0.16.1",
            "RELAY_AZBRIDGE_IMAGE_ID": IMAGE_ID,
        }
        values.update(overrides)
        (self.dir / "env.prod").write_text("".join(f"{k}={v}\n" for k, v in values.items() if v is not None),
                                           encoding="utf-8", newline="\n")

    def run_script(self, **extra):
        env = dict(os.environ, FAKE_DIR=shell_path(self.dir), ENV_FILE=shell_path(self.dir / "env.prod"),
                   CHECK_EVERY="0", MAX_CYCLES="1")
        env.update(extra)
        env["PATH"] = (shell_path(self.dir / "bin") + ":/usr/bin:/bin" if sys.platform == "win32"
                       else str(self.dir / "bin") + os.pathsep + os.environ.get("PATH", ""))
        result = subprocess.run([BASH, str(SCRIPT)], env=env, capture_output=True, encoding="utf-8",
                                errors="replace", timeout=120)
        return result.returncode, result.stdout + result.stderr

    def runs(self):
        log = self.dir / "docker.log"
        calls = [line.split(SEP)[:-1] for line in log.read_text(encoding="utf-8").splitlines()] if log.exists() else []
        return [c for c in calls if c and c[0] == "run"]

    def test_the_listener_shares_the_proxy_namespace_and_forwards_to_nginx(self):
        code, output = self.run_script()
        self.assertEqual(code, 0, output)
        run = self.runs()[0]
        self.assertEqual(run[run.index("--network") + 1], f"container:{PROXY_A}")
        self.assertIn("azbridge:0.16.1", run)
        self.assertEqual(run[run.index("-T") + 1], "case-tracker-documents:127.0.0.1:443")
        self.assertEqual(run[run.index("-e") + 1], "sb://relay-portal-test1.servicebus.windows.net/")
        self.assertIn("managed identity", output)

    def test_by_default_no_key_and_no_env_file(self):
        self.assertEqual(self.run_script()[0], 0)
        run = self.runs()[0]
        self.assertNotIn("--env-file", run)
        self.assertNotIn("-x", run)
        self.assertFalse(any("SharedAccessKey" in a for a in run))

    def test_the_sas_fallback_travels_in_an_env_file_never_on_the_command_line(self):
        key = secrets.token_hex(16)
        conn = self.dir / "relay-listen.env"
        conn.write_text("AZURE_BRIDGE_CONNECTIONSTRING=Endpoint=sb://relay-portal-test1.servicebus.windows.net/;"
                        f"SharedAccessKeyName=vm-listen;SharedAccessKey={key};EntityPath=case-tracker-documents\n",
                        encoding="utf-8", newline="\n")
        self.write_env(RELAY_LISTEN_CONNECTION_STRING_FILE=shell_path(conn))
        code, output = self.run_script()
        self.assertEqual(code, 0, output)
        run = self.runs()[0]
        self.assertEqual(run[run.index("--env-file") + 1], shell_path(conn))
        self.assertFalse(any(key in a for a in run))
        self.assertNotIn(key, output)

    def test_a_recreated_proxy_gets_the_listener_reattached(self):
        # Inspections: start (A), watch (A), watch (B: changed) -> restart against B.
        self.set_proxy_ids(PROXY_A, PROXY_A, PROXY_B)
        code, output = self.run_script(MAX_CYCLES="2", FAKE_LISTENER_DIES_FROM_RUN="2")
        self.assertEqual(code, 0, output)
        networks = [r[r.index("--network") + 1] for r in self.runs()]
        self.assertEqual(networks, [f"container:{PROXY_A}", f"container:{PROXY_B}"])
        self.assertIn("re-attaching", output)

    def test_a_listener_that_keeps_dying_fails_the_script(self):
        code, output = self.run_script(MAX_CYCLES="0")
        self.assertNotEqual(code, 0)
        self.assertIn("exited 5 times in a row", output)
        self.assertEqual(len(self.runs()), 5)

    def test_any_other_image_is_refused(self):
        (self.dir / "image_id").write_text("sha256:" + "d" * 64, encoding="utf-8", newline="\n")
        code, output = self.run_script()
        self.assertNotEqual(code, 0)
        self.assertIn("refusing to run it", output)
        self.assertEqual(self.runs(), [])

    def test_missing_settings_are_refused_before_anything_starts(self):
        for missing in ("RELAY_NAMESPACE", "RELAY_AZBRIDGE_IMAGE", "RELAY_AZBRIDGE_IMAGE_ID"):
            with self.subTest(missing=missing):
                self.write_env(**{missing: None})
                code, output = self.run_script()
                self.assertNotEqual(code, 0)
                self.assertIn(missing, output)
        self.assertEqual(self.runs(), [])


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class TheAlertNamesARelayFailureAsOne(unittest.TestCase):
    def test_the_relay_unit_is_reported_as_a_relay_failure(self):
        d = pathlib.Path(tempfile.mkdtemp(prefix="relay-alert-"))
        try:
            (d / "env.prod").write_text("SMTP_HOST=mail.example.test\nSMTP_FROM_ADDRESS=portal@example.test\n"
                                        "BACKUP_ALERT_RECIPIENTS=ops@example.test\n", encoding="utf-8", newline="\n")
            env = dict(os.environ, ENV_FILE=shell_path(d / "env.prod"), BACKUP_ALERT_DRY_RUN="1")
            result = subprocess.run([BASH, str(ALERT), "failed", "hcs-portal-relay.service"], env=env,
                                    capture_output=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("Subject: Patient Portal relay: hcs-portal-relay.service failed", result.stdout)
        finally:
            shutil.rmtree(d, ignore_errors=True)


class TheUnitAndScriptShipCorrectly(unittest.TestCase):
    def test_git_tracks_the_script_executable(self):
        mode = subprocess.run(["git", "ls-files", "-s", str(SCRIPT.relative_to(REPO_ROOT))], cwd=REPO_ROOT,
                              capture_output=True, encoding="utf-8").stdout.split(" ")[0]
        self.assertEqual(mode, "100755")

    def test_the_unit_restarts_on_failure_and_alerts_past_its_start_limit(self):
        unit = UNIT.read_text(encoding="utf-8")
        self.assertIn("OnFailure=hcs-portal-backup-alert@%n.service", unit)
        self.assertIn("Restart=on-failure", unit)
        self.assertRegex(unit, r"StartLimitBurst=\d+")
        self.assertIn("ExecStart=/bin/bash /home/apadmin/hcs-patient-portal/scripts/hosting/relay-listener.sh", unit)


if __name__ == "__main__":
    unittest.main()
