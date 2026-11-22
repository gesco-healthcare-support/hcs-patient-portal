"""No password, key or token is ever put on a command line by the hosting scripts, the dev scripts or the compose files.

A command line is readable by every local user (`ps`, /proc/<pid>/cmdline), and a container's processes are on the
HOST's process list too. So a secret goes to a tool by NAME instead:

  - sqlcmd reads SQLCMDPASSWORD, never `-P <password>`;
  - mc reads its login from MC_HOST_<alias>, never `mc alias set <alias> <url> <user> <secret>`;
  - `mc admin user add` reads the new secret from stdin when it is not an argument;
  - docker passes a variable through by name (`-e NAME` with no value, the value in docker's own environment),
    never `-e NAME=<secret>`.

This test scans every tracked shell, PowerShell and compose file under scripts/, docker/ and the repo root, joins
continued lines first, skips comment lines, and fails on any of the forbidden shapes. Each rule is also proven to
fire on the exact lines this repository used to carry, so the scan cannot pass by matching nothing.
"""

import pathlib
import re
import subprocess
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]

# (rule name, pattern). A line is judged after continuation lines are joined, comments skipped.
RULES = [
    # Case-insensitive so a ${SQLCMD} variable counts too; the \b keeps SQLCMDPASSWORD out.
    ("sqlcmd password on the command line (-P)",
     re.compile(r"sqlcmd\b.*?[\s'\",(]-P(?:[\s'\",]|$)", re.IGNORECASE)),
    ("mc alias set (the login on mc's command line)",
     re.compile(r"\bmc\s+alias\s+set\b")),
    ("docker -e MC_HOST_*=... (the login on docker's command line)",
     re.compile(r"(?:^|\s)-e\s+[\"']?MC_HOST_\w+=")),
    ("docker -e NAME=<secret variable> (the value on docker's command line)",
     re.compile(r"(?:^|\s)-e\s+[\"']?\w+=[\"']?\$\{?\w*(?:PASS|SECRET|TOKEN|KEY)", re.IGNORECASE)),
    # openssl -passin/-passout pass:<value from a variable> puts the passphrase on openssl's command line; use env:NAME.
    ("openssl pass:$VAR (the passphrase on openssl's command line)",
     re.compile(r"-pass(?:in|out)\s+[\"']?pass:\$")),
]

# `mc admin user add` must be fed from a pipe (the secret on stdin), never given the secret as an argument.
USER_ADD = re.compile(r"\bmc\s+admin\s+user\s+add\b")
USER_ADD_FROM_PIPE = re.compile(r"\|\s*mc\s+admin\s+user\s+add\b")

# The lines this repository carried before the fix (2026-11-20). Every rule must catch its own.
KNOWN_BAD = [
    '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sa_password" -C -b "$@"; }',
    "test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -Q \"SELECT 1\" -C -b",
    "q() { docker exec \"$SQL\" bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"$MSSQL_SA_PASSWORD\"'",
    "'/opt/mssql-tools18/bin/sqlcmd','-S','localhost','-U','sa','-P',$saPassword,",
    "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $saPassword `",
    "as_sa() { docker exec -i \"$container\" bash -c \"${SQLCMD} -S localhost -U sa -P \\\"\\$MSSQL_SA_PASSWORD\\\" -C -b\"; }",
    'mc alias set local http://minio:9000 "$$MINIO_ROOT_USER" "$$MINIO_ROOT_PASSWORD"',
    "printf 'mc alias set local %s %s %s > /dev/null\\n' \"$(sq \"$MINIO_URL\")\"",
    '-e "MC_HOST_m=http://${MINIO_USER}:${MINIO_PASS}@minio:9000" \\',
    'run() { MSYS_NO_PATHCONV=1 docker run --rm -e K="$ABP_NUGET_API_KEY" -e P="$PROJECT" \\',
    '-out "$OUT" -passout "pass:${DATAPROTECTION_CERT_PASSPHRASE}"',
    '-out "$OUT" -passout "pass:${AUTHSERVER_CERT_PASSPHRASE}"',
    'openssl pkcs12 -in "$OUT" -noout -passin pass:$PW',
]
KNOWN_BAD_USER_ADD = [
    'mc admin user add local "$$MINIO_APP_ACCESS_KEY" "$$MINIO_APP_SECRET_KEY"',
    "mc admin user add local $(sq \"$access_key\") $(sq \"$secret\") > /dev/null",
]
# Shapes that are fine and must NOT be flagged.
KNOWN_GOOD = [
    'SQLCMDPASSWORD="$SA_PASSWORD" docker compose exec -T -e SQLCMDPASSWORD sql-server sqlcmd -S localhost -U sa -C',
    'export MC_HOST_local="http://$${MINIO_ROOT_USER}:$${MINIO_ROOT_PASSWORD}@minio:9000"',
    'MC_HOST_m="http://${MINIO_USER}:${MINIO_PASS}@minio:9000" docker run --rm -e MC_HOST_m \\',
    'K="$ABP_NUGET_API_KEY" docker run --rm -e K -e P="$PROJECT" -e SLN="$SLN" \\',
    "printf '%s\\n' \"$$MINIO_APP_SECRET_KEY\" | mc admin user add local \"$$MINIO_APP_ACCESS_KEY\"",
    "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q \"SELECT 1\" -o /dev/null",
    # "-P" inside a word is not the password flag: the rule wants -P as its own argument.
    "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -o /var/log/restore-P",
    "as_sa() { docker exec -i \"$container\" bash -c \"SQLCMDPASSWORD=\\\"\\$MSSQL_SA_PASSWORD\\\" ${SQLCMD} -S localhost -U sa -C\"; }",
    '-out "$OUT" -passout env:DATAPROTECTION_CERT_PASSPHRASE',
    'openssl pkcs12 -in "$OUT" -noout -passin env:PW',
]

# Shell, PowerShell and compose files: anything that can run a command.
SCANNED_SUFFIXES = (".sh", ".ps1", ".psm1", ".bash")


def tracked_files():
    out = subprocess.run(["git", "ls-files", "scripts", "docker", "docker-compose*.yml"], cwd=REPO_ROOT,
                         capture_output=True, text=True, encoding="utf-8", check=True).stdout.splitlines()
    return sorted(p for p in out
                  if p.endswith(SCANNED_SUFFIXES) or (p.startswith("docker-compose") and p.endswith(".yml")))


def logical_lines(text):
    """Lines with continuations joined (a trailing backslash in shell, a trailing backtick in PowerShell). Comment
    lines are skipped, because the comments that explain this rule necessarily name the forbidden shapes."""
    joined, buffer = [], ""
    for raw in text.replace("\r\n", "\n").split("\n"):
        stripped = raw.strip()
        if not buffer and stripped.startswith("#"):
            continue
        if stripped.endswith(("\\", "`")):
            buffer += stripped[:-1] + " "
            continue
        joined.append(buffer + stripped)
        buffer = ""
    if buffer:
        joined.append(buffer)
    return joined


def violations(line):
    found = [name for name, pattern in RULES if pattern.search(line)]
    if USER_ADD.search(line) and not USER_ADD_FROM_PIPE.search(line):
        found.append("mc admin user add with the secret as an argument (feed it on stdin)")
    return found


class NoSecretOnArgv(unittest.TestCase):
    def test_no_tracked_script_or_compose_file_puts_a_secret_on_a_command_line(self):
        files = tracked_files()
        found = []
        for rel in files:
            text = (REPO_ROOT / rel).read_text(encoding="utf-8", errors="replace")
            for line in logical_lines(text):
                for rule in violations(line):
                    found.append(f"{rel}: {rule}: {line[:160]}")
        self.assertEqual(found, [], "\n".join(found))

    def test_the_scan_reaches_the_files_that_handle_secrets(self):
        files = set(tracked_files())
        for rel in ("docker-compose.yml", "docker-compose.prod.yml",
                    "scripts/hosting/backup-azure-blob.sh", "scripts/hosting/backup-databases.sh",
                    "scripts/hosting/backup-offbox.sh", "scripts/hosting/rehearse-migrator.sh",
                    "scripts/hosting/sql-simple-recovery.sh", "scripts/hosting/minio-case-tracker-access.sh",
                    "scripts/hosting/office-signature.sh", "scripts/dev/Book-RealAppointment.ps1",
                    "scripts/dev/Seed-PacketParityFixture.ps1", "docker/verify-nuget-scrub.sh"):
            self.assertIn(rel, files)
        self.assertGreater(len(files), 40, "the scan found suspiciously few files")

    def test_every_rule_fires_on_the_lines_this_repository_used_to_carry(self):
        for line in KNOWN_BAD + KNOWN_BAD_USER_ADD:
            with self.subTest(line=line):
                self.assertTrue(violations(logical_lines(line)[0]), f"not caught: {line}")

    def test_the_shapes_that_pass_a_secret_by_name_are_not_flagged(self):
        for line in KNOWN_GOOD:
            with self.subTest(line=line):
                self.assertEqual(violations(logical_lines(line)[0]), [])

    def test_a_continued_command_is_judged_as_one_line(self):
        text = ('sqlc() { docker compose exec -T sql-server \\\n'
                '    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \\\n'
                '    -P "$SA_PASSWORD" -C -b "$@"; }\n')
        self.assertTrue(violations(logical_lines(text)[0]))


if __name__ == "__main__":
    unittest.main()
