"""scripts/hosting/vm-env-prod.py writes the Azure VM's env file and refuses one that would bring it up unsafe.

What these tests pin:

  - GENERATE builds from env.prod.example, and EVERY key there has a decision: a new key, or a decision for a key
    the example dropped, stops it (exit 2), so the generator cannot drift from the documented settings;
  - the VM's decisions: both vhosts "deny all;" (EMPTY would open them), Express, the app's own SQL login, the
    push off, ./backups, Production for the migrator, Relay still commented;
  - generated secrets fit what consumes them: MinIO 20 / 40 characters, SQL Server's password policy with nothing
    that breaks a connection string, two DIFFERENT Case Tracker tokens;
  - only what a person must supply is left as CHANGE_ME; the file is created mode 600 and never overwritten;
  - CHECK refuses each unsafe setting, with exit code 1, and names keys, never values. Bad input exits 2;
  - neither command prints a secret.

The tests call main() in this process, not a subprocess, so the script's coverage is measured.
"""

import argparse
import contextlib
import hashlib
import io
import os
import re
import shutil
import stat
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from gate_loader import load

vm_env = load("vm_env_prod", "scripts/hosting/vm-env-prod.py")

EXAMPLE_TEXT = vm_env.EXAMPLE.read_text(encoding="utf-8")
HUMAN_KEYS = {key for key, action in vm_env.PLAN.items() if action == vm_env.HUMAN}
ALERT_KEYS = {key for key, action in vm_env.PLAN.items() if action == vm_env.ALERT}
SECRET_KEYS = {key for key, action in vm_env.PLAN.items() if action == vm_env.SECRET}


def options(**overrides):
    values = {"base_domain": vm_env.DEFAULT_BASE_DOMAIN, "alert_email": ""}
    values.update(overrides)
    return argparse.Namespace(**values)


def generated(alert_email=""):
    text, made = vm_env.render(EXAMPLE_TEXT, options(alert_email=alert_email))
    return text, made


def filled():
    """A generated file with every CHANGE_ME given a synthetic value, as a person would."""
    text, _ = generated("ops@example.com")
    synthetic = {"AZURE_BACKUP_STORAGE_ACCOUNT": "stportalbackup01"}
    return re.sub(r"^([A-Z_]+)=CHANGE_ME\S*$",
                  lambda m: f"{m.group(1)}={synthetic.get(m.group(1), 'synthetic-' + m.group(1).lower())}",
                  text, flags=re.M)


def with_value(text, key, value):
    """The file with KEY's line replaced, or appended when absent; value None removes the line."""
    lines = [line for line in text.splitlines() if not line.startswith(f"{key}=")]
    if value is not None:
        lines.append(f"{key}={value}")
    return "\n".join(lines) + "\n"


def run(*argv):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        code = vm_env.main(list(argv))
    return code, out.getvalue() + err.getvalue()


class GenerateAccountsForEveryExampleKey(unittest.TestCase):
    def test_every_key_of_the_real_example_has_a_decision(self):
        text, made = generated()
        self.assertEqual(set(made), SECRET_KEYS)
        self.assertTrue(text.endswith("\n"))

    def test_a_key_the_script_does_not_know_stops_it(self):
        with self.assertRaises(vm_env.BadInput) as raised:
            vm_env.render(EXAMPLE_TEXT + "\nBRAND_NEW_SETTING=1\n", options())
        self.assertIn("BRAND_NEW_SETTING", str(raised.exception))

    def test_an_optional_key_the_script_does_not_know_stops_it_too(self):
        with self.assertRaises(vm_env.BadInput):
            vm_env.render(EXAMPLE_TEXT + "\n# BRAND_NEW_OPTION=\n", options())

    def test_a_decision_for_a_key_the_example_dropped_stops_it(self):
        trimmed = "\n".join(l for l in EXAMPLE_TEXT.splitlines() if not l.startswith("CLAMAV_MEM_LIMIT="))
        with self.assertRaises(vm_env.BadInput) as raised:
            vm_env.render(trimmed, options())
        self.assertIn("CLAMAV_MEM_LIMIT", str(raised.exception))

    def test_a_key_named_twice_in_the_example_stops_it(self):
        with self.assertRaises(vm_env.BadInput):
            vm_env.render(EXAMPLE_TEXT + "\nAPP_NAME=again\n", options())


class GenerateWritesTheVmDecisions(unittest.TestCase):
    def setUp(self):
        self.values = vm_env.parse(generated()[0])[0]

    def test_both_vhosts_are_closed_rather_than_empty(self):
        self.assertEqual(self.values["MINIO_ALLOW_DIRECTIVES"], "deny all;")
        self.assertEqual(self.values["DOCS_ALLOW_DIRECTIVES"], "deny all;")

    def test_the_server_settings(self):
        expected = {"MSSQL_PID": "Express", "SQL_APP_LOGIN": "portal_app", "BACKUP_DIR": "./backups",
                    "DBMIGRATOR_ENVIRONMENT": "Production", "BASE_DOMAIN": "medlegal.evaluators.com",
                    "CASE_TRACKER_BASE_URL": "", "CASE_TRACKER_INTAKE_TOKEN": "", "ADMIN_PASSWORD_VAULT_URI": "",
                    "TRUSTED_PROXY_SET_REAL_IP_FROM": "", "TRUSTED_PROXY_REAL_IP_RECURSIVE": "",
                    "ADMIN_PASSWORD_DIRECTORY": "/run/admin-passwords", "ACME_CHALLENGE": "dns-01"}
        self.assertEqual({key: self.values.get(key) for key in expected}, expected)

    def test_what_stays_off_stays_commented(self):
        for key in ("MINIO_REGION", "RELAY_NAMESPACE", "RELAY_LISTEN_CONNECTION_STRING_FILE",
                    "AZURE_BRIDGE_CONNECTIONSTRING"):
            self.assertNotIn(key, self.values)

    def test_the_base_domain_is_a_setting(self):
        text, _ = vm_env.render(EXAMPLE_TEXT, options(base_domain="portal.example.com"))
        self.assertEqual(vm_env.parse(text)[0]["BASE_DOMAIN"], "portal.example.com")


class GeneratedSecretsFitTheirConsumers(unittest.TestCase):
    def test_limits_hold_on_every_draw(self):
        for _ in range(25):
            _, made = generated()
            self.assertLessEqual(len(made["MINIO_ROOT_USER"]), 20)
            self.assertLessEqual(len(made["MINIO_APP_ACCESS_KEY"]), 20)
            self.assertTrue(8 <= len(made["MINIO_ROOT_PASSWORD"]) <= 40)
            self.assertTrue(8 <= len(made["MINIO_APP_SECRET_KEY"]) <= 40)
            self.assertNotEqual(made["MINIO_APP_ACCESS_KEY"], made["MINIO_ROOT_USER"])
            self.assertNotEqual(made["CASE_TRACKER_FEED_TOKEN"], made["CASE_TRACKER_INTEGRATION_TOKEN"])
            self.assertRegex(made["CASE_TRACKER_FEED_TOKEN"], r"^[0-9a-f]{64}$")
            self.assertEqual(len(set(made.values())), len(made), "every secret is its own value")
            self.assertEqual(vm_env._sql(made), [])
            self.assertEqual(vm_env._lengths(made), [])

    def test_equal_tokens_from_the_random_source_are_refused(self):
        same = {"CASE_TRACKER_FEED_TOKEN": lambda: "same", "CASE_TRACKER_INTEGRATION_TOKEN": lambda: "same"}
        with mock.patch.dict(vm_env.SECRET_MAKERS, same), self.assertRaises(vm_env.BadInput):
            generated()


class GenerateLeavesOnlyWhatAPersonSupplies(unittest.TestCase):
    def test_placeholders_are_exactly_the_human_and_alert_keys(self):
        values = vm_env.parse(generated()[0])[0]
        left = {key for key, value in values.items() if vm_env.PLACEHOLDER in value}
        self.assertEqual(left, HUMAN_KEYS | ALERT_KEYS)

    def test_the_alert_address_fills_all_three_alert_settings(self):
        values = vm_env.parse(generated("ops@example.com")[0])[0]
        self.assertEqual({values[key] for key in ALERT_KEYS}, {"ops@example.com"})

    def test_a_filled_in_file_passes_the_check(self):
        self.assertEqual(vm_env.check_text(filled()), [])


class GenerateOnDisk(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp(prefix="vm-env-"))
        self.out = self.dir / "env.prod"

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def test_writes_a_new_file_and_never_overwrites_it(self):
        code, output = run("generate", "--out", str(self.out))
        self.assertEqual(code, 0, output)
        self.assertIn("still to fill in: ABP_LICENSE_CODE", output)
        before = hashlib.sha256(self.out.read_bytes()).hexdigest()
        code, output = run("generate", "--out", str(self.out))
        self.assertEqual(code, 1)
        self.assertIn("already exists", output)
        self.assertEqual(hashlib.sha256(self.out.read_bytes()).hexdigest(), before)

    @unittest.skipUnless(os.name == "posix", "file modes are POSIX")
    def test_the_file_is_mode_600(self):
        self.assertEqual(run("generate", "--out", str(self.out))[0], 0)
        self.assertEqual(stat.S_IMODE(self.out.stat().st_mode), 0o600)

    def test_a_missing_directory_is_bad_input(self):
        code, output = run("generate", "--out", str(self.dir / "no-such-dir" / "env.prod"))
        self.assertEqual(code, 2)
        self.assertIn("install -d -m 700", output)

    def test_an_unaccounted_example_is_bad_input(self):
        example = self.dir / "example"
        example.write_text(EXAMPLE_TEXT + "\nBRAND_NEW_SETTING=1\n", encoding="utf-8")
        self.assertEqual(run("generate", "--out", str(self.out), "--example", str(example))[0], 2)
        self.assertFalse(self.out.exists())

    def test_no_generated_secret_is_printed(self):
        code, output = run("generate", "--out", str(self.out))
        self.assertEqual(code, 0)
        values = vm_env.parse(self.out.read_text(encoding="utf-8"))[0]
        code, check_output = run("check", str(self.out))
        self.assertEqual(code, 1)  # the placeholders are still there
        for key in SECRET_KEYS:
            self.assertNotIn(values[key], output + check_output, key)


class CheckRefusesAnUnsafeFile(unittest.TestCase):
    def setUp(self):
        self.good = filled()

    def problems_with(self, key, value):
        return vm_env.check_text(with_value(self.good, key, value))

    def assert_refused(self, key, value, fragment):
        problems = self.problems_with(key, value)
        self.assertTrue(any(fragment in problem for problem in problems), (key, value, problems))

    def test_equal_feed_and_integration_tokens(self):
        token = vm_env.parse(self.good)[0]["CASE_TRACKER_INTEGRATION_TOKEN"]
        self.assert_refused("CASE_TRACKER_FEED_TOKEN", token, "equals CASE_TRACKER_INTEGRATION_TOKEN")

    def test_a_vhost_that_is_not_exactly_deny_all(self):
        for key in ("MINIO_ALLOW_DIRECTIVES", "DOCS_ALLOW_DIRECTIVES"):
            for value in (None, "", '""', '"allow all;"', '"deny all"', '"allow 10.0.0.0/8;"'):
                with self.subTest(key=key, value=value):
                    self.assert_refused(key, value, key)

    def test_sql_server_not_on_express(self):
        self.assert_refused("MSSQL_PID", None, "Developer")
        self.assert_refused("MSSQL_PID", "Developer", "MSSQL_PID")

    def test_the_migrator_in_development(self):
        self.assert_refused("DBMIGRATOR_ENVIRONMENT", "Development", "demo seeders")
        self.assertEqual(self.problems_with("DBMIGRATOR_ENVIRONMENT", None), [])  # compose defaults it

    def test_a_backup_dir_other_than_the_one_the_scripts_hard_code(self):
        self.assert_refused("BACKUP_DIR", "/srv/backups", "BACKUP_DIR")

    def test_minio_limits(self):
        cases = {"MINIO_ROOT_USER": "u" * 21, "MINIO_APP_ACCESS_KEY": "a" * 64, "MINIO_ROOT_PASSWORD": "p" * 41,
                 "MINIO_APP_SECRET_KEY": "s" * 7}
        for key, value in cases.items():
            with self.subTest(key=key):
                self.assert_refused(key, value, f"{key} is {len(value)} characters")

    def test_the_app_minio_key_equal_to_root(self):
        root = vm_env.parse(self.good)[0]["MINIO_ROOT_USER"]
        self.assert_refused("MINIO_APP_ACCESS_KEY", root, "must differ from MINIO_ROOT_USER")

    def test_a_leftover_placeholder(self):
        self.assert_refused("SMTP_HOST", "CHANGE_ME_smtp_host", "SMTP_HOST still holds a placeholder")
        self.assert_refused("SMTP_HOST", "<same relay as the office server>", "SMTP_HOST still holds a placeholder")

    def test_a_missing_required_setting(self):
        self.assert_refused("AZURE_BACKUP_STORAGE_ACCOUNT", None, "AZURE_BACKUP_STORAGE_ACCOUNT is missing")
        self.assert_refused("CASE_TRACKER_FEED_TOKEN", "", "CASE_TRACKER_FEED_TOKEN is missing")

    def test_the_sql_login_and_passwords(self):
        self.assert_refused("SQL_APP_LOGIN", "sa", "not sa")
        self.assert_refused("SQL_APP_LOGIN", "SA", "not sa")
        self.assert_refused("SQL_APP_LOGIN", "portal-app", "plain login name")
        for bad in ("Pp-1234567890abc;Z9", "Pp-1234567890abc'Z9", "Pp-1234567890abc$Z9", "Pp 1234567890abcdZ9"):
            with self.subTest(value=bad):
                self.assert_refused("SQL_APP_PASSWORD", f"'{bad}'", "must not contain")
        self.assert_refused("SQL_APP_PASSWORD", "Ab1" * 5, "SQL_APP_PASSWORD is 15 characters")
        self.assert_refused("MSSQL_SA_PASSWORD", "nouppercase-123456", "needs upper case")

    def test_the_vault_store(self):
        self.assert_refused("ADMIN_PASSWORD_VAULT_URI", "https://vault.example.com/", "must be empty")

    def test_an_acme_challenge_the_renewal_script_would_refuse(self):
        # renew-tls-cert.sh compares exactly, so a different case is as wrong as a different method.
        for value in ("dns", "http01", "HTTP-01", "tls-alpn-01", "dns-01,http-01"):
            with self.subTest(value=value):
                self.assert_refused("ACME_CHALLENGE", value, "ACME_CHALLENGE must be dns-01 or http-01")

    def test_either_acme_challenge_or_the_default_passes(self):
        for value in ("dns-01", "http-01", "", None):
            with self.subTest(value=value):
                self.assertEqual(self.problems_with("ACME_CHALLENGE", value), [])

    def test_the_file_shape(self):
        self.assertTrue(any("set twice" in p for p in vm_env.check_text(self.good + "APP_NAME=Again\n")))
        self.assertTrue(any("is not KEY=value" in p for p in vm_env.check_text(self.good + "not a setting\n")))
        self.assertTrue(any("Windows line endings" in p for p in vm_env.check_text(self.good.replace("\n", "\r\n"))))

    def test_quotes_and_inline_comments_are_read_the_way_compose_reads_them(self):
        self.assertEqual(self.problems_with("MSSQL_PID", "'Express'"), [])
        self.assertEqual(self.problems_with("MSSQL_PID", "Express # licensed for production"), [])


class CheckOnDisk(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp(prefix="vm-env-check-"))
        self.path = self.dir / "env.prod"

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def write(self, text):
        self.path.write_text(text, encoding="utf-8", newline="\n")
        if os.name == "posix":
            os.chmod(self.path, 0o600)

    def test_exit_codes(self):
        self.write(filled())
        code, output = run("check", str(self.path))
        self.assertEqual((code, output.startswith("OK:")), (0, True), output)
        self.write(with_value(filled(), "MSSQL_PID", "Developer"))
        self.assertEqual(run("check", str(self.path))[0], 1)
        self.assertEqual(run("check", str(self.dir / "missing"))[0], 2)

    @unittest.skipUnless(os.name == "posix", "file modes are POSIX")
    def test_a_file_others_can_read_is_refused(self):
        self.write(filled())
        os.chmod(self.path, 0o644)
        code, output = run("check", str(self.path))
        self.assertEqual(code, 1)
        self.assertIn("mode 644", output)

    def test_problems_name_keys_never_values(self):
        marker_token = "f00dfeed" * 8
        text = with_value(filled(), "CASE_TRACKER_FEED_TOKEN", marker_token)
        text = with_value(text, "CASE_TRACKER_INTEGRATION_TOKEN", marker_token)
        text = with_value(text, "MINIO_ROOT_USER", "rootvaluemarker" * 3)
        text = with_value(text, "SQL_APP_PASSWORD", "'Pp-markerpassword;Z9'")
        self.write(text)
        code, output = run("check", str(self.path))
        self.assertEqual(code, 1)
        for value in (marker_token, "rootvaluemarker", "markerpassword"):
            self.assertNotIn(value, output)


if __name__ == "__main__":
    unittest.main()
