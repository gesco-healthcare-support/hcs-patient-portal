"""deploy-release.sh checks, after `up`, that both web hosts RUN as Production.

It replaced a probe that printed `App__DisablePII` and wanted "true". That setting stopped deciding anything outside
Development (IdentityModelPiiLogging decides by the environment), so on a correct server the variable is empty and
the probe could only raise a false alarm. The environment name is what now keeps PII logging and the API
documentation off, so the deploy reads it from the running containers.

What these tests pin:

  - ASPNETCORE_ENVIRONMENT must be exactly Production, and an EMPTY value fails. `printenv` prints nothing for an
    unset variable, so a check written as "not Development" would pass a host it could not read at all;
  - DOTNET_ENVIRONMENT may be unset or Production, and nothing else (the same rule as the compose-config check);
  - a container that is not running is a failure, not a blank pass;
  - the result reaches the end of the log: a wrong value is repeated just above the DEPLOY DONE line.

The behavioural tests run the script's own `check_web_env` function, lifted out of the file as text, against a
fake `docker`, so they exercise what the deploy runs rather than a copy of it. They need a POSIX bash. CI runs on
Linux. On Windows set TEST_BASH to Git Bash's plain shell, C:/Program Files/Git/usr/bin/bash.exe, or the
behavioural tests are skipped.
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
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "deploy-release.sh"
API_CONTAINER = "hcs-patient-portal-api-1"
SIGN_IN_CONTAINER = "hcs-patient-portal-authserver-1"

# `docker exec <container> printenv <VAR>`, as the real one behaves: a set variable prints its value and a newline,
# an unset one prints nothing and exits 1, and a stopped container is an error on stderr.
FAKE_DOCKER = r"""#!/usr/bin/env bash
[ "$1" = exec ] && [ "$3" = printenv ] || { echo "fake docker: unexpected: $*" >&2; exit 2; }
if [ -e "$FAKE_DIR/down/$2" ]; then echo "Error response from daemon: container $2 is not running" >&2; exit 1; fi
f="$FAKE_DIR/env/$2/$4"
[ -f "$f" ] || exit 1
cat "$f"; echo
"""


def script_lines():
    return SCRIPT.read_text(encoding="utf-8").splitlines()


def code_lines():
    return [line for line in script_lines() if line.strip() and not line.lstrip().startswith("#")]


def check_function_source():
    """The `check_web_env` definition, from its opening line to the first line that is exactly `}`."""
    lines = script_lines()
    start = next(i for i, line in enumerate(lines) if line.startswith("check_web_env() {"))
    end = next(i for i in range(start, len(lines)) if lines[i] == "}")
    return "\n".join(lines[start:end + 1]) + "\n"


class TheProbeIsWiredIn(unittest.TestCase):
    def test_the_retired_disable_pii_probe_is_gone(self):
        self.assertEqual([line for line in code_lines() if "DisablePII" in line], [])

    def test_the_check_runs_once_after_the_stack_is_up(self):
        lines = code_lines()
        calls = [i for i, line in enumerate(lines) if "check_web_env ||" in line]
        self.assertEqual(len(calls), 1, calls)
        proxy = next(i for i, line in enumerate(lines) if "--force-recreate reverse-proxy" in line)
        self.assertGreater(calls[0], proxy)

    def test_a_wrong_value_is_repeated_just_above_the_last_line(self):
        lines = code_lines()
        done = next(i for i, line in enumerate(lines) if "DEPLOY DONE" in line)
        self.assertEqual(done, len(lines) - 1, "DEPLOY DONE must stay the last line")
        warning = lines[done - 1]
        self.assertTrue(warning.startswith('[ "$webenv" = OK ] ||'), warning)
        self.assertIn("NOT RUNNING AS Production", warning)
        self.assertIn("rollback-release.sh $PREV", warning)


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class TheCheckReadsTheRunningContainers(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="deploy-web-env-"))
        (self.dir / "bin").mkdir()
        docker = self.dir / "bin" / "docker"
        docker.write_text(FAKE_DOCKER, encoding="utf-8", newline="\n")
        docker.chmod(docker.stat().st_mode | stat.S_IEXEC)
        harness = self.dir / "harness.sh"
        harness.write_text("set -u\n" + check_function_source() + 'check_web_env; echo "RC=$?"\n',
                           encoding="utf-8", newline="\n")
        self.harness = harness

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def set_env(self, container, name, value):
        folder = self.dir / "env" / container
        folder.mkdir(parents=True, exist_ok=True)
        (folder / name).write_text(value, encoding="utf-8", newline="\n")

    def stop(self, container):
        (self.dir / "down").mkdir(exist_ok=True)
        (self.dir / "down" / container).write_text("", encoding="utf-8")

    def both_production(self):
        self.set_env(API_CONTAINER, "ASPNETCORE_ENVIRONMENT", "Production")
        self.set_env(SIGN_IN_CONTAINER, "ASPNETCORE_ENVIRONMENT", "Production")

    def run_check(self):
        env = dict(os.environ, FAKE_DIR=shell_path(self.dir))
        env["PATH"] = (shell_path(self.dir / "bin") + ":/usr/bin:/bin" if sys.platform == "win32"
                       else str(self.dir / "bin") + os.pathsep + os.environ.get("PATH", ""))
        result = subprocess.run([BASH, str(self.harness)], env=env, capture_output=True, encoding="utf-8",
                                errors="replace", timeout=60)
        output = result.stdout + result.stderr
        self.assertNotIn("fake docker: unexpected", output)
        lines = output.splitlines()
        rc = next(line for line in lines if line.startswith("RC="))
        hosts = {line.split()[0]: line for line in lines if " environment:" in line}
        self.assertEqual(sorted(hosts), ["api", "auth"], output)
        return rc, hosts

    def test_both_hosts_on_production_pass(self):
        self.both_production()
        rc, hosts = self.run_check()
        self.assertEqual(rc, "RC=0")
        for line in hosts.values():
            self.assertIn("ASPNETCORE_ENVIRONMENT=[Production] DOTNET_ENVIRONMENT=[]", line)
            self.assertTrue(line.endswith("  OK"), line)

    def test_dotnet_environment_set_to_production_also_passes(self):
        self.both_production()
        self.set_env(API_CONTAINER, "DOTNET_ENVIRONMENT", "Production")
        rc, hosts = self.run_check()
        self.assertEqual(rc, "RC=0")
        self.assertTrue(hosts["api"].endswith("  OK"), hosts["api"])

    def test_an_unset_aspnetcore_environment_fails_rather_than_passing_on_nothing(self):
        self.set_env(SIGN_IN_CONTAINER, "ASPNETCORE_ENVIRONMENT", "Production")
        rc, hosts = self.run_check()
        self.assertEqual(rc, "RC=1")
        self.assertIn("ASPNETCORE_ENVIRONMENT=[] ", hosts["api"])
        self.assertIn("WRONG", hosts["api"])
        self.assertTrue(hosts["auth"].endswith("  OK"), hosts["auth"])

    def test_any_aspnetcore_value_other_than_production_fails(self):
        for value in ("Development", "Staging", ""):
            with self.subTest(value=value):
                self.set_env(API_CONTAINER, "ASPNETCORE_ENVIRONMENT", "Production")
                self.set_env(SIGN_IN_CONTAINER, "ASPNETCORE_ENVIRONMENT", value)
                rc, hosts = self.run_check()
                self.assertEqual(rc, "RC=1")
                self.assertIn("WRONG", hosts["auth"])
                self.assertTrue(hosts["api"].endswith("  OK"), hosts["api"])

    def test_dotnet_environment_other_than_production_fails_even_when_aspnetcore_is_right(self):
        for value in ("Development", "Staging"):
            with self.subTest(value=value):
                self.both_production()
                self.set_env(API_CONTAINER, "DOTNET_ENVIRONMENT", value)
                rc, hosts = self.run_check()
                self.assertEqual(rc, "RC=1")
                self.assertIn(f"DOTNET_ENVIRONMENT=[{value}]", hosts["api"])
                self.assertIn("WRONG", hosts["api"])

    def test_a_stopped_container_fails_and_shows_why(self):
        self.both_production()
        self.stop(SIGN_IN_CONTAINER)
        rc, hosts = self.run_check()
        self.assertEqual(rc, "RC=1")
        self.assertIn("is not running", hosts["auth"])
        self.assertIn("WRONG", hosts["auth"])
        self.assertTrue(hosts["api"].endswith("  OK"), hosts["api"])


if __name__ == "__main__":
    unittest.main()
