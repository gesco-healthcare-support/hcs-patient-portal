"""scripts/hosting/rehearse-migrator.sh sets up the app's own SQL login on its scratch server when the migrator uses one.

The deploy rehearsal restores the newest backups into a FRESH SQL Server container and runs the new migrator image
against it. Where env.prod sets SQL_APP_LOGIN (the Azure VM), the migrator connects as that login, not sa
(docker-compose.prod.yml), and a database restore brings no server logins -- so without this the rehearsal's
migrator could not sign in and every VM deploy would stop at the rehearsal gate.

What these tests pin, by running the real script end to end against fakes (docker, sudo, free, python3, and a
stand-in sql-app-login.sh that records how it was called):

  - with an app login, sql-app-login.sh runs against the SCRATCH container, first to create the login and then with
    --adopt, AFTER every restore (adopt needs the restored databases) and BEFORE the migrator starts;
  - with sa -- no SQL_APP_LOGIN, the office server -- it is never called, whatever the case of "sa";
  - a failure of either step stops the rehearsal before the migrator starts, naming the login and the step;
  - the password is never printed: not the one inside the migrator's connection string, and not one that a noisy
    error from SQL Server might echo -- only sql-app-login.sh's own timestamped log lines are passed through;
  - a connection string with no login is a failure, not a silent skip.
"""

import json
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
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "rehearse-migrator.sh"
SCRATCH_SQL = "hcs-rehearsal-sql"
STAMP = "20261020-013128"
DATABASES = ("CaseEvaluation", "CaseEvaluation_sandbox")

# Records the calls that matter, in order, to $FAKE_DIR/calls. Every other docker call succeeds quietly.
FAKE_DOCKER = r"""#!/usr/bin/env bash
case "$1" in
  compose) [[ "$*" == *"config --format json"* ]] && cat "$FAKE_DIR/config.json"; exit 0 ;;
  image) echo "2026-10-20T00:00:00Z"; exit 0 ;;
  exec)
    if [[ "$3" == bash ]]; then
      q="$6"
      case "$q" in
        *"SELECT 1"*) echo 1 ;;
        *"RESTORE FILELISTONLY"*) printf 'Data|/d.mdf|D\nLog|/l.ldf|L\n' ;;
        *"RESTORE DATABASE"*) db="${q#*RESTORE DATABASE [}"; echo "restore ${db%%]*}" >> "$FAKE_DIR/calls" ;;
      esac
    fi
    exit 0 ;;
  run) [[ "$*" == *"--name hcs-rehearsal-migrator"* ]] && echo "migrator-run" >> "$FAKE_DIR/calls"; exit 0 ;;
  inspect)
    [[ "$*" == *State.Running* ]] && echo false
    [[ "$*" == *State.ExitCode* ]] && echo 0
    exit 0 ;;
  logs) echo "Successfully completed all database migrations"; exit 0 ;;
esac
exit 0
"""

# `sudo docker ...` reaches the fake docker; any other sudo (chown, chmod, rm of the throwaway folder) is a no-op.
FAKE_SUDO = r"""#!/usr/bin/env bash
[ "$1" = docker ] && { shift; exec docker "$@"; }
exit 0
"""

FAKE_FREE = """#!/usr/bin/env bash
echo "Mem: 16000 8000 0 0 0 6000"
"""

# Stands in for scripts/hosting/sql-app-login.sh in the fake checkout. It records its arguments and the container it was
# pointed at, prints one of its own log lines, then a line like a noisy SQL Server error that repeats the password, and
# exits with the code in $FAKE_DIR/applogin-rc-<step>, if that file exists.
FAKE_APP_LOGIN = r"""#!/usr/bin/env bash
step="${1:-create}"
echo "applogin ${step} container=${SQL_CONTAINER:-UNSET}" >> "$FAKE_DIR/calls"
echo "[2026-10-20T00:00:00Z] sql-app-login step ${step}"
echo "Msg 15118, Level 16: Password validation failed for '${FAKE_PASSWORD}'" >&2
rc=0
[ -f "$FAKE_DIR/applogin-rc-${step#--}" ] && rc=$(cat "$FAKE_DIR/applogin-rc-${step#--}")
exit "$rc"
"""


def strong_password():
    return "Pp-" + secrets.token_hex(10) + "Z9"


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class RehearsalAppLogin(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="rehearse-app-login-"))
        self.home = self.dir / "home"
        self.checkout = self.home / "hcs-patient-portal"
        (self.checkout / "secrets").mkdir(parents=True)
        (self.checkout / "backups").mkdir()
        (self.checkout / "scripts" / "hosting").mkdir(parents=True)
        (self.dir / "tmp").mkdir()
        (self.dir / "bin").mkdir()
        for db in DATABASES:
            (self.checkout / "backups" / f"{db}_{STAMP}.bak").write_bytes(b"bak")
        self.password = strong_password()
        self.sa_password = strong_password()
        python = shell_path(sys.executable) if sys.platform == "win32" else sys.executable
        for name, body in (("docker", FAKE_DOCKER), ("sudo", FAKE_SUDO), ("free", FAKE_FREE),
                           ("python3", f'#!/usr/bin/env bash\nexec "{python}" "$@"\n')):
            self.executable(self.dir / "bin" / name, body)
        self.executable(self.checkout / "scripts" / "hosting" / "sql-app-login.sh", FAKE_APP_LOGIN)
        self.configure(login="portal_app")

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    @staticmethod
    def executable(path, body):
        path.write_text(body, encoding="utf-8", newline="\n")
        path.chmod(path.stat().st_mode | stat.S_IEXEC)

    def configure(self, login):
        """The compose config the script reads, and the matching env.prod. login=None means no SQL_APP_LOGIN, where
        compose falls back to sa."""
        user = "sa" if login is None else login
        password = self.sa_password if login is None else self.password
        config = {"services": {
            "sql-server": {"image": "mssql-image", "environment": {"MSSQL_SA_PASSWORD": self.sa_password}},
            "db-migrator": {"environment": {
                "ConnectionStrings__Default":
                    f"Server=sql-server;Database=CaseEvaluation;User Id={user};Password={password};"
                    "TrustServerCertificate=True",
                "DOTNET_ENVIRONMENT": "Production"}},
        }}
        (self.dir / "config.json").write_text(json.dumps(config), encoding="utf-8", newline="\n")
        lines = [f"MSSQL_SA_PASSWORD={self.sa_password}"]
        if login is not None:
            lines += [f"SQL_APP_LOGIN={login}", f"SQL_APP_PASSWORD={self.password}"]
        (self.checkout / "secrets" / "env.prod").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")

    def set_connection_string(self, value):
        config = json.loads((self.dir / "config.json").read_text(encoding="utf-8"))
        config["services"]["db-migrator"]["environment"]["ConnectionStrings__Default"] = value
        (self.dir / "config.json").write_text(json.dumps(config), encoding="utf-8", newline="\n")

    def run_script(self):
        env = dict(os.environ, HOME=shell_path(self.home), TMPDIR=shell_path(self.dir / "tmp"),
                   FAKE_DIR=shell_path(self.dir), FAKE_PASSWORD=self.password)
        env["PATH"] = (shell_path(self.dir / "bin") + ":/usr/bin:/bin" if sys.platform == "win32"
                       else str(self.dir / "bin") + os.pathsep + os.environ.get("PATH", ""))
        result = subprocess.run([BASH, str(SCRIPT)], env=env, capture_output=True, encoding="utf-8",
                                errors="replace", timeout=120, stdin=subprocess.DEVNULL)
        return result.returncode, result.stdout + result.stderr

    def calls(self):
        path = self.dir / "calls"
        return path.read_text(encoding="utf-8").splitlines() if path.exists() else []

    def test_an_app_login_is_created_then_adopted_on_the_scratch_server_between_the_restores_and_the_migrator(self):
        rc, out = self.run_script()

        self.assertEqual(rc, 0, out)
        self.assertIn("REHEARSAL PASS", out)
        self.assertEqual(self.calls(), [
            *(f"restore {db}" for db in DATABASES),
            f"applogin create container={SCRATCH_SQL}",
            f"applogin --adopt container={SCRATCH_SQL}",
            "migrator-run",
        ])
        self.assertIn("app login portal_app created and owns the restored databases", out)

    def test_sql_app_login_logs_are_passed_through(self):
        _, out = self.run_script()

        self.assertIn("[2026-10-20T00:00:00Z] sql-app-login step create", out)
        self.assertIn("[2026-10-20T00:00:00Z] sql-app-login step --adopt", out)

    def test_the_office_server_sa_fallback_needs_no_login(self):
        # No SQL_APP_LOGIN in env.prod: compose's connection string falls back to sa, as on the office server.
        self.configure(login=None)

        rc, out = self.run_script()

        self.assertEqual(rc, 0, out)
        self.assertIn("REHEARSAL PASS", out)
        self.assertEqual(self.calls(), [*(f"restore {db}" for db in DATABASES), "migrator-run"])
        self.assertNotIn("app login", out)

    def test_sa_is_recognised_in_any_case(self):
        self.set_connection_string(
            f"Server=sql-server;Database=CaseEvaluation;USER ID=SA;Password={self.sa_password};TrustServerCertificate=True")

        rc, out = self.run_script()

        self.assertEqual(rc, 0, out)
        self.assertNotIn("applogin create container=" + SCRATCH_SQL, self.calls())

    def test_a_failed_create_stops_before_the_migrator_and_names_the_step(self):
        (self.dir / "applogin-rc-create").write_text("1", encoding="utf-8")

        rc, out = self.run_script()

        self.assertNotEqual(rc, 0)
        self.assertIn("REHEARSAL FAIL", out)
        self.assertIn("app login portal_app", out)
        self.assertIn("sql-app-login.sh create, exit 1", out)
        self.assertNotIn("migrator-run", self.calls())
        self.assertNotIn(f"applogin --adopt container={SCRATCH_SQL}", self.calls())

    def test_a_failed_adopt_stops_before_the_migrator_and_names_the_step(self):
        (self.dir / "applogin-rc-adopt").write_text("3", encoding="utf-8")

        rc, out = self.run_script()

        self.assertNotEqual(rc, 0)
        self.assertIn("sql-app-login.sh --adopt, exit 3", out)
        self.assertNotIn("migrator-run", self.calls())

    def test_the_password_is_never_printed(self):
        # Both on the passing path and on a failing one: the noisy line the stand-in prints repeats the password, and
        # the migrator's connection string in the compose config carries it too.
        for failing in (False, True):
            with self.subTest(failing=failing):
                rc_file = self.dir / "applogin-rc-create"
                if failing:
                    rc_file.write_text("1", encoding="utf-8")
                elif rc_file.exists():
                    rc_file.unlink()

                _, out = self.run_script()

                self.assertNotIn(self.password, out)
                self.assertNotIn(self.sa_password, out)

    def test_a_connection_string_with_no_login_is_a_failure_not_a_skip(self):
        self.set_connection_string("Server=sql-server;Database=CaseEvaluation;TrustServerCertificate=True")

        rc, out = self.run_script()

        self.assertNotEqual(rc, 0)
        self.assertIn("could not read the migrator's SQL login", out)
        self.assertNotIn("migrator-run", self.calls())


if __name__ == "__main__":
    unittest.main()
