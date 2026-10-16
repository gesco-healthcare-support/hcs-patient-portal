"""scripts/hosting/renew-tls-cert.sh keeps the Azure VM's public certificate current, and never leaves nginx serving
something it did not check.

What these tests pin:

  - lego is asked for exactly the four names nginx serves, against the right Let's Encrypt server, signing in as
    the VM's managed identity, with the zone's subscription and resource group taken from instance metadata;
  - a new certificate is installed IN PLACE (same inode: compose bind-mounts the file, and a renamed-over file is
    one the running container never sees), the previous pair is kept as .prev, and nginx is checked and reloaded;
  - nothing is installed when lego fails, when a name is missing, or when the key does not match;
  - a reload that does not take, or a served certificate close to expiry, fails the run (which alerts);
  - --staging proves issuance and installs nothing;
  - ACME_CHALLENGE=http-01 (the fallback when the public names are A records, 2026-10-15): lego is asked for every
    name EXPLICITLY (base, the proxy's own hosts, admin, and three per office read from SaasTenants), through the
    webroot nginx serves on port 80, with no Azure sign-in at all; the webroot is proven before Let's Encrypt is
    asked; an office added since the last issuance gets a new certificate at once; a fresh VM gets a placeholder so
    nginx can start; office names that are not DNS labels, or that collide with a reserved host, and more than 100
    names are refused.

lego and nginx are fakes here: `docker` is a script that, for the lego container, issues a real self-signed
certificate with openssl for the names it was given, and for `nginx -s reload` copies the installed certificate to
the file the script reads as "served" (TLS_SERVED_CERT_FILE, the script's test-only switch); `docker compose exec
sql-server` prints FAKE_OFFICES as the office list; `curl` answers the HTTP-01 webroot probe from the webroot on disk
(unless FAKE_WEBROOT_BROKEN). openssl is real
throughout, so every certificate check runs against a real certificate. The real thing -- Let's Encrypt, Azure
DNS, the managed identity -- can only be proven on the VM: run it with --staging first.

They need a POSIX bash and openssl. CI runs on Linux. On Windows set TEST_BASH to Git Bash's plain shell,
C:/Program Files/Git/usr/bin/bash.exe, or these tests are skipped.
"""

import os
import pathlib
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest

from shell_harness import BASH, shell_path

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
    [[ "$prev" == -v && "$a" == *:/data ]] && data="${a%:/data}"
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
    *"exec -T sql-server"*) printf '%b' "${FAKE_OFFICES:-}"; exit "${FAKE_SQL_RC:-0}" ;;
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
{ printf '%s\x1f' "$@"; printf '\n'; } >> "$FAKE_DIR/curl.log"
case "$*" in
  *"/.well-known/acme-challenge/"*)
    [[ -n "${FAKE_WEBROOT_BROKEN:-}" ]] && { echo "<html>404 Not Found</html>"; exit 0; }
    url="${@: -1}"; cat "$FAKE_WEBROOT/.well-known/acme-challenge/${url##*/}" 2>/dev/null
    exit 0 ;;
  *"Metadata: true"*169.254.169.254*)
    printf '{"location":"westus2","name":"vm-portal-prod","resourceGroupName":"rg-appointment-portal-prod","subscriptionId":"%s","vmId":"x"}' "$FAKE_SUBSCRIPTION"
    exit 0 ;;
esac
exit 7
"""


def openssl(*args, input_path=None):
    result = subprocess.run(["openssl", *args, *(["-in", str(input_path)] if input_path else [])],
                            capture_output=True, encoding="utf-8", errors="replace")
    return result.returncode, result.stdout


class _Harness(unittest.TestCase):
    """setUp, the fakes and the helpers; no tests of its own."""

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
                   ACME_WEBROOT=shell_path(self.dir / "webroot"),
                   FAKE_WEBROOT=shell_path(self.dir / "webroot"),
                   TLS_SERVED_CERT_FILE=shell_path(self.served),
                   TLS_VERIFY_WAIT="0")
        for key in ("AZURE_SUBSCRIPTION_ID", "AZURE_RESOURCE_GROUP", "ACME_CHALLENGE"):
            env.pop(key, None)
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

    def sans(self, path):
        text = openssl("x509", "-noout", "-ext", "subjectAltName", input_path=path)[1]
        parts = ",".join(text.splitlines()).split(",")
        return [part.strip()[len("DNS:"):] for part in parts if part.strip().startswith("DNS:")]


@unittest.skipUnless(BASH and shutil.which("openssl"), "needs a POSIX bash (set TEST_BASH on Windows) and openssl")
class RenewTlsCert(_Harness):
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

    def test_an_unknown_challenge_is_refused_before_anything_runs(self):
        code, output = self.run_script(ACME_CHALLENGE="tls-alpn-01")
        self.assertNotEqual(code, 0)
        self.assertIn("must be dns-01 or http-01", output)
        self.assertEqual(self.docker_calls(), [])


PROXY_HOSTS = ("api", "auth", "health", "minio", "www")


def http01_names(offices):
    names = [BASE] + [f"{h}.{BASE}" for h in PROXY_HOSTS] + [f"admin.{BASE}", f"admin.api.{BASE}", f"admin.auth.{BASE}"]
    for office in offices:
        names += [f"{office}.{BASE}", f"{office}.api.{BASE}", f"{office}.auth.{BASE}"]
    return names


@unittest.skipUnless(BASH and shutil.which("openssl"), "needs a POSIX bash (set TEST_BASH on Windows) and openssl")
class RenewTlsCertHttp01(_Harness):
    """ACME_CHALLENGE=http-01: the fallback when the public names are A records at the registrar (2026-10-15)."""

    OFFICES = ("falkinstein", "test-office")

    def setUp(self):
        super().setUp()
        with (self.dir / "env.prod").open("a", encoding="utf-8", newline="\n") as env:
            env.write("ACME_CHALLENGE=http-01\n")

    def run_http(self, *args, offices=OFFICES, **extra):
        extra.setdefault("FAKE_OFFICES", "".join(f"{office}\n" for office in offices))
        return self.run_script(*args, **extra)

    def issued(self, offices=OFFICES):
        """A fresh VM: the first run writes the placeholder, the second issues the real certificate."""
        code, output = self.run_http(offices=offices)
        self.assertEqual(code, 0, output)
        self.assertIn("placeholder", output)
        (self.dir / "docker.log").unlink(missing_ok=True)
        code, output = self.run_http(offices=offices)
        self.assertEqual(code, 0, output)
        return output

    def lego_runs(self):
        return [c for c in self.docker_calls() if c and c[0] == "run"]

    def test_lego_is_asked_for_every_name_through_the_webroot_and_never_signs_in_to_azure(self):
        output = self.issued()
        self.assertIn("DONE: serving", output)
        call = self.lego_call()
        domains = [call[i + 1] for i, a in enumerate(call) if a == "--domains"]
        self.assertEqual(domains, http01_names(self.OFFICES))
        self.assertIn("--http", call)
        self.assertEqual(call[call.index("--http.webroot") + 1], "/webroot")
        self.assertTrue(any(a.endswith(":/webroot") for a in call), call)
        self.assertNotIn("--dns", call)
        self.assertNotIn("--network", call)
        self.assertFalse([a for a in call if a.startswith("AZURE_")], call)
        self.assertNotIn("169.254.169.254", (self.dir / "curl.log").read_text(encoding="utf-8"))
        self.assertEqual(sorted(self.sans(self.cert)), sorted(http01_names(self.OFFICES)))

    def test_a_fresh_vm_gets_a_short_lived_placeholder_so_nginx_can_start(self):
        code, output = self.run_http()
        self.assertEqual(code, 0, output)
        self.assertIn("placeholder", output)
        self.assertEqual(self.docker_calls(), [], "no office query, no lego, no nginx before the stack exists")
        self.assertTrue(self.key.exists())
        self.assertIn(f"admin.{BASE}", self.sans(self.cert))
        rc, _ = openssl("x509", "-noout", "-checkend", str(3 * 86400), input_path=self.cert)
        self.assertNotEqual(rc, 0, "the placeholder must not outlive a few days")

    def test_an_office_added_since_the_last_issuance_gets_a_new_certificate_at_once(self):
        self.issued(offices=("falkinstein",))
        code, output = self.run_http(offices=("falkinstein", "newoffice"), FAKE_LEGO_NOCHANGE="1")
        self.assertEqual(code, 0, output)
        self.assertIn("lacks", output)
        self.assertIn(f"newoffice.auth.{BASE}", self.sans(self.cert))
        self.assertTrue(list((self.dir / "acme" / "certificates").glob("portal.crt.superseded-*")))

    def test_a_day_with_no_new_office_changes_nothing(self):
        self.issued()
        before = self.cert.read_bytes()
        code, output = self.run_http(FAKE_LEGO_NOCHANGE="1")
        self.assertEqual(code, 0, output)
        self.assertNotIn("lacks", output)
        self.assertIn("already the current one", output)
        self.assertEqual(self.cert.read_bytes(), before)

    def test_a_webroot_nginx_does_not_serve_stops_before_lets_encrypt_is_asked(self):
        self.issued()
        before = self.cert.read_bytes()
        (self.dir / "docker.log").unlink()
        code, output = self.run_http(FAKE_WEBROOT_BROKEN="1")
        self.assertNotEqual(code, 0)
        self.assertIn("does not serve", output)
        self.assertEqual(self.lego_runs(), [])
        self.assertEqual(self.cert.read_bytes(), before)
        self.assertEqual(list((self.dir / "webroot" / ".well-known" / "acme-challenge").iterdir()), [],
                         "the probe file is removed")

    def test_a_stopped_reverse_proxy_fails_the_run(self):
        self.issued()
        (self.dir / "docker.log").unlink()
        code, output = self.run_http(FAKE_PROXY_RUNNING="")
        self.assertNotEqual(code, 0)
        self.assertIn("reverse proxy is not running", output)
        self.assertEqual(self.lego_runs(), [])

    def test_an_office_list_that_cannot_be_read_fails_the_run(self):
        self.issued()
        code, output = self.run_http(FAKE_SQL_RC="1")
        self.assertNotEqual(code, 0)
        self.assertIn("cannot read the office list", output)

    def test_an_office_name_that_is_not_a_dns_label_is_refused(self):
        self.issued()
        (self.dir / "docker.log").unlink()
        code, output = self.run_http(offices=("falkinstein", "bad_name"))
        self.assertNotEqual(code, 0)
        self.assertIn("not a valid DNS label", output)
        self.assertEqual(self.lego_runs(), [])

    def test_an_office_named_like_a_reserved_host_is_refused(self):
        self.issued()
        code, output = self.run_http(offices=("minio",))
        self.assertNotEqual(code, 0)
        self.assertIn("collides with a reserved host", output)

    def test_more_than_one_hundred_names_are_refused(self):
        self.issued()
        (self.dir / "docker.log").unlink()
        code, output = self.run_http(offices=tuple(f"office{i}" for i in range(31)))
        self.assertNotEqual(code, 0)
        self.assertIn("at most 100", output)
        self.assertEqual(self.lego_runs(), [])

    def test_staging_proves_the_challenge_path_and_installs_nothing(self):
        self.assertEqual(self.run_http()[0], 0)
        placeholder = self.cert.read_bytes()
        code, output = self.run_http("--staging")
        self.assertEqual(code, 0, output)
        self.assertIn("Nothing was installed", output)
        self.assertEqual(self.cert.read_bytes(), placeholder)
        self.assertIn("https://acme-staging-v02.api.letsencrypt.org/directory", self.lego_call())


class TheProxyHostsMatchTenantNaming(unittest.TestCase):
    """HTTP-01 lists the proxy's own hosts by name, so the script's list must be TenantNaming.ProxyReservedSlugs.
    A host added on one side only would be refused a certificate (or certified for a name nginx does not serve)."""

    def test_the_script_names_exactly_the_proxy_reserved_slugs(self):
        naming = (REPO_ROOT / "src" / "HealthcareSupport.CaseEvaluation.Domain.Shared" / "MultiTenancy"
                  / "TenantNaming.cs").read_text(encoding="utf-8")
        csharp = re.search(r"ProxyReservedSlugs\s*=\s*new HashSet<string>\([^)]*\)\s*\{([^}]*)\}", naming)
        self.assertIsNotNone(csharp, "TenantNaming.ProxyReservedSlugs not found")
        script = re.search(r"^PROXY_HOSTS=\(([^)]*)\)", SCRIPT.read_text(encoding="utf-8"), re.MULTILINE)
        self.assertIsNotNone(script, "PROXY_HOSTS not found in the script")
        self.assertEqual(set(script.group(1).split()), set(re.findall(r'"([a-z0-9-]+)"', csharp.group(1))))
        self.assertEqual(set(PROXY_HOSTS), set(script.group(1).split()))


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
