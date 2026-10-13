"""scripts/hosting/renew-tls-cert.sh keeps the Azure VM's public certificate current, and never leaves nginx serving
something it did not check.

What these tests pin:

  - lego is asked for exactly the four names nginx serves, against the right Let's Encrypt server, signing in as
    the VM's managed identity, with the zone's subscription and resource group taken from instance metadata;
  - a new certificate is installed IN PLACE (same inode: compose bind-mounts the file, and a renamed-over file is
    one the running container never sees), the previous pair is kept as .prev, and nginx is checked and reloaded;
  - nothing is installed when lego fails, when a name is missing, or when the key does not match;
  - a reload that does not take, or a served certificate close to expiry, fails the run (which alerts);
  - --staging proves issuance and installs nothing.

lego and nginx are fakes here: `docker` is a script that, for the lego container, issues a real self-signed
certificate with openssl for the names it was given, and for `nginx -s reload` copies the installed certificate to
the file the script reads as "served" (TLS_SERVED_CERT_FILE, the script's test-only switch). openssl is real
throughout, so every certificate check runs against a real certificate. The real thing -- Let's Encrypt, Azure
DNS, the managed identity -- can only be proven on the VM: run it with --staging first.

They need a POSIX bash and openssl. CI runs on Linux. On Windows set TEST_BASH to Git Bash's plain shell,
C:/Program Files/Git/usr/bin/bash.exe, or these tests are skipped.
"""

import os
import pathlib
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "renew-tls-cert.sh"
ALERT = REPO_ROOT / "scripts" / "hosting" / "backup-alert.sh"
BASE = "portal.example.test"
NAMES = [BASE, f"*.{BASE}", f"*.api.{BASE}", f"*.auth.{BASE}"]
SUBSCRIPTION = "0f8fad5b-d9cb-469f-a165-70867728950e"
SEP = "\x1f"

FAKE_DOCKER = r"""#!/usr/bin/env bash
{ printf '%s\x1f' "$@"; printf '\n'; } >> "$FAKE_DIR/docker.log"
if [[ "$1" == run ]]; then
  data=""; domains=(); prev=""
  for a in "$@"; do
    [[ "$prev" == -v ]] && data="${a%:/data}"
    [[ "$prev" == --domains ]] && domains+=("$a")
    prev="$a"
  done
  [[ -n "${FAKE_LEGO_FAIL:-}" ]] && { echo "lego: acme: error presenting token" >&2; exit 1; }
  certs="$data/certificates"; mkdir -p "$certs"
  if [[ -n "${FAKE_LEGO_NOCHANGE:-}" && -s "$certs/portal.crt" ]]; then echo "Skip renewal"; exit 0; fi
  [[ -n "${FAKE_LEGO_DROP_NAME:-}" ]] && domains=("${domains[@]:0:3}")
  san=""; for d in "${domains[@]}"; do san+="${san:+,}DNS:$d"; done
  MSYS_NO_PATHCONV=1 openssl req -x509 -newkey rsa:2048 -nodes -days "${FAKE_DAYS:-90}" -subj "/CN=${domains[0]}" \
    -addext "subjectAltName=$san" -keyout "$certs/portal.key" -out "$certs/portal.crt" 2>/dev/null || exit 3
  if [[ -n "${FAKE_LEGO_WRONG_KEY:-}" ]]; then
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$certs/portal.key" 2>/dev/null || exit 3
  fi
  exit 0
fi
if [[ "$1" == compose ]]; then
  case "$*" in
    *"ps -q --status running reverse-proxy"*) [[ -n "${FAKE_PROXY_RUNNING:-}" ]] && echo deadbeef; exit 0 ;;
    *"exec -T reverse-proxy nginx -t"*) exit "${FAKE_NGINX_T_RC:-0}" ;;
    *"exec -T reverse-proxy nginx -s reload"*)
      [[ -z "${FAKE_RELOAD_NOOP:-}" ]] && cp "$FAKE_CERT_PATH" "$FAKE_SERVED"; exit 0 ;;
  esac
fi
echo "fake docker: unexpected: $*" >&2
exit 2
"""

# Instance metadata: answers the compute document only when asked with the Metadata header.
FAKE_CURL = r"""#!/usr/bin/env bash
case "$*" in
  *"Metadata: true"*169.254.169.254*)
    printf '{"location":"westus2","name":"vm-portal-prod","resourceGroupName":"rg-appointment-portal-prod","subscriptionId":"%s","vmId":"x"}' "$FAKE_SUBSCRIPTION"
    exit 0 ;;
esac
exit 7
"""


def _find_bash():
    explicit = os.environ.get("TEST_BASH")
    if explicit:
        return explicit
    if sys.platform == "win32":
        return None  # PATH's bash on Windows may be WSL, which cannot see this filesystem the same way
    return shutil.which("bash")


BASH = _find_bash()


def shell_path(path):
    """A path as the shell under test should see it (/c/x on Windows, unchanged on Linux)."""
    text = pathlib.Path(path).as_posix()
    if sys.platform == "win32" and len(text) > 1 and text[1] == ":":
        return "/" + text[0].lower() + text[2:]
    return text


def openssl(*args, input_path=None):
    result = subprocess.run(["openssl", *args, *(["-in", str(input_path)] if input_path else [])],
                            capture_output=True, encoding="utf-8", errors="replace")
    return result.returncode, result.stdout


@unittest.skipUnless(BASH and shutil.which("openssl"), "needs a POSIX bash (set TEST_BASH on Windows) and openssl")
class RenewTlsCert(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="tls-renew-"))
        (self.dir / "bin").mkdir()
        for name, body in (("docker", FAKE_DOCKER), ("curl", FAKE_CURL)):
            path = self.dir / "bin" / name
            path.write_text(body, encoding="utf-8", newline="\n")
            path.chmod(path.stat().st_mode | stat.S_IEXEC)
        (self.dir / "secrets").mkdir()
        self.cert = self.dir / "secrets" / "wildcard.crt"
        self.key = self.dir / "secrets" / "wildcard.key"
        self.served = self.dir / "served.crt"
        (self.dir / "env.prod").write_text(
            f"BASE_DOMAIN={BASE}\n"
            f"TLS_CERT_PATH={shell_path(self.cert)}\n"
            f"TLS_KEY_PATH={shell_path(self.key)}\n", encoding="utf-8", newline="\n")

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def run_script(self, *args, **extra):
        env = dict(os.environ,
                   FAKE_DIR=shell_path(self.dir),
                   FAKE_CERT_PATH=shell_path(self.cert),
                   FAKE_SERVED=shell_path(self.served),
                   FAKE_SUBSCRIPTION=SUBSCRIPTION,
                   FAKE_PROXY_RUNNING="1",
                   ENV_FILE=shell_path(self.dir / "env.prod"),
                   ACME_DIR=shell_path(self.dir / "acme"),
                   TLS_SERVED_CERT_FILE=shell_path(self.served),
                   TLS_VERIFY_WAIT="0")
        env.pop("AZURE_SUBSCRIPTION_ID", None)
        env.pop("AZURE_RESOURCE_GROUP", None)
        env.update(extra)
        env["PATH"] = (shell_path(self.dir / "bin") + ":/usr/bin:/bin" if sys.platform == "win32"
                       else str(self.dir / "bin") + os.pathsep + os.environ.get("PATH", ""))
        result = subprocess.run([BASH, str(SCRIPT), *args], env=env, capture_output=True,
                                encoding="utf-8", errors="replace", timeout=180)
        return result.returncode, result.stdout + result.stderr

    def docker_calls(self):
        log = self.dir / "docker.log"
        return [line.split(SEP)[:-1] for line in log.read_text(encoding="utf-8").splitlines()] if log.exists() else []

    def lego_call(self):
        calls = [c for c in self.docker_calls() if c and c[0] == "run"]
        self.assertEqual(len(calls), 1, calls)
        return calls[0]

    def fingerprint(self, path):
        return openssl("x509", "-noout", "-fingerprint", "-sha256", input_path=path)[1].strip()

    def test_first_run_issues_installs_reloads_and_verifies(self):
        code, output = self.run_script()
        self.assertEqual(code, 0, output)
        self.assertIn("DONE: serving", output)
        self.assertTrue(self.cert.exists() and self.key.exists())
        self.assertEqual(self.fingerprint(self.served), self.fingerprint(self.cert))
        ops = [" ".join(c) for c in self.docker_calls() if c and c[0] == "compose"]
        self.assertTrue(any("nginx -t" in op for op in ops), ops)
        self.assertTrue(any("nginx -s reload" in op for op in ops), ops)
        self.assertLess(next(i for i, op in enumerate(ops) if "nginx -t" in op),
                        next(i for i, op in enumerate(ops) if "nginx -s reload" in op))

    def test_lego_is_asked_for_the_four_names_as_the_vm_identity(self):
        self.assertEqual(self.run_script()[0], 0)
        call = self.lego_call()
        domains = [call[i + 1] for i, a in enumerate(call) if a == "--domains"]
        self.assertEqual(domains, NAMES)
        self.assertIn("https://acme-v02.api.letsencrypt.org/directory", call)
        self.assertIn("AZURE_AUTH_METHOD=msi", call)
        self.assertIn(f"AZURE_SUBSCRIPTION_ID={SUBSCRIPTION}", call)
        self.assertIn("AZURE_RESOURCE_GROUP=rg-appointment-portal-prod", call)
        self.assertIn(f"AZURE_ZONE_NAME={BASE}", call)
        self.assertIn("--dns", call)
        self.assertEqual(call[call.index("--dns") + 1], "azuredns")
        image = next(a for a in call if a.startswith("goacme/lego:"))
        self.assertRegex(image, r"^goacme/lego:v[0-9.]+@sha256:[0-9a-f]{64}$", "the image must be pinned by digest")

    def test_explicit_subscription_and_group_skip_instance_metadata(self):
        code, output = self.run_script(AZURE_SUBSCRIPTION_ID="7c9e6679-7425-40de-944b-e07fc1f90ae7",
                                       AZURE_RESOURCE_GROUP="rg-other")
        self.assertEqual(code, 0, output)
        call = self.lego_call()
        self.assertIn("AZURE_SUBSCRIPTION_ID=7c9e6679-7425-40de-944b-e07fc1f90ae7", call)
        self.assertIn("AZURE_RESOURCE_GROUP=rg-other", call)

    def test_a_day_with_nothing_to_renew_changes_nothing(self):
        self.assertEqual(self.run_script()[0], 0)
        before = self.cert.read_bytes()
        (self.dir / "docker.log").unlink()
        code, output = self.run_script(FAKE_LEGO_NOCHANGE="1")
        self.assertEqual(code, 0, output)
        self.assertIn("already the current one", output)
        self.assertEqual(self.cert.read_bytes(), before)
        self.assertFalse(pathlib.Path(str(self.cert) + ".prev").exists())
        self.assertFalse(any("reload" in " ".join(c) for c in self.docker_calls()))

    def test_a_renewal_is_written_in_place_and_keeps_the_previous_pair(self):
        self.assertEqual(self.run_script()[0], 0)
        old_cert, old_key = self.cert.read_bytes(), self.key.read_bytes()
        inode_cert, inode_key = self.cert.stat().st_ino, self.key.stat().st_ino
        code, output = self.run_script()
        self.assertEqual(code, 0, output)
        self.assertNotEqual(self.cert.read_bytes(), old_cert)
        self.assertEqual(pathlib.Path(str(self.cert) + ".prev").read_bytes(), old_cert)
        self.assertEqual(pathlib.Path(str(self.key) + ".prev").read_bytes(), old_key)
        self.assertEqual(self.cert.stat().st_ino, inode_cert, "replaced, not rewritten: nginx would never see it")
        self.assertEqual(self.key.stat().st_ino, inode_key)
        self.assertEqual(self.fingerprint(self.served), self.fingerprint(self.cert))

    def assert_installed_untouched(self, before):
        self.assertEqual(self.cert.read_bytes(), before)

    def test_nothing_is_installed_when_lego_fails(self):
        self.assertEqual(self.run_script()[0], 0)
        before = self.cert.read_bytes()
        code, output = self.run_script(FAKE_LEGO_FAIL="1")
        self.assertNotEqual(code, 0)
        self.assertIn("lego failed", output)
        self.assert_installed_untouched(before)

    def test_nothing_is_installed_when_a_name_is_missing(self):
        code, output = self.run_script(FAKE_LEGO_DROP_NAME="1")
        self.assertNotEqual(code, 0)
        self.assertIn(f"does not cover *.auth.{BASE}", output)
        self.assertFalse(self.cert.exists())

    def test_nothing_is_installed_when_the_key_does_not_match(self):
        code, output = self.run_script(FAKE_LEGO_WRONG_KEY="1")
        self.assertNotEqual(code, 0)
        self.assertIn("is not the private key", output)
        self.assertFalse(self.cert.exists())

    def test_a_reload_that_did_not_take_fails_the_run(self):
        self.assertEqual(self.run_script()[0], 0)
        code, output = self.run_script(FAKE_RELOAD_NOOP="1")
        self.assertNotEqual(code, 0)
        self.assertIn("nginx serves", output)

    def test_a_rejected_configuration_fails_and_names_the_previous_pair(self):
        self.assertEqual(self.run_script()[0], 0)
        code, output = self.run_script(FAKE_NGINX_T_RC="1")
        self.assertNotEqual(code, 0)
        self.assertIn(".prev", output)
        self.assertFalse(any("reload" in " ".join(c) for c in self.docker_calls()[-2:]))

    def test_a_served_certificate_close_to_expiry_fails_the_run(self):
        code, output = self.run_script(FAKE_DAYS="10")
        self.assertNotEqual(code, 0)
        self.assertIn("expires within 14 days", output)

    def test_staging_proves_issuance_and_installs_nothing(self):
        code, output = self.run_script("--staging")
        self.assertEqual(code, 0, output)
        self.assertIn("Nothing was installed", output)
        self.assertFalse(self.cert.exists())
        call = self.lego_call()
        self.assertIn("https://acme-staging-v02.api.letsencrypt.org/directory", call)
        self.assertTrue(any(a.endswith("/acme/staging:/data") for a in call), call)
        self.assertFalse(any(c and c[0] == "compose" and "reload" in " ".join(c) for c in self.docker_calls()))

    def test_a_proxy_that_is_not_running_yet_gets_the_files_and_no_reload(self):
        code, output = self.run_script(FAKE_PROXY_RUNNING="", TLS_SERVED_CERT_FILE="")
        self.assertEqual(code, 0, output)
        self.assertIn("will load the new certificate when it starts", output)
        self.assertTrue(self.cert.exists())
        self.assertFalse(any("reload" in " ".join(c) for c in self.docker_calls()))

    def test_a_malformed_base_domain_is_refused_before_lego_runs(self):
        (self.dir / "env.prod").write_text("BASE_DOMAIN=Portal Example\n", encoding="utf-8", newline="\n")
        code, output = self.run_script()
        self.assertNotEqual(code, 0)
        self.assertIn("not a plain lowercase DNS name", output)
        self.assertEqual(self.docker_calls(), [])


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class TheAlertNamesACertificateFailureAsOne(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="tls-alert-"))
        (self.dir / "env.prod").write_text(
            "SMTP_HOST=mail.example.test\nSMTP_FROM_ADDRESS=portal@example.test\n"
            "BACKUP_ALERT_RECIPIENTS=ops@example.test\n", encoding="utf-8", newline="\n")

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def alert(self, unit):
        env = dict(os.environ, ENV_FILE=shell_path(self.dir / "env.prod"), BACKUP_ALERT_DRY_RUN="1")
        result = subprocess.run([BASH, str(ALERT), "failed", unit], env=env, capture_output=True,
                                encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout

    def test_the_renewal_unit_is_reported_as_a_certificate_failure(self):
        out = self.alert("hcs-portal-tls-renew.service")
        self.assertIn("Subject: Patient Portal certificate: hcs-portal-tls-renew.service failed", out)
        self.assertNotIn("backup", out.split("Subject:", 1)[1].splitlines()[0])

    def test_a_backup_unit_is_still_reported_as_a_backup_failure(self):
        out = self.alert("hcs-portal-backup-azure.service")
        self.assertIn("Subject: Patient Portal backup: hcs-portal-backup-azure.service failed", out)


class TheUnitsAndScriptShipCorrectly(unittest.TestCase):
    def test_git_tracks_the_script_executable(self):
        mode = subprocess.run(["git", "ls-files", "-s", str(SCRIPT.relative_to(REPO_ROOT))], cwd=REPO_ROOT,
                              capture_output=True, encoding="utf-8").stdout.split(" ")[0]
        self.assertEqual(mode, "100755")

    def test_the_service_alerts_on_failure_and_runs_the_script(self):
        unit = (REPO_ROOT / "scripts" / "hosting" / "systemd" / "hcs-portal-tls-renew.service").read_text(encoding="utf-8")
        self.assertIn("OnFailure=hcs-portal-backup-alert@%n.service", unit)
        self.assertIn("ExecStart=/bin/bash /home/apadmin/hcs-patient-portal/scripts/hosting/renew-tls-cert.sh", unit)
        self.assertIn("Type=oneshot", unit)

    def test_the_timer_runs_daily_and_catches_up_after_downtime(self):
        timer = (REPO_ROOT / "scripts" / "hosting" / "systemd" / "hcs-portal-tls-renew.timer").read_text(encoding="utf-8")
        self.assertRegex(timer, r"OnCalendar=\*-\*-\* \d\d:\d\d:\d\d")
        self.assertIn("Persistent=true", timer)


if __name__ == "__main__":
    unittest.main()
