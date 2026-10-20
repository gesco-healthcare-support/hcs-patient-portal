"""deploy-release.sh and rollback-release.sh stop the Relay listener around the reverse-proxy recreate.

The Relay listener (relay-listener.sh, Azure VM only) runs inside the reverse proxy's network namespace. Recreating
the proxy takes that namespace's network away, so the listener's connection cannot close, and Azure Relay kept
counting the dead listener for about four minutes (measured on the VM, 2026-10-20). A Case Tracker read routed to it
in that window fails. A clean stop closes the connection at once, so both scripts stop the listener before compose
touches the stack and start it again once the new proxy runs.

What these tests pin, for each script:

  - the stop comes before the first `up -d`, and the start comes after the proxy recreate;
  - every way out between the two starts the listener again before stopping the script, so a failed deploy or
    rollback does not leave the Case Tracker without documents;
  - on a host where the listener is not running (the office server, which has no such unit) nothing is stopped or
    started;
  - a listener the script did not stop is never started by it, and one it stopped is started once.

The behavioural tests run the scripts' own `relay_pause` / `relay_resume` functions, lifted out of each file as
text, against a fake `systemctl` and `sudo`, so they exercise what the scripts run rather than a copy. They need a
POSIX bash. CI runs on Linux. On Windows set TEST_BASH to Git Bash's plain shell,
C:/Program Files/Git/usr/bin/bash.exe, or the behavioural tests are skipped.
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
SCRIPTS = {
    "deploy": REPO_ROOT / "scripts" / "hosting" / "deploy-release.sh",
    "rollback": REPO_ROOT / "scripts" / "hosting" / "rollback-release.sh",
}
UNIT = "hcs-portal-relay.service"

# `systemctl is-active --quiet <unit>` exits 0 only when $FAKE_DIR/active exists; `stop` and `start` record the call
# and fail when $FAKE_DIR/fail-<verb> exists. `sudo` records itself and runs the rest.
FAKE_SYSTEMCTL = r"""#!/usr/bin/env bash
case "$1" in
  is-active) [ "$2" = --quiet ] || { echo "fake systemctl: unexpected: $*" >&2; exit 2; }
             [ -e "$FAKE_DIR/active" ] && exit 0 || exit 3 ;;
  stop|start) echo "systemctl $*" >> "$FAKE_DIR/calls"
              [ -e "$FAKE_DIR/fail-$1" ] && exit 1 || exit 0 ;;
  *) echo "fake systemctl: unexpected: $*" >&2; exit 2 ;;
esac
"""
FAKE_SUDO = r"""#!/usr/bin/env bash
echo -n "sudo " >> "$FAKE_DIR/calls"
exec "$@"
"""


def script_lines(script):
    return script.read_text(encoding="utf-8").splitlines()


def code_lines(script):
    return [line for line in script_lines(script) if line.strip() and not line.lstrip().startswith("#")]


def relay_block_source(script):
    """From the `RELAY_UNIT=` line to the `}` that closes `relay_resume`."""
    lines = script_lines(script)
    start = next(i for i, line in enumerate(lines) if line.startswith("RELAY_UNIT="))
    resume = next(i for i in range(start, len(lines)) if lines[i].startswith("relay_resume() {"))
    end = next(i for i in range(resume, len(lines)) if lines[i] == "}")
    return "\n".join(lines[start:end + 1]) + "\n"


class TheListenerIsStoppedAroundTheRecreate(unittest.TestCase):
    def test_each_script_defines_the_block_once_for_the_relay_unit(self):
        for name, script in SCRIPTS.items():
            with self.subTest(script=name):
                lines = code_lines(script)
                self.assertEqual([line for line in lines if line.startswith("RELAY_UNIT=")],
                                 [f"RELAY_UNIT={UNIT}"])
                self.assertEqual(sum(line.startswith("relay_pause() {") for line in lines), 1)
                self.assertEqual(sum(line.startswith("relay_resume() {") for line in lines), 1)

    def test_the_stop_comes_before_the_first_up_and_the_start_after_the_recreate(self):
        for name, script in SCRIPTS.items():
            with self.subTest(script=name):
                lines = code_lines(script)
                pause = [i for i, line in enumerate(lines) if line.startswith("relay_pause ||")]
                resume = [i for i, line in enumerate(lines) if line.startswith("relay_resume ||")]
                ups = [i for i, line in enumerate(lines) if line.startswith("$C up -d")]
                recreate = next(i for i, line in enumerate(lines) if "--force-recreate reverse-proxy" in line)
                self.assertEqual(len(pause), 1, pause)
                self.assertEqual(len(resume), 1, resume)
                self.assertLess(pause[0], min(ups))
                self.assertGreater(resume[0], recreate)
                self.assertTrue(all(pause[0] < i < resume[0] for i in ups), (pause, ups, resume))

    def test_every_way_out_between_the_two_starts_the_listener_again(self):
        for name, script in SCRIPTS.items():
            with self.subTest(script=name):
                lines = code_lines(script)
                pause = next(i for i, line in enumerate(lines) if line.startswith("relay_pause ||"))
                resume = next(i for i, line in enumerate(lines) if line.startswith("relay_resume ||"))
                exits = [lines[i] for i in range(pause + 1, resume) if "stop " in lines[i]]
                self.assertTrue(exits, "expected the up -d and recreate failure paths")
                for line in exits:
                    self.assertIn("relay_resume; stop ", line)

    def test_a_failed_start_is_reported_rather_than_ignored(self):
        for name, script in SCRIPTS.items():
            with self.subTest(script=name):
                line = next(line for line in code_lines(script) if line.startswith("relay_resume ||"))
                self.assertIn("WARNING", line)
                self.assertIn("sudo systemctl start $RELAY_UNIT", line)


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class TheFunctionsStopAndStartOnlyWhatTheyMust(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="deploy-relay-pause-"))
        (self.dir / "bin").mkdir()
        for name, body in (("systemctl", FAKE_SYSTEMCTL), ("sudo", FAKE_SUDO)):
            fake = self.dir / "bin" / name
            fake.write_text(body, encoding="utf-8", newline="\n")
            fake.chmod(fake.stat().st_mode | stat.S_IEXEC)

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def mark(self, name):
        (self.dir / name).write_text("", encoding="utf-8")

    def run_steps(self, script, steps):
        (self.dir / "calls").unlink(missing_ok=True)  # each run starts with no recorded calls
        harness = self.dir / "harness.sh"
        body = "".join(f'{step}; echo "{step}=$?"\n' for step in steps)
        harness.write_text("set -u\nts() { echo T; }\n" + relay_block_source(script) + body,
                           encoding="utf-8", newline="\n")
        env = dict(os.environ, FAKE_DIR=shell_path(self.dir))
        env["PATH"] = (shell_path(self.dir / "bin") + ":/usr/bin:/bin" if sys.platform == "win32"
                       else str(self.dir / "bin") + os.pathsep + os.environ.get("PATH", ""))
        result = subprocess.run([BASH, shell_path(harness)], env=env, capture_output=True, encoding="utf-8",
                                errors="replace", timeout=60)
        output = result.stdout + result.stderr
        self.assertNotIn("fake systemctl: unexpected", output)
        codes = dict(line.split("=", 1) for line in output.splitlines()
                     if line.startswith(("relay_pause=", "relay_resume=")))
        calls_file = self.dir / "calls"
        calls = calls_file.read_text(encoding="utf-8").splitlines() if calls_file.exists() else []
        return codes, calls, output

    def test_a_host_without_a_running_listener_is_left_alone(self):
        for name, script in SCRIPTS.items():
            with self.subTest(script=name):
                codes, calls, output = self.run_steps(script, ["relay_pause", "relay_resume"])
                self.assertEqual(codes, {"relay_pause": "0", "relay_resume": "0"}, output)
                self.assertEqual(calls, [])

    def test_a_running_listener_is_stopped_then_started_once(self):
        self.mark("active")
        for name, script in SCRIPTS.items():
            with self.subTest(script=name):
                codes, calls, output = self.run_steps(script, ["relay_pause", "relay_resume", "relay_resume"])
                self.assertEqual(codes["relay_pause"], "0", output)
                self.assertEqual(codes["relay_resume"], "0", output)
                self.assertEqual(calls, [f"sudo systemctl stop {UNIT}", f"sudo systemctl start {UNIT}"])
                self.assertIn("relay listener stopped for the proxy recreate", output)
                self.assertIn("relay listener started again", output)

    def test_a_failed_stop_is_reported_and_nothing_is_started(self):
        self.mark("active")
        self.mark("fail-stop")
        for name, script in SCRIPTS.items():
            with self.subTest(script=name):
                codes, calls, output = self.run_steps(script, ["relay_pause", "relay_resume"])
                self.assertEqual(codes, {"relay_pause": "1", "relay_resume": "0"}, output)
                self.assertEqual(calls, [f"sudo systemctl stop {UNIT}"])

    def test_a_failed_start_is_reported(self):
        self.mark("active")
        self.mark("fail-start")
        for name, script in SCRIPTS.items():
            with self.subTest(script=name):
                codes, calls, output = self.run_steps(script, ["relay_pause", "relay_resume"])
                self.assertEqual(codes, {"relay_pause": "0", "relay_resume": "1"}, output)
                self.assertEqual(calls, [f"sudo systemctl stop {UNIT}", f"sudo systemctl start {UNIT}"])
                self.assertNotIn("relay listener started again", output)


if __name__ == "__main__":
    unittest.main()
