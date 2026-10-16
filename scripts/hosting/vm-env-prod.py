#!/usr/bin/env python3
"""Write, then check, the Azure VM's secrets/env.prod (go-live runbook, "secrets/env.prod for the VM").

    python3 scripts/hosting/vm-env-prod.py generate [--out secrets/env.prod] [--alert-email ADDRESS]
    python3 scripts/hosting/vm-env-prod.py check [secrets/env.prod]

Run both from the repository root on the VM. Stdlib only, so it runs on a fresh VM with nothing installed.

WHY IT EXISTS. The VM's env file was a page of hand edits, and three of them fail silently:

  - an EMPTY MINIO_ALLOW_DIRECTIVES or DOCS_ALLOW_DIRECTIVES does not close anything: compose's ":-" default
    swaps it for the private address ranges (measured with `docker compose config`, 2026-10-13);
  - MinIO refuses an access key over 20 characters or a secret over 40, so `openssl rand -hex 32` (64) is too
    long. It already happened once, on the office server;
  - the feed token and the integration token must differ (the feed only reads, the integration token also
    closes appointments through attendance), and nothing in the application checks it.

GENERATE writes a NEW file from env.prod.example, so it never drifts from the documented keys: every key there
must have a decision below, and a key below that the example no longer has is refused too. Each secret is drawn
from `secrets` within the limits of what consumes it. What only a person can supply (the ABP licence, the SMTP
account, the backup account name the template outputs) is left as CHANGE_ME. The file is created mode 600 and
an existing one is never overwritten.

CHECK refuses a file that would bring the VM up unsafe or not at all. It names keys and lengths, never values.

Exit codes: 0 done / passes; 1 refused (a problem with the file, or an existing file); 2 bad input (a missing
file or directory, or an example this script does not account for).
"""

from __future__ import annotations

import argparse
import os
import re
import secrets
import stat
import sys
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
EXAMPLE = REPO_ROOT / "env.prod.example"
DEFAULT_ENV_FILE = Path("secrets") / "env.prod"
DEFAULT_BASE_DOMAIN = "medlegal.evaluators.com"

PLACEHOLDER = "CHANGE_ME"
DENY_ALL = "deny all;"

# How each key of env.prod.example is written for the VM.
KEEP = "keep"            # the example's line, unchanged (an optional "# KEY=" line stays commented)
HUMAN = "human"          # CHANGE_ME_<hint>: a person fills it in
SECRET = "secret"        # generated here
ALERT = "alert"          # the --alert-email address, else CHANGE_ME

ACTIVE_LINE = re.compile(r"^([A-Z][A-Z0-9_]*)=(.*)$")
OPTIONAL_LINE = re.compile(r"^# ([A-Z][A-Z0-9_]*)=(.*)$")


def _hex(nbytes: int) -> str:
    return secrets.token_hex(nbytes)


# SQL Server's policy wants upper, lower and digits; the fixed ends guarantee them, and nothing here can break a
# connection string (; ' " { }) or shell quoting ($ `).
SECRET_MAKERS = {
    "AUTHSERVER_CERT_PASSPHRASE": lambda: _hex(24),
    "DATAPROTECTION_CERT_PASSPHRASE": lambda: _hex(24),
    "MSSQL_SA_PASSWORD": lambda: f"Sa-{_hex(16)}-Q7",
    "SQL_APP_PASSWORD": lambda: f"Pp-{_hex(16)}-Z9",
    "STRING_ENCRYPTION_PASSPHRASE": lambda: _hex(16),
    "MINIO_ROOT_USER": lambda: f"minioroot{_hex(5)}",        # 19 characters, MinIO allows 20
    "MINIO_ROOT_PASSWORD": lambda: _hex(20),                 # 40 characters, MinIO allows 40
    "MINIO_APP_ACCESS_KEY": lambda: f"portalapp{_hex(5)}",   # 19
    "MINIO_APP_SECRET_KEY": lambda: _hex(20),                # 40
    "CASE_TRACKER_INTEGRATION_TOKEN": lambda: _hex(32),
    "CASE_TRACKER_FEED_TOKEN": lambda: _hex(32),
}

# A str value is written as it stands ("" writes an empty value, which is meant: those settings are OFF here).
PLAN: dict[str, str] = {
    "BASE_DOMAIN": "{base_domain}",
    "APP_NAME": KEEP, "HTTP_PORT": KEEP, "HTTPS_PORT": KEEP,
    # nginx is the first hop on the VM, so nothing may override the caller's address.
    "TRUSTED_PROXY_SET_REAL_IP_FROM": "", "TRUSTED_PROXY_REAL_IP_RECURSIVE": "",
    # CLOSED. Empty would NOT be: see the module docstring.
    "MINIO_ALLOW_DIRECTIVES": f'"{DENY_ALL}"', "DOCS_ALLOW_DIRECTIVES": f'"{DENY_ALL}"',
    "TLS_CERT_PATH": KEEP, "TLS_KEY_PATH": KEEP, "ACME_EMAIL": ALERT,
    "OPENIDDICT_PFX_PATH": KEEP, "AUTHSERVER_CERT_PASSPHRASE": SECRET,
    "DATAPROTECTION_PFX_PATH": KEEP, "DATAPROTECTION_CERT_PASSPHRASE": SECRET,
    "MSSQL_SA_PASSWORD": SECRET, "SQL_APP_LOGIN": "portal_app", "SQL_APP_PASSWORD": SECRET,
    "STRING_ENCRYPTION_PASSPHRASE": SECRET,
    "MINIO_ROOT_USER": SECRET, "MINIO_ROOT_PASSWORD": SECRET, "MINIO_BUCKET_NAME": KEEP,
    "MINIO_APP_ACCESS_KEY": SECRET, "MINIO_APP_SECRET_KEY": SECRET,
    "MINIO_CASE_TRACKER_BUCKET_NAME": KEEP, "MINIO_REGION": KEEP,
    "ABP_LICENSE_CODE": HUMAN, "ABP_NUGET_API_KEY": HUMAN,
    "SMTP_HOST": HUMAN, "SMTP_PORT": KEEP, "SMTP_ENABLE_SSL": KEEP, "SMTP_USERNAME": HUMAN,
    "SMTP_PASSWORD": HUMAN, "SMTP_FROM_ADDRESS": HUMAN, "SMTP_FROM_NAME": KEEP,
    "BACKUP_ALERT_RECIPIENTS": ALERT,
    # The Case Tracker PULLS the feed here, so the push stays unset.
    "CASE_TRACKER_BASE_URL": "", "CASE_TRACKER_INTAKE_TOKEN": "",
    "CASE_TRACKER_INTEGRATION_TOKEN": SECRET, "CASE_TRACKER_FEED_TOKEN": SECRET,
    "CASE_TRACKER_FEED_ALERT_RECIPIENTS": ALERT, "CASE_TRACKER_TIMEOUT_SECONDS": KEEP,
    "ADMIN_PASSWORD_DIRECTORY": KEEP, "ADMIN_PASSWORD_VAULT_URI": KEEP,
    "DBMIGRATOR_ENVIRONMENT": "Production",
    "BACKUP_DIR": "./backups", "BACKUP_RETENTION_DAYS": KEEP,
    "AZURE_BACKUP_STORAGE_ACCOUNT": HUMAN, "AZURE_BACKUP_DB_CONTAINER": KEEP,
    "AZURE_BACKUP_DOCUMENTS_CONTAINER": KEEP, "AZURE_BACKUP_BUCKETS": KEEP, "AZURE_BACKUP_MSI_CLIENT_ID": KEEP,
    # Azure Relay is switched on after go-live (runbook T7); its keys stay commented until then.
    "RELAY_NAMESPACE": KEEP, "RELAY_HYBRID_CONNECTION": KEEP, "RELAY_AZBRIDGE_IMAGE": KEEP,
    "RELAY_AZBRIDGE_IMAGE_ID": KEEP, "RELAY_LISTEN_CONNECTION_STRING_FILE": KEEP,
    # Not an env key: the example's description of the fallback file's content, kept as written.
    "AZURE_BRIDGE_CONNECTIONSTRING": KEEP,
    "MSSQL_PID": "Express",
    "MSSQL_MEMORY_LIMIT_MB": KEEP, "SQL_MEM_LIMIT": KEEP, "API_MEM_LIMIT": KEEP,
    "AUTHSERVER_MEM_LIMIT": KEEP, "PACKET_RENDERER_MEM_LIMIT": KEEP, "CLAMAV_MEM_LIMIT": KEEP,
}

HUMAN_HINTS = {
    "ABP_LICENSE_CODE": "same_org_licence_as_the_office_server",
    "ABP_NUGET_API_KEY": "same_org_nuget_key_as_the_office_server",
    "SMTP_HOST": "smtp_host", "SMTP_USERNAME": "smtp_user", "SMTP_PASSWORD": "smtp_password",
    "SMTP_FROM_ADDRESS": "from_address",
    "AZURE_BACKUP_STORAGE_ACCOUNT": "backupStorageAccountName_template_output",
}


class BadInput(Exception):
    """The inputs are wrong (exit 2), as opposed to a file that fails a check (exit 1)."""


# ---------------------------------------------------------------------------------------------------- generate


def _value_for(key: str, action: str, options: argparse.Namespace, made: dict[str, str]) -> str:
    if action == SECRET:
        made[key] = SECRET_MAKERS[key]()
        return made[key]
    if action == HUMAN:
        return f"{PLACEHOLDER}_{HUMAN_HINTS[key]}"
    if action == ALERT:
        return options.alert_email or f"{PLACEHOLDER}_alert_address"
    return action.format(base_domain=options.base_domain)


def render(example_text: str, options: argparse.Namespace) -> tuple[str, dict[str, str]]:
    """The VM's env file built from the example's lines, and the secrets it generated (for the tests only)."""
    unknown, seen, out, made = [], set(), [], {}
    for line in example_text.splitlines():
        match = ACTIVE_LINE.match(line) or OPTIONAL_LINE.match(line)
        if not match:
            out.append(line)
            continue
        key = match.group(1)
        if key in seen:
            raise BadInput(f"env.prod.example names {key} twice; this script cannot tell which line is meant")
        seen.add(key)
        action = PLAN.get(key)
        if action is None:
            unknown.append(key)
        out.append(line if action in (None, KEEP) else f"{key}={_value_for(key, action, options, made)}")
    if unknown:
        raise BadInput("env.prod.example has keys this script does not know: " + ", ".join(unknown)
                       + ". Give each a decision in PLAN (scripts/hosting/vm-env-prod.py).")
    stale = sorted(set(PLAN) - seen)
    if stale:
        raise BadInput("PLAN names keys env.prod.example no longer has: " + ", ".join(stale))
    if made["CASE_TRACKER_FEED_TOKEN"] == made["CASE_TRACKER_INTEGRATION_TOKEN"]:
        raise BadInput("the two Case Tracker tokens came out equal; run it again")
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    header = [f"# Written for the Azure VM by scripts/hosting/vm-env-prod.py generate, {stamp}.",
              "# Fill in every CHANGE_ME, then run: python3 scripts/hosting/vm-env-prod.py check", ""]
    return "\n".join(header + out) + "\n", made


def _write_new(path: Path, text: str) -> None:
    """Create the file mode 600 in one step, refusing to replace an existing one."""
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    if os.name == "posix":
        os.chmod(path, 0o600)  # the umask can only narrow the mode; this pins it


def generate(options: argparse.Namespace) -> int:
    out = Path(options.out)
    if not out.parent.is_dir():
        raise BadInput(f"{out.parent} does not exist; create it first: install -d -m 700 {out.parent}")
    text, made = render(Path(options.example).read_text(encoding="utf-8"), options)
    try:
        _write_new(out, text)
    except FileExistsError:
        print(f"REFUSED: {out} already exists. This script never overwrites one; move it aside yourself.")
        return 1
    left = sorted(key for key, value in parse(text)[0].items() if PLACEHOLDER in value)
    print(f"wrote {out} (mode 600) with {len(made)} generated secrets; no value is printed.")
    print("still to fill in: " + (", ".join(left) if left else "nothing"))
    print(f"then run: python3 scripts/hosting/vm-env-prod.py check {out}")
    return 0


# ------------------------------------------------------------------------------------------------------- check


def _unquote(raw: str) -> str:
    value = raw.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
        return value[1:-1]
    return value.split(" #", 1)[0].rstrip()


def parse(text: str) -> tuple[dict[str, str], list[str]]:
    """Compose env-file values by key, and the problems with the file's shape (never a value)."""
    values, where, problems = {}, {}, []
    if "\r" in text:
        problems.append("the file has Windows line endings; convert it with: sed -i 's/\\r$//' <file>")
    for number, line in enumerate(text.splitlines(), start=1):
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        match = ACTIVE_LINE.match(stripped)
        if not match:
            problems.append(f"line {number} is not KEY=value")
            continue
        key = match.group(1)
        if key in where:
            problems.append(f"{key} is set twice (lines {where[key]} and {number}): compose takes the last, "
                            "some hosting scripts the first")
        where[key] = number
        values[key] = _unquote(match.group(2))
    return values, problems


REQUIRED = (
    "BASE_DOMAIN", "TLS_CERT_PATH", "TLS_KEY_PATH", "OPENIDDICT_PFX_PATH", "AUTHSERVER_CERT_PASSPHRASE",
    "DATAPROTECTION_PFX_PATH", "DATAPROTECTION_CERT_PASSPHRASE", "MSSQL_SA_PASSWORD", "SQL_APP_LOGIN",
    "SQL_APP_PASSWORD", "STRING_ENCRYPTION_PASSPHRASE", "MINIO_ROOT_USER", "MINIO_ROOT_PASSWORD",
    "MINIO_BUCKET_NAME", "MINIO_APP_ACCESS_KEY", "MINIO_APP_SECRET_KEY", "ABP_LICENSE_CODE", "ABP_NUGET_API_KEY",
    "SMTP_HOST", "SMTP_FROM_ADDRESS", "BACKUP_ALERT_RECIPIENTS", "CASE_TRACKER_INTEGRATION_TOKEN",
    "CASE_TRACKER_FEED_TOKEN", "ADMIN_PASSWORD_DIRECTORY", "AZURE_BACKUP_STORAGE_ACCOUNT",
)

# Exactly this value, or the VM comes up open or wrong. The text says what an absent key falls back to.
EXACT = {
    "MINIO_ALLOW_DIRECTIVES": (DENY_ALL, "absent or empty, compose opens the MinIO vhost to the private ranges"),
    "DOCS_ALLOW_DIRECTIVES": (DENY_ALL, "absent or empty, compose opens the docs site to the private ranges"),
    "MSSQL_PID": ("Express", "absent, SQL Server runs as Developer, licensed for development and test only"),
    "BACKUP_DIR": ("./backups", "deploy-release.sh and rehearse-migrator.sh hard-code ./backups"),
}

# (minimum, maximum) characters. MinIO's own limits: access key 3-20, secret 8-40.
LENGTHS = {
    "MINIO_ROOT_USER": (3, 20), "MINIO_APP_ACCESS_KEY": (3, 20),
    "MINIO_ROOT_PASSWORD": (8, 40), "MINIO_APP_SECRET_KEY": (8, 40),
    "SQL_APP_PASSWORD": (16, 128), "MSSQL_SA_PASSWORD": (8, 128), "STRING_ENCRYPTION_PASSPHRASE": (16, 512),
}

UNSAFE_IN_SQL_PASSWORD = re.compile(r"[;'\"{}$`\s]")


def _placeholders(values: dict[str, str]) -> list[str]:
    return [f"{key} still holds a placeholder" for key, value in values.items()
            if PLACEHOLDER in value or (value.startswith("<") and value.endswith(">"))]


def _required(values: dict[str, str]) -> list[str]:
    return [f"{key} is missing or empty" for key in REQUIRED if not values.get(key)]


def _exact(values: dict[str, str]) -> list[str]:
    problems = []
    for key, (want, consequence) in EXACT.items():
        if key not in values or not values[key]:
            problems.append(f"{key} must be {want!r}: {consequence}")
        elif values[key] != want:
            problems.append(f"{key} must be exactly {want!r}")
    return problems


def _environment(values: dict[str, str]) -> list[str]:
    # Absent is fine: compose defaults the migrator to Production. Development there runs the demo seeders.
    problems = []
    if values.get("DBMIGRATOR_ENVIRONMENT", "Production") != "Production":
        problems.append("DBMIGRATOR_ENVIRONMENT must be Production: Development runs the demo seeders")
    if values.get("ADMIN_PASSWORD_VAULT_URI"):
        problems.append("ADMIN_PASSWORD_VAULT_URI must be empty: this build refuses the vault store")
    return problems


def _tokens(values: dict[str, str]) -> list[str]:
    feed, integration = values.get("CASE_TRACKER_FEED_TOKEN"), values.get("CASE_TRACKER_INTEGRATION_TOKEN")
    if feed and feed == integration:
        return ["CASE_TRACKER_FEED_TOKEN equals CASE_TRACKER_INTEGRATION_TOKEN: the feed token must not also "
                "authorise attendance, which closes appointments"]
    return []


def _lengths(values: dict[str, str]) -> list[str]:
    problems = []
    for key, (low, high) in LENGTHS.items():
        size = len(values.get(key) or "")
        if size and not low <= size <= high:
            problems.append(f"{key} is {size} characters; it must be {low}-{high}")
    if values.get("MINIO_APP_ACCESS_KEY") and values.get("MINIO_APP_ACCESS_KEY") == values.get("MINIO_ROOT_USER"):
        problems.append("MINIO_APP_ACCESS_KEY must differ from MINIO_ROOT_USER: the app must not hold root")
    return problems


def _sql(values: dict[str, str]) -> list[str]:
    problems = []
    login = values.get("SQL_APP_LOGIN", "")
    if login.lower() == "sa" or (login and not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]{2,63}", login)):
        problems.append("SQL_APP_LOGIN must be a plain login name and not sa")
    for key in ("SQL_APP_PASSWORD", "MSSQL_SA_PASSWORD"):
        password = values.get(key, "")
        if not password:
            continue
        if UNSAFE_IN_SQL_PASSWORD.search(password):
            problems.append(f"{key} must not contain ; ' \" {{ }} $ ` or spaces")
        if not (re.search(r"[A-Z]", password) and re.search(r"[a-z]", password) and re.search(r"\d", password)):
            problems.append(f"{key} needs upper case, lower case and digits (SQL Server's password policy)")
    return problems


RULES = (_placeholders, _required, _exact, _environment, _tokens, _lengths, _sql)


def _mode_problems(path: Path) -> list[str]:
    if os.name != "posix":
        return []
    mode = stat.S_IMODE(path.stat().st_mode)
    return [f"{path} is mode {mode:03o}; it holds every secret of the VM: chmod 600 {path}"] if mode & 0o077 else []


def check_text(text: str) -> list[str]:
    values, problems = parse(text)
    for rule in RULES:
        problems.extend(rule(values))
    return problems


def check(options: argparse.Namespace) -> int:
    path = Path(options.env_file)
    if not path.is_file():
        raise BadInput(f"{path} does not exist")
    problems = _mode_problems(path) + check_text(path.read_text(encoding="utf-8"))
    for problem in problems:
        print(f"REFUSED: {problem}")
    if problems:
        print(f"{len(problems)} problem(s) in {path}; fix them and run check again. No value was printed.")
        return 1
    print(f"OK: {path} passes every check ({len(RULES) + 1} groups).")
    return 0


# -------------------------------------------------------------------------------------------------------- main


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n", 1)[0])
    commands = parser.add_subparsers(dest="command", required=True)
    gen = commands.add_parser("generate", help="write a NEW env file for the VM")
    gen.add_argument("--out", default=str(DEFAULT_ENV_FILE))
    gen.add_argument("--example", default=str(EXAMPLE))
    gen.add_argument("--base-domain", default=DEFAULT_BASE_DOMAIN)
    gen.add_argument("--alert-email", default="", help="ACME, backup and feed alerts; else left as CHANGE_ME")
    chk = commands.add_parser("check", help="refuse an env file that would bring the VM up unsafe")
    chk.add_argument("env_file", nargs="?", default=str(DEFAULT_ENV_FILE))
    return parser


def main(argv: list[str] | None = None) -> int:
    options = build_parser().parse_args(argv)
    try:
        return generate(options) if options.command == "generate" else check(options)
    except (BadInput, OSError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
