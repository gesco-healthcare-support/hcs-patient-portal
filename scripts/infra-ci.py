#!/usr/bin/env python3
"""CI helper for the Azure infrastructure workflow (.github/workflows/infra.yml).

Three subcommands, stdlib only:

  check      Static guards that need no Azure access:
             - every role a resource-group template assigns is in the infra identity's ABAC set
               (infra/azure/subscription/assignable-roles.json), and every entry in that set is either
               assigned or explicitly reserved for a named plan item;
             - infra.yml keeps its safety properties: no pull_request_target, every action pinned by SHA,
               the preview job names no environment, the deploy job names azure-production-infra.
  params     Writes a deployment parameters file for either the pull-request preview (placeholders for
             secure values, everything else from the last real deploy) or the deploy (from the
             environment's secrets and variables).
  summarize  Turns `az ... what-if --result-format ResourceIdOnly` JSON into a summary that is safe to
             post publicly: change counts and resource names, never property values.

WHY SO CAREFUL ABOUT OUTPUT: the repository is public, and so are its workflow logs, artifacts and PR
comments. A default what-if prints every changed property, office allow-list addresses included.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from collections import Counter
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
ASSIGNABLE_ROLES = "infra/azure/subscription/assignable-roles.json"
WORKFLOW = ".github/workflows/infra.yml"

GUID_LITERAL = re.compile(r"'([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})'")
USES_LINE = re.compile(r"^\s*(?:-\s*)?uses:\s*(\S+)")
PINNED = re.compile(r"^[^@\s]+@[0-9a-f]{40}$")
JOB_KEY = re.compile(r"^  ([A-Za-z0-9_-]+):\s*$")

SUMMARY_MARKER = "<!-- infra-what-if -->"
CHANGE_ORDER = ["Create", "Modify", "Delete", "Deploy", "Ignore", "NoChange", "Unsupported"]
SECURE_TYPES = {"securestring", "secureobject"}
ZERO_GUID = "00000000-0000-0000-0000-000000000000"

# Template parameter -> the GitHub environment secret or variable that supplies it on deploy.
ENV_MAP = {
    "envName": "ENV_NAME",
    "baseDomain": "BASE_DOMAIN",
    "sqlAdminLogin": "SQL_ADMIN_LOGIN",
    "sqlAdminPassword": "SQL_ADMIN_PASSWORD",
    "sqlEntraAdminObjectId": "SQL_ENTRA_ADMIN_OBJECT_ID",
    "sqlEntraAdminName": "SQL_ENTRA_ADMIN_NAME",
    "adminSshPublicKey": "ADMIN_SSH_PUBLIC_KEY",
    "deployGateway": "DEPLOY_GATEWAY",
    "tlsCertificateSecretId": "TLS_CERTIFICATE_SECRET_ID",
    "wafMode": "WAF_MODE",
    "createDnsZone": "CREATE_DNS_ZONE",
    "allowedClientCidrs": "ALLOWED_CLIENT_CIDRS",
    "adminAllowedIpRanges": "ADMIN_ALLOWED_IP_RANGES",
}


class ParameterError(ValueError):
    """A deploy parameter is missing or malformed. The message names the variable, never its value."""


# ---------------------------------------------------------------- check: roles


def resource_group_templates(root: Path) -> list[Path]:
    """The templates CI deploys as the infra identity. The subscription layer is deliberately excluded."""
    azure = root / "infra" / "azure"
    return [azure / "main.bicep", *sorted((azure / "modules").glob("*.bicep"))]


def assigned_roles(paths: list[Path]) -> dict[str, list[str]]:
    """Every GUID literal in the given templates, with where it appears. Each one is a role definition id."""
    found: dict[str, list[str]] = {}
    for path in paths:
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
            for guid in GUID_LITERAL.findall(line):
                found.setdefault(guid, []).append(f"{path.name}:{number}")
    return found


def check_roles(assigned: dict[str, list[str]], assignable: dict[str, dict]) -> list[str]:
    """Both directions: nothing assigned outside the set, nothing in the set without a reason to be there."""
    errors = []
    for guid, where in sorted(assigned.items()):
        if guid not in assignable:
            errors.append(
                f"{guid} is assigned at {', '.join(where)} but is not in {ASSIGNABLE_ROLES}; "
                "the deploy would fail with AuthorizationFailed. Add it there and re-run the bootstrap."
            )
    for guid, entry in sorted(assignable.items()):
        if not entry.get("role") or not entry.get("usedBy"):
            errors.append(f"{guid} in {ASSIGNABLE_ROLES} needs both 'role' and 'usedBy'.")
        reserved = entry.get("reserved")
        if guid in assigned and reserved:
            errors.append(f"{guid} ({entry.get('role')}) is now assigned; remove its 'reserved' marker.")
        if guid not in assigned and not reserved:
            errors.append(
                f"{guid} ({entry.get('role')}) is in the assignable set but no template assigns it. "
                "Remove it, or mark it 'reserved' with the plan item that will use it."
            )
    return errors


# ---------------------------------------------------------------- check: workflow


def job_blocks(workflow: str) -> dict[str, str]:
    """Top-level jobs of a workflow, keyed by job id. Assumes the repository's two-space indentation."""
    blocks: dict[str, list[str]] = {}
    current = None
    in_jobs = False
    for line in workflow.splitlines():
        if not line.startswith(" ") and line.strip():
            in_jobs = line.rstrip() == "jobs:"
            current = None
            continue
        match = JOB_KEY.match(line) if in_jobs else None
        if match:
            current = match.group(1)
            blocks[current] = []
        elif current:
            blocks[current].append(line)
    return {name: "\n".join(lines) for name, lines in blocks.items()}


def unpinned_actions(workflow: str) -> list[str]:
    """Every `uses:` that is neither a local action nor pinned to a 40-hex commit SHA."""
    errors = []
    for number, line in enumerate(workflow.splitlines(), start=1):
        match = USES_LINE.match(line)
        if match and not match.group(1).startswith("./") and not PINNED.match(match.group(1)):
            errors.append(f"infra.yml:{number} is not pinned to a full commit SHA: {match.group(1)}")
    return errors


def job_errors(jobs: dict[str, str]) -> list[str]:
    """Which job may hold which identity."""
    errors = []
    for required in ("bicep", "what-if", "deploy"):
        if required not in jobs:
            errors.append(f"infra.yml has no '{required}' job.")
    if "environment:" in jobs.get("what-if", ""):
        errors.append("the what-if job must name no environment, or its OIDC subject stops being pull_request.")
    if "environment: azure-production-infra" not in jobs.get("deploy", ""):
        errors.append("the deploy job must name environment azure-production-infra.")
    for name, block in jobs.items():
        if "id-token: write" in block and name not in ("what-if", "deploy"):
            errors.append(f"job '{name}' requests an OIDC token; only what-if and deploy may.")
    return errors


def check_workflow(workflow: str) -> list[str]:
    """The properties of infra.yml that a well-meaning edit could quietly break."""
    errors = []
    code = "\n".join(line for line in workflow.splitlines() if not line.lstrip().startswith("#"))
    if "pull_request_target" in code:
        errors.append("infra.yml must never use pull_request_target: it runs fork code with base-repo rights.")
    errors += unpinned_actions(workflow)
    errors += job_errors(job_blocks(workflow))
    if not re.search(r"^permissions:\n  contents: read$", workflow, re.MULTILINE):
        errors.append("infra.yml must default to 'permissions: contents: read' at the top level.")
    return errors


def cmd_check(root: Path) -> int:
    assignable = json.loads((root / ASSIGNABLE_ROLES).read_text(encoding="utf-8"))["roles"]
    errors = check_roles(assigned_roles(resource_group_templates(root)), assignable)
    errors += check_workflow((root / WORKFLOW).read_text(encoding="utf-8"))
    for error in errors:
        print(f"::error::{error}")
    if not errors:
        print(f"infra checks passed: {len(assignable)} assignable roles, workflow guards intact.")
    return 1 if errors else 0


# ---------------------------------------------------------------- params


def placeholder(declaration: dict):
    """A value the preview can type-check but that cannot be mistaken for, or leak, a real one."""
    kind = declaration["type"].lower()
    if kind in SECURE_TYPES:
        return "placeholder-not-evaluated" if kind == "securestring" else {}
    if kind == "string":
        return ZERO_GUID if declaration.get("minLength") == 36 else "placeholder.invalid"
    return {"bool": False, "int": declaration.get("minValue", 0), "array": [], "object": {}}[kind]


def is_unfilled(value) -> bool:
    return isinstance(value, str) and value.startswith("REPLACE")


def pr_parameters(declarations: dict, last_deployed: dict | None, example: dict) -> dict:
    """Preview values: placeholders for secure parameters, otherwise the last real deploy, else the example."""
    values = {}
    last_deployed = last_deployed or {}
    for name, declaration in declarations.items():
        if declaration["type"].lower() in SECURE_TYPES:
            values[name] = placeholder(declaration)
        elif "value" in last_deployed.get(name, {}):
            values[name] = last_deployed[name]["value"]
        elif "defaultValue" in declaration:
            # Also covers a parameter added since the last deploy: the template default is what a deploy would use.
            continue
        elif name in example and not is_unfilled(example[name].get("value")):
            values[name] = example[name]["value"]
        else:
            values[name] = placeholder(declaration)
    return values


def convert(raw: str, kind: str, variable: str):
    kind = kind.lower()
    if kind in ("string", "securestring"):
        return raw
    if kind == "bool":
        if raw.lower() not in ("true", "false"):
            raise ParameterError(f"{variable} must be true or false.")
        return raw.lower() == "true"
    try:
        return int(raw) if kind == "int" else json.loads(raw)
    except ValueError as exc:
        raise ParameterError(f"{variable} is not valid {kind}.") from exc


def deploy_parameters(declarations: dict, environ: dict) -> dict:
    """Deploy values from the environment. Missing required values fail with the variable's NAME only."""
    values = {}
    for name, declaration in declarations.items():
        variable = ENV_MAP.get(name)
        raw = environ.get(variable, "") if variable else ""
        if raw == "":
            if "defaultValue" not in declaration:
                source = variable or f"(no mapping for {name} in scripts/infra-ci.py ENV_MAP)"
                raise ParameterError(f"required parameter {name} has no value: set {source}.")
            continue
        values[name] = convert(raw, declaration["type"], variable)
    if values.get("deployGateway") and not values.get("tlsCertificateSecretId"):
        raise ParameterError("DEPLOY_GATEWAY is true but TLS_CERTIFICATE_SECRET_ID is empty; the gateway cannot start.")
    for name, variable in ENV_MAP.items():
        if environ.get(variable) and name not in declarations:
            print(f"::notice::{variable} is set but the template declares no {name}; ignored.")
    return values


def parameters_file(values: dict) -> dict:
    return {
        "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
        "contentVersion": "1.0.0.0",
        "parameters": {name: {"value": value} for name, value in values.items()},
    }


def read_json(path: str | None):
    """A JSON file, or None when the path is absent or empty (no earlier deploy yet)."""
    if not path or not Path(path).is_file() or not Path(path).read_text(encoding="utf-8").strip():
        return None
    return json.loads(Path(path).read_text(encoding="utf-8"))


def cmd_params(args: argparse.Namespace) -> int:
    declarations = json.loads(Path(args.template).read_text(encoding="utf-8"))["parameters"]
    try:
        if args.mode == "pr":
            example = json.loads(Path(args.example).read_text(encoding="utf-8"))["parameters"]
            values = pr_parameters(declarations, read_json(args.last), example)
        else:
            values = deploy_parameters(declarations, dict(os.environ))
    except ParameterError as exc:
        print(f"::error::{exc}")
        return 1
    Path(args.out).write_text(json.dumps(parameters_file(values), indent=2), encoding="utf-8")
    print(f"wrote {len(values)} parameters ({args.mode}); values not printed.")
    return 0


# ---------------------------------------------------------------- summarize


def resource_label(resource_id: str) -> str:
    """Type and name only. Everything before the first /providers/ holds the subscription id and is dropped."""
    marker = "/providers/"
    if marker not in resource_id:
        return "(resource group)"
    return resource_id[resource_id.index(marker) + len(marker):]


def summarize(result: dict, title: str) -> tuple[str, bool]:
    """Markdown safe to publish, and whether the what-if succeeded."""
    changes = result.get("changes") or []
    counts = Counter(change.get("changeType", "Unknown") for change in changes)
    ok = str(result.get("status", "")).lower() == "succeeded"
    lines = [SUMMARY_MARKER, f"### Infra what-if: {title}", ""]
    if not ok:
        code = (result.get("error") or {}).get("code", "unknown")
        lines += [f"**What-if did not succeed** (status `{result.get('status')}`, error `{code}`).",
                  "The full error is in the job log; it is not repeated here.", ""]
    lines += ["| change | count |", "| --- | --- |"]
    for kind in sorted(counts, key=lambda k: CHANGE_ORDER.index(k) if k in CHANGE_ORDER else len(CHANGE_ORDER)):
        lines.append(f"| {kind} | {counts[kind]} |")
    lines.append("")
    for change in changes:
        if change.get("changeType") not in ("NoChange", "Ignore"):
            lines.append(f"- {change.get('changeType')}: `{resource_label(change.get('resourceId', ''))}`")
    lines += [
        "",
        "Incremental mode: resources removed from the template are not deleted. "
        "`Ignore` means the resource exists in Azure but not in the template.",
        "Property values are not shown: this repository and its logs are public. Run what-if locally for the diff.",
    ]
    return "\n".join(lines) + "\n", ok


def cmd_summarize(args: argparse.Namespace) -> int:
    result = json.loads(Path(args.whatif).read_text(encoding="utf-8"))
    text, ok = summarize(result, args.title)
    Path(args.out).write_text(text, encoding="utf-8")
    if not ok:
        print(json.dumps(result.get("error"), indent=2))
    return 0 if ok else 1


# ---------------------------------------------------------------- entry point


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("check", help="static guards; no Azure access")
    params = commands.add_parser("params", help="write a deployment parameters file")
    params.add_argument("--mode", choices=["pr", "deploy"], required=True)
    params.add_argument("--template", required=True, help="compiled ARM JSON of main.bicep")
    params.add_argument("--out", required=True)
    params.add_argument("--last", help="pr mode: parameters of the last real deploy (may be absent)")
    params.add_argument("--example", default=str(REPO_ROOT / "infra/azure/main.parameters.example.json"))
    summary = commands.add_parser("summarize", help="publishable what-if summary")
    summary.add_argument("--whatif", required=True)
    summary.add_argument("--title", required=True)
    summary.add_argument("--out", required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    if args.command == "check":
        return cmd_check(REPO_ROOT)
    if args.command == "params":
        return cmd_params(args)
    return cmd_summarize(args)


if __name__ == "__main__":
    sys.exit(main())
