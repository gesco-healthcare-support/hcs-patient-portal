"""scripts/hosting/sql-app-login.sh gives the application its own SQL Server login instead of sa.

What these tests pin (the script was also proven against a real SQL Server 2022 container by hand, 17 checks: the
login creates and owns an office database, is refused CREATE LOGIN, sysadmin and sp_configure, cannot open a
database sa created until --adopt, and a changed password in env.prod is refused, not applied):

  - the login is created with CREATE ANY DATABASE and NOTHING else server-wide; never sysadmin or dbcreator;
  - the app password reaches SQL Server on stdin or in the container environment, never on a command line;
  - unsafe settings are refused: the login sa, a short password, characters that break a connection string;
  - the script verifies the login's real rights and fails if they are wrong;
  - a password that no longer matches the existing login is an error, not a silent password change;
  - --adopt hands ownership of the portal databases (and only those) to the login;
  - compose falls back to sa when nothing is set, so the office server is unchanged.
"""

import os
import pathlib
import re
import secrets
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest

from shell_harness import BASH, shell_path

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "sql-app-login.sh"
COMPOSE = REPO_ROOT / "docker-compose.prod.yml"
SEP = "\x1f"

FAKE_DOCKER = r"""#!/usr/bin/env bash
{ printf '%s\x1f' "$@"; printf '\n'; } >> "$FAKE_DIR/docker.log"
case "$1" in
  compose) [[ "$*" == *"ps -q sql-server"* ]] && echo sqlcontainer; exit 0 ;;
  exec)
    if [[ "$*" == *"-U sa"* ]]; then
      cat >> "$FAKE_DIR/sa-stdin.log"; printf '\n---\n' >> "$FAKE_DIR/sa-stdin.log"
      exit "${FAKE_SA_RC:-0}"
    fi
    if [[ "${SQLCMDPASSWORD:-}" != "$FAKE_REAL_PASSWORD" ]]; then
      echo "Sqlcmd: Error: Login failed for user" >&2; exit 1
    fi
    echo "${FAKE_RIGHTS:-0,0,1,0,0}"; exit 0 ;;
esac
echo "fake docker: unexpected: $*" >&2
exit 2
"""


def strong_password():
    return "Pp-" + secrets.token_hex(10) + "Z9"


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class SqlAppLogin(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="sql-app-login-"))
        (self.dir / "bin").mkdir()
        path = self.dir / "bin" / "docker"
        path.write_text(FAKE_DOCKER, encoding="utf-8", newline="\n")
        path.chmod(path.stat().st_mode | stat.S_IEXEC)
        self.password = strong_password()
        self.write_env(login="portal_app", password=self.password)

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def write_env(self, login, password):
        lines = []
        if login is not None:
            lines.append(f"SQL_APP_LOGIN={login}")
        if password is not None:
            lines.append(f"SQL_APP_PASSWORD={password}")
        (self.dir / "env.prod").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")

    def run_script(self, *args, real_password=None, **extra):
        env = dict(os.environ, FAKE_DIR=shell_path(self.dir), ENV_FILE=shell_path(self.dir / "env.prod"),
                   FAKE_REAL_PASSWORD=self.password if real_password is None else real_password)
        env.update(extra)
        env["PATH"] = (shell_path(self.dir / "bin") + ":/usr/bin:/bin" if sys.platform == "win32"
                       else str(self.dir / "bin") + os.pathsep + os.environ.get("PATH", ""))
        result = subprocess.run([BASH, str(SCRIPT), *args], env=env, capture_output=True, encoding="utf-8",
                                errors="replace", timeout=60)
        return result.returncode, result.stdout + result.stderr

    def sa_sql(self):
        log = self.dir / "sa-stdin.log"
        return log.read_text(encoding="utf-8") if log.exists() else ""

    def argv(self):
        log = self.dir / "docker.log"
        return log.read_text(encoding="utf-8") if log.exists() else ""

    def test_create_grants_create_any_database_and_nothing_else(self):
        code, output = self.run_script()
        self.assertEqual(code, 0, output)
        sql = self.sa_sql()
        self.assertIn("CREATE LOGIN [portal_app] WITH PASSWORD = N'" + self.password + "', CHECK_POLICY = ON", sql)
        self.assertIn("IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'portal_app')", sql)
        self.assertEqual(re.findall(r"GRANT [A-Z ]+ TO", sql), ["GRANT CREATE ANY DATABASE TO"])
        for forbidden in ("sysadmin", "dbcreator", "ALTER SERVER ROLE", "sp_addsrvrolemember", "CONTROL SERVER"):
            self.assertNotIn(forbidden, sql)
        self.assertNotIn("ALTER LOGIN", sql, "an existing login's password must never be changed here")
        self.assertIn("verified", output)

    def test_the_app_password_is_never_on_a_command_line(self):
        self.assertEqual(self.run_script()[0], 0)
        self.assertNotIn(self.password, self.argv())
        self.assertNotIn(self.password, self.run_script()[1])

    def test_wrong_server_rights_fail_the_run(self):
        code, output = self.run_script(FAKE_RIGHTS="1,0,1,0,0")
        self.assertNotEqual(code, 0)
        self.assertIn("wrong server rights", output)

    def test_a_password_that_no_longer_matches_the_login_is_an_error_not_a_change(self):
        code, output = self.run_script(real_password="Pp-the-old-one-1234")
        self.assertNotEqual(code, 0)
        self.assertIn("do NOT change it here", output)

    def test_adopt_hands_the_portal_databases_to_the_login(self):
        code, output = self.run_script("--adopt")
        self.assertEqual(code, 0, output)
        sql = self.sa_sql()
        self.assertIn("ALTER AUTHORIZATION ON DATABASE::", sql)
        self.assertIn("name = N'CaseEvaluation' OR name LIKE N'CaseEvaluation[_]%'", sql)
        self.assertNotIn("CREATE LOGIN", sql)

    def test_unsafe_settings_are_refused_before_sql_server_is_touched(self):
        cases = {
            "sa": ("sa", strong_password(), "must not be sa"),
            "SA": ("SA", strong_password(), "must not be sa"),
            "missing login": (None, strong_password(), "SQL_APP_LOGIN"),
            "short": ("portal_app", "Pp-Short-1", "at least 16"),
            "no digits": ("portal_app", "Pp-OnlyLettersHereForSure", "upper case, lower case and digits"),
            "semicolon": ("portal_app", "Pp-" + secrets.token_hex(8) + ";X9", "must not contain"),
            "quote": ("portal_app", "Pp-" + secrets.token_hex(8) + "'X9", "must not contain"),
        }
        for name, (login, password, message) in cases.items():
            with self.subTest(case=name):
                self.write_env(login=login, password=password)
                code, output = self.run_script()
                self.assertNotEqual(code, 0)
                self.assertIn(message, output)
        self.assertEqual(self.sa_sql(), "")

    def test_unknown_arguments_are_refused(self):
        code, output = self.run_script("--drop")
        self.assertNotEqual(code, 0)
        self.assertIn("unknown argument", output)


class ComposeKeepsTheOfficeServerOnSa(unittest.TestCase):
    def test_every_service_takes_the_app_login_with_sa_as_the_fallback(self):
        compose = COMPOSE.read_text(encoding="utf-8")
        strings = re.findall(r'ConnectionStrings__Default: "([^"]+)"', compose)
        self.assertEqual(len(strings), 3)
        for value in strings:
            self.assertIn("User Id=${SQL_APP_LOGIN:-sa};", value)
            self.assertIn("Password=${SQL_APP_PASSWORD:-${MSSQL_SA_PASSWORD}};", value)
        self.assertNotIn("User Id=sa;", compose)

    def test_git_tracks_the_script_executable(self):
        mode = subprocess.run(["git", "ls-files", "-s", str(SCRIPT.relative_to(REPO_ROOT))], cwd=REPO_ROOT,
                              capture_output=True, encoding="utf-8").stdout.split(" ")[0]
        self.assertEqual(mode, "100755")


if __name__ == "__main__":
    unittest.main()
