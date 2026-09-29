"""Tests for scripts/infra-ci.py, the Azure infrastructure workflow's helper (A2).

Its failure modes are quiet ones, which is why each guard is tested from both sides:

- the role drift check passing while a template assigns a role CI may not grant (the deploy then fails with
  AuthorizationFailed, in production, at the first real run);
- the workflow guard passing while infra.yml gains an unpinned action or an environment on the preview job;
- a parameter or an error message leaking into a PUBLIC log or PR comment.
"""

import json
import pathlib
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO

from gate_loader import REPO_ROOT, load

infra = load("infra_ci", "scripts/infra-ci.py")

PIN = "11d5960a326750d5838078e36cf38b85af677262"
ROLE_A = "7f951dda-4ed3-4680-a7ca-43fe172d538d"
ROLE_B = "b86a8fe4-44ce-4948-aee5-eccb2c155cd7"

GOOD_WORKFLOW = f"""name: Infra
on:
  pull_request:
# never pull_request_target
permissions:
  contents: read
jobs:
  bicep:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@{PIN}  # v4.4.0
      - uses: ./.github/actions/local
  what-if:
    permissions:
      id-token: write
    steps:
      - uses: azure/login@{PIN}  # v3.1.0
  deploy:
    environment: azure-production-infra
    permissions:
      id-token: write
"""


def quiet(function, *args):
    """Run a function with its stdout captured; return (result, output)."""
    buffer = StringIO()
    with redirect_stdout(buffer):
        result = function(*args)
    return result, buffer.getvalue()


def streams(function, *args):
    """Run a function with stdout and stderr captured separately; return (result, stdout, stderr)."""
    out, err = StringIO(), StringIO()
    with redirect_stdout(out), redirect_stderr(err):
        result = function(*args)
    return result, out.getvalue(), err.getvalue()


class TempTree(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = pathlib.Path(self._tmp.name)

    def write(self, relative, body):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(body, encoding="utf-8")
        return path


class AssignedRoles(TempTree):
    def test_finds_every_guid_literal_with_its_location(self):
        main = self.write("infra/azure/main.bicep", f"var a = '{ROLE_A}'\n\nvar b = '{ROLE_B}'\n")
        module = self.write("infra/azure/modules/x.bicep", f"var again = '{ROLE_A}'\n")
        found = infra.assigned_roles([main, module])
        self.assertEqual(found[ROLE_A], ["main.bicep:1", "x.bicep:1"])
        self.assertEqual(found[ROLE_B], ["main.bicep:3"])

    def test_resource_group_layer_excludes_the_subscription_layer(self):
        self.write("infra/azure/main.bicep", "")
        self.write("infra/azure/modules/b.bicep", "")
        self.write("infra/azure/modules/a.bicep", "")
        self.write("infra/azure/subscription/bootstrap.bicep", "")
        names = [p.relative_to(self.root).as_posix() for p in infra.resource_group_templates(self.root)]
        self.assertEqual(names, ["infra/azure/main.bicep", "infra/azure/modules/a.bicep", "infra/azure/modules/b.bicep"])


class CheckRoles(unittest.TestCase):
    def test_clean_when_assigned_and_reserved_entries_are_accounted_for(self):
        assignable = {ROLE_A: {"role": "AcrPull", "usedBy": "A1"},
                      ROLE_B: {"role": "Officer", "usedBy": "A3", "reserved": "A3"}}
        self.assertEqual(infra.check_roles({ROLE_A: ["main.bicep:1"]}, assignable), [])

    def test_assigned_role_outside_the_set_fails_and_names_where(self):
        errors = infra.check_roles({ROLE_A: ["main.bicep:9"]}, {})
        self.assertEqual(len(errors), 1)
        self.assertIn("main.bicep:9", errors[0])
        self.assertIn("AuthorizationFailed", errors[0])

    def test_unused_unreserved_entry_fails(self):
        errors = infra.check_roles({}, {ROLE_A: {"role": "AcrPull", "usedBy": "A1"}})
        self.assertEqual(len(errors), 1)
        self.assertIn("reserved", errors[0])

    def test_reservation_must_be_removed_once_the_role_is_used(self):
        errors = infra.check_roles({ROLE_B: ["x:1"]}, {ROLE_B: {"role": "Officer", "usedBy": "A3", "reserved": "A3"}})
        self.assertEqual(len(errors), 1)
        self.assertIn("remove its 'reserved'", errors[0])

    def test_entry_needs_role_and_used_by(self):
        errors = infra.check_roles({ROLE_A: ["x:1"]}, {ROLE_A: {"role": "AcrPull"}})
        self.assertEqual(len(errors), 1)
        self.assertIn("'usedBy'", errors[0])


class CheckWorkflow(unittest.TestCase):
    def test_good_workflow_passes(self):
        self.assertEqual(infra.check_workflow(GOOD_WORKFLOW), [])

    def test_job_blocks_split_on_two_space_job_keys(self):
        blocks = infra.job_blocks(GOOD_WORKFLOW)
        self.assertEqual(list(blocks), ["bicep", "what-if", "deploy"])
        self.assertIn("environment: azure-production-infra", blocks["deploy"])
        self.assertNotIn("azure/login", blocks["bicep"])

    def test_pull_request_target_as_code_fails_but_in_a_comment_does_not(self):
        bad = GOOD_WORKFLOW.replace("  pull_request:\n", "  pull_request_target:\n")
        self.assertTrue(any("pull_request_target" in e for e in infra.check_workflow(bad)))

    def test_tag_pinned_action_fails(self):
        bad = GOOD_WORKFLOW.replace(f"azure/login@{PIN}", "azure/login@v3")
        errors = infra.check_workflow(bad)
        self.assertEqual(len(errors), 1)
        self.assertIn("azure/login@v3", errors[0])

    def test_environment_on_the_preview_job_fails(self):
        bad = GOOD_WORKFLOW.replace("  what-if:\n", "  what-if:\n    environment: azure-production-infra\n")
        self.assertTrue(any("subject stops being pull_request" in e for e in infra.check_workflow(bad)))

    def test_deploy_job_without_its_environment_fails(self):
        bad = GOOD_WORKFLOW.replace("    environment: azure-production-infra\n", "")
        self.assertTrue(any("deploy job must name" in e for e in infra.check_workflow(bad)))

    def test_token_request_from_another_job_fails(self):
        bad = GOOD_WORKFLOW.replace("    runs-on: ubuntu-latest\n", "    permissions:\n      id-token: write\n", 1)
        self.assertTrue(any("job 'bicep' requests an OIDC token" in e for e in infra.check_workflow(bad)))

    def test_missing_job_and_missing_default_permissions_fail(self):
        bad = GOOD_WORKFLOW.replace("permissions:\n  contents: read\n", "").replace("  deploy:", "  ship:")
        errors = infra.check_workflow(bad)
        self.assertTrue(any("no 'deploy' job" in e for e in errors))
        self.assertTrue(any("contents: read" in e for e in errors))

    def test_the_real_workflow_and_role_set_pass(self):
        result, output = quiet(infra.cmd_check, REPO_ROOT)
        self.assertEqual(result, 0, output)


class CmdCheckOnATree(TempTree):
    def test_reports_each_error_as_a_workflow_annotation(self):
        self.write("infra/azure/main.bicep", f"var r = '{ROLE_A}'\n")
        self.write(infra.ASSIGNABLE_ROLES, json.dumps({"roles": {}}))
        self.write(infra.WORKFLOW, GOOD_WORKFLOW)
        (self.root / "infra/azure/modules").mkdir(parents=True)
        result, output = quiet(infra.cmd_check, self.root)
        self.assertEqual(result, 1)
        self.assertTrue(output.startswith("::error::"))


class Parameters(unittest.TestCase):
    DECLARATIONS = {
        "baseDomain": {"type": "string"},
        "sqlAdminPassword": {"type": "securestring"},
        "sqlEntraAdminObjectId": {"type": "string", "minLength": 36, "maxLength": 36},
        "deployGateway": {"type": "bool", "defaultValue": False},
        "tlsCertificateSecretId": {"type": "securestring", "defaultValue": ""},
        "elasticPoolCapacity": {"type": "int", "defaultValue": 100},
    }

    def test_placeholder_by_type(self):
        self.assertEqual(infra.placeholder({"type": "secureString"}), "placeholder-not-evaluated")
        self.assertEqual(infra.placeholder({"type": "secureObject"}), {})
        self.assertEqual(infra.placeholder({"type": "string", "minLength": 36}), infra.ZERO_GUID)
        self.assertEqual(infra.placeholder({"type": "string"}), "placeholder.invalid")
        self.assertIs(infra.placeholder({"type": "bool"}), False)
        self.assertEqual(infra.placeholder({"type": "int", "minValue": 2}), 2)
        self.assertEqual(infra.placeholder({"type": "array"}), [])

    def test_pr_before_any_deploy_uses_example_but_never_its_unfilled_values(self):
        example = {"baseDomain": {"value": "portal.example.com"},
                   "sqlEntraAdminObjectId": {"value": "REPLACE-WITH-THE-ENTRA-GROUP-OBJECT-ID"}}
        values = infra.pr_parameters(self.DECLARATIONS, None, example)
        self.assertEqual(values["baseDomain"], "portal.example.com")
        self.assertEqual(values["sqlEntraAdminObjectId"], infra.ZERO_GUID)
        self.assertEqual(values["sqlAdminPassword"], "placeholder-not-evaluated")
        self.assertNotIn("deployGateway", values)

    def test_pr_prefers_the_last_deploy_and_never_carries_a_secure_value(self):
        last = {"baseDomain": {"type": "String", "value": "portal.real.test"},
                "deployGateway": {"type": "Bool", "value": True},
                "sqlAdminPassword": {"type": "SecureString"}}
        values = infra.pr_parameters(self.DECLARATIONS, last, {})
        self.assertEqual(values["baseDomain"], "portal.real.test")
        self.assertIs(values["deployGateway"], True)
        self.assertEqual(values["sqlAdminPassword"], "placeholder-not-evaluated")

    def test_pr_parameter_added_since_the_last_deploy_falls_back_to_a_placeholder(self):
        values = infra.pr_parameters(self.DECLARATIONS, {"baseDomain": {"value": "x.test"}}, {})
        self.assertEqual(values["sqlEntraAdminObjectId"], infra.ZERO_GUID)
        self.assertNotIn("elasticPoolCapacity", values)

    def test_convert(self):
        self.assertIs(infra.convert("TRUE", "bool", "V"), True)
        self.assertEqual(infra.convert("200", "int", "V"), 200)
        self.assertEqual(infra.convert('["1.2.3.4/32"]', "array", "V"), ["1.2.3.4/32"])
        self.assertEqual(infra.convert("s", "securestring", "V"), "s")
        with self.assertRaisesRegex(infra.ParameterError, "V must be true or false"):
            infra.convert("yes", "bool", "V")
        with self.assertRaisesRegex(infra.ParameterError, "V is not valid array"):
            infra.convert("[not json", "array", "V")

    def test_error_messages_carry_the_name_never_the_value(self):
        with self.assertRaises(infra.ParameterError) as caught:
            infra.convert("hunter2-secret", "int", "SQL_ADMIN_PASSWORD")
        self.assertNotIn("hunter2", str(caught.exception))

    def env(self, **extra):
        base = {"BASE_DOMAIN": "portal.real.test", "SQL_ADMIN_PASSWORD": "p",
                "SQL_ENTRA_ADMIN_OBJECT_ID": "11111111-1111-1111-1111-111111111111"}
        base.update(extra)
        return base

    def test_deploy_takes_values_from_the_environment_and_skips_defaults(self):
        values, _ = quiet(infra.deploy_parameters, self.DECLARATIONS, self.env(DEPLOY_GATEWAY="false"))
        self.assertEqual(values["baseDomain"], "portal.real.test")
        self.assertIs(values["deployGateway"], False)
        self.assertNotIn("elasticPoolCapacity", values)

    def test_deploy_missing_required_value_names_the_variable(self):
        environ = self.env(BASE_DOMAIN="")
        with self.assertRaisesRegex(infra.ParameterError, "set BASE_DOMAIN"):
            infra.deploy_parameters(self.DECLARATIONS, environ)

    def test_deploy_required_parameter_without_a_mapping_says_so(self):
        declarations = {"somethingNew": {"type": "string"}}
        with self.assertRaisesRegex(infra.ParameterError, "no mapping for somethingNew"):
            infra.deploy_parameters(declarations, {})

    def test_gateway_without_a_certificate_is_refused(self):
        environ = self.env(DEPLOY_GATEWAY="true")
        with self.assertRaisesRegex(infra.ParameterError, "TLS_CERTIFICATE_SECRET_ID is empty"):
            infra.deploy_parameters(self.DECLARATIONS, environ)

    def test_variable_for_an_undeclared_parameter_is_ignored_with_a_notice(self):
        values, output = quiet(infra.deploy_parameters, self.DECLARATIONS, self.env(ALLOWED_CLIENT_CIDRS='["x"]'))
        self.assertNotIn("allowedClientCidrs", values)
        self.assertIn("::notice::ALLOWED_CLIENT_CIDRS", output)
        self.assertNotIn('["x"]', output)

    def test_parameters_file_shape(self):
        document = infra.parameters_file({"a": 1})
        self.assertEqual(document["parameters"], {"a": {"value": 1}})
        self.assertEqual(document["contentVersion"], "1.0.0.0")


class CmdParams(TempTree):
    def setUp(self):
        super().setUp()
        self.template = self.write("main.json", json.dumps({"parameters": {
            "baseDomain": {"type": "string"}, "sqlAdminPassword": {"type": "securestring"}}}))
        self.example = self.write("example.json", json.dumps({"parameters": {"baseDomain": {"value": "e.test"}}}))
        self.out = self.root / "out.json"

    def test_read_json_treats_absent_and_empty_as_no_earlier_deploy(self):
        self.assertIsNone(infra.read_json(None))
        self.assertIsNone(infra.read_json(str(self.root / "missing.json")))
        self.assertIsNone(infra.read_json(str(self.write("empty.json", "  \n"))))
        self.assertEqual(infra.read_json(str(self.example))["parameters"]["baseDomain"]["value"], "e.test")

    def test_pr_mode_writes_the_file_and_prints_no_values(self):
        last = self.write("last.json", "")
        result, output = quiet(infra.main, ["params", "--mode", "pr", "--template", str(self.template),
                                            "--last", str(last), "--example", str(self.example), "--out", str(self.out)])
        self.assertEqual(result, 0)
        self.assertNotIn("e.test", output)
        self.assertEqual(json.loads(self.out.read_text())["parameters"]["baseDomain"]["value"], "e.test")

    def test_deploy_mode_failure_is_an_annotation_and_writes_nothing(self):
        result, output = quiet(infra.main, ["params", "--mode", "deploy", "--template", str(self.template),
                                            "--out", str(self.out)])
        self.assertEqual(result, 1)
        self.assertIn("::error::required parameter", output)
        self.assertFalse(self.out.exists())


class Confined(TempTree):
    """The helper may only touch the checkout and the temp directories."""

    def test_paths_inside_an_allowed_root_resolve(self):
        inside = self.write("x.json", "{}")
        self.assertEqual(infra.confined(str(inside)), inside.resolve())
        self.assertEqual(infra.confined(str(REPO_ROOT / "scripts" / "infra-ci.py")),
                         (REPO_ROOT / "scripts" / "infra-ci.py").resolve())

    def test_runner_temp_is_an_allowed_root(self):
        self.addCleanup(infra.os.environ.pop, "RUNNER_TEMP", None)
        infra.os.environ["RUNNER_TEMP"] = str(self.root)
        self.assertIn(self.root.resolve(), infra.allowed_roots())

    def test_a_path_outside_every_root_is_refused(self):
        self.addCleanup(setattr, infra, "allowed_roots", infra.allowed_roots)
        infra.allowed_roots = lambda: [self.root.resolve()]
        outside = self.root.parent / "elsewhere.json"
        with self.assertRaisesRegex(infra.ParameterError, "outside the repository"):
            infra.confined(str(outside))

    def test_a_different_drive_is_not_inside(self):
        self.addCleanup(setattr, infra.os.path, "commonpath", infra.os.path.commonpath)

        def different_drive(paths):
            raise ValueError("Paths don't have the same drive")

        infra.os.path.commonpath = different_drive
        with self.assertRaises(infra.ParameterError):
            infra.confined(str(self.root / "x.json"))

    def test_commands_refuse_an_outside_path_with_an_annotation(self):
        self.addCleanup(setattr, infra, "allowed_roots", infra.allowed_roots)
        whatif = self.write("w.json", json.dumps({"status": "Succeeded", "changes": []}))
        infra.allowed_roots = lambda: [whatif.parent.resolve()]
        outside = str(self.root.parent / "s.md")
        result, stdout, stderr = streams(infra.main, ["summarize", "--whatif", outside, "--title", "t"])
        self.assertEqual(result, 1)
        self.assertEqual(stdout, "")
        self.assertIn("::error::", stderr)
        template = self.write("m.json", json.dumps({"parameters": {}}))
        result, output = quiet(infra.main, ["params", "--mode", "deploy", "--template", str(template), "--out", outside])
        self.assertEqual(result, 1)
        self.assertIn("outside the repository", output)


class Summarize(TempTree):
    SUB = "11111111-2222-3333-4444-555555555555"

    def rid(self, tail):
        return f"/subscriptions/{self.SUB}/resourceGroups/rg-portal-pilot{tail}"

    def test_resource_label_drops_the_subscription(self):
        self.assertEqual(infra.resource_label(self.rid("")), "(resource group)")
        self.assertEqual(infra.resource_label(self.rid("/providers/Microsoft.Network/virtualNetworks/vnet")),
                         "Microsoft.Network/virtualNetworks/vnet")
        nested = self.rid("/providers/Microsoft.KeyVault/vaults/kv/providers/Microsoft.Authorization/roleAssignments/g")
        self.assertEqual(infra.resource_label(nested),
                         "Microsoft.KeyVault/vaults/kv/providers/Microsoft.Authorization/roleAssignments/g")

    def test_success_counts_and_lists_only_real_changes(self):
        result = {"status": "Succeeded", "changes": [
            {"changeType": "NoChange", "resourceId": self.rid("/providers/A/b/quiet")},
            {"changeType": "Create", "resourceId": self.rid("/providers/A/b/new")},
            {"changeType": "Ignore", "resourceId": self.rid("/providers/A/b/other")},
            {"changeType": "Modify", "resourceId": self.rid("/providers/A/b/changed")},
            {"changeType": "Surprise", "resourceId": self.rid("/providers/A/b/odd")},
        ]}
        text, ok = infra.summarize(result, "t")
        self.assertTrue(ok)
        self.assertTrue(text.startswith(infra.SUMMARY_MARKER))
        self.assertNotIn(self.SUB, text)
        self.assertIn("- Create: `A/b/new`", text)
        self.assertNotIn("A/b/quiet", text)
        self.assertLess(text.index("| Create |"), text.index("| Modify |"))
        self.assertLess(text.index("| NoChange |"), text.index("| Surprise |"))
        self.assertIn("resources removed from the template are not deleted", text)

    def test_failure_shows_the_code_but_never_the_message(self):
        result = {"status": "Failed", "error": {"code": "AuthorizationFailed",
                                                "message": f"client x has no access to /subscriptions/{self.SUB}"}}
        text, ok = infra.summarize(result, "t")
        self.assertFalse(ok)
        self.assertIn("`AuthorizationFailed`", text)
        self.assertNotIn(self.SUB, text)

    def test_summary_goes_to_stdout_and_only_the_error_code_to_stderr(self):
        # The job log is public too, so stderr carries the same digest as the comment: never the message.
        whatif = self.write("w.json", json.dumps({"status": "Failed", "error": {"code": "X", "message": "detail"}}))
        result, stdout, stderr = streams(infra.main, ["summarize", "--whatif", str(whatif), "--title", "t"])
        self.assertEqual(result, 1)
        self.assertTrue(stdout.startswith(infra.SUMMARY_MARKER))
        self.assertNotIn("detail", stdout)
        self.assertNotIn("detail", stderr)
        self.assertIn("what-if error: X", stderr)

    def test_successful_summary_exits_zero_and_writes_nothing_to_stderr(self):
        whatif = self.write("w.json", json.dumps({"status": "Succeeded", "changes": []}))
        result, stdout, stderr = streams(infra.main, ["summarize", "--whatif", str(whatif), "--title", "t"])
        self.assertEqual(result, 0)
        self.assertIn("| change | count |", stdout)
        self.assertEqual(stderr, "")


# An ARM error shaped like the ones validation returns: the message quotes the rejected parameter value.
QUOTING_ERROR = {
    "code": "InvalidTemplateDeployment",
    "message": "The value 'portal.real.example' of parameter baseDomain is not valid.",
    "details": [
        {"code": "InvalidParameter", "target": "baseDomain", "message": "rejected portal.real.example"},
        {"code": "Conflict", "message": "group 11111111-2222-3333-4444-555555555555",
         "details": [{"code": "DeepInner", "target": "sqlEntraAdminObjectId", "message": "id 1111"}]},
    ],
}


class ErrorDigest(TempTree):
    def test_walks_details_depth_first_and_never_reads_the_message(self):
        digest = infra.error_digest(QUOTING_ERROR)
        self.assertEqual(digest, ["InvalidTemplateDeployment", "InvalidParameter (target baseDomain)", "Conflict",
                                  "DeepInner (target sqlEntraAdminObjectId)"])
        self.assertNotIn("portal.real.example", " ".join(digest))
        self.assertNotIn("1111", " ".join(digest))

    def test_accepts_the_wrapped_form_and_ignores_anything_that_is_not_an_error(self):
        self.assertEqual(infra.error_digest({"error": {"code": "A"}}), ["A"])
        self.assertEqual(infra.error_digest(None), [])
        self.assertEqual(infra.error_digest("text"), [])
        self.assertEqual(infra.error_digest({"message": "only a message"}), [])

    def test_stderr_with_a_json_error_body(self):
        text = "ERROR: " + json.dumps({"error": QUOTING_ERROR})
        digest = infra.stderr_digest(text)
        self.assertEqual(digest[0], "InvalidTemplateDeployment")
        self.assertNotIn("portal.real.example", " ".join(digest))

    def test_stderr_in_the_cli_text_form(self):
        text = ("ERROR: (InvalidTemplateDeployment) The value 'portal.real.example' is not valid.\n"
                "Code: InvalidTemplateDeployment\nMessage: The value 'portal.real.example' is not valid.\n"
                "Target: baseDomain\n")
        self.assertEqual(infra.stderr_digest(text), ["InvalidTemplateDeployment", "baseDomain"])

    def test_stderr_with_only_the_error_header(self):
        self.assertEqual(infra.stderr_digest("ERROR: (AuthorizationFailed) The client 'x' has no access."),
                         ["AuthorizationFailed"])

    def test_unrecognised_stderr_is_reported_without_repeating_it(self):
        digest = infra.stderr_digest("something broke: portal.real.example {not json")
        self.assertEqual(len(digest), 1)
        self.assertNotIn("portal.real.example", digest[0])

    def test_digest_command_prints_only_codes_for_the_log(self):
        err = self.write("w.err", "ERROR: " + json.dumps({"error": QUOTING_ERROR}))
        result, stdout, stderr = streams(infra.main, ["digest", "--stderr", str(err)])
        self.assertEqual(result, 0)
        self.assertIn("az error: InvalidTemplateDeployment", stdout)
        self.assertNotIn("portal.real.example", stdout + stderr)

    def test_digest_command_prints_a_what_if_result_holding_only_codes(self):
        err = self.write("w.err", "ERROR: " + json.dumps({"error": QUOTING_ERROR}))
        result, stdout, stderr = streams(infra.main, ["digest", "--stderr", str(err), "--as-whatif-json"])
        self.assertEqual(result, 0)
        self.assertIn("az error: InvalidTemplateDeployment", stderr)
        self.assertNotIn("portal.real.example", stdout + stderr)
        written = json.loads(stdout)
        self.assertEqual(written["status"], "Failed")
        text, ok = infra.summarize(written, "t")
        self.assertFalse(ok)
        self.assertIn("`InvalidParameter (target baseDomain)`", text)

    def test_digest_command_refuses_a_path_outside_the_allowed_roots(self):
        self.addCleanup(setattr, infra, "allowed_roots", infra.allowed_roots)
        infra.allowed_roots = lambda: [self.root.resolve()]
        result, stdout, stderr = streams(infra.main, ["digest", "--stderr", str(self.root.parent / "x.err")])
        self.assertEqual(result, 1)
        self.assertEqual(stdout, "")
        self.assertIn("::error::", stderr)


class AzStderrGuard(unittest.TestCase):
    def test_flags_an_az_call_whose_stderr_is_not_redirected(self):
        errors = infra.unredirected_az('az deployment group what-if -g "$RG" \\\n  --output json > out.json\n')
        self.assertEqual(len(errors), 1)
        self.assertIn("az stderr reaches the public log", errors[0])

    def test_passes_when_stderr_goes_to_a_file_even_across_continuation_lines(self):
        code = 'az deployment group create -g "$RG" \\\n  --output none \\\n  2> "$RUNNER_TEMP/deploy.err"\n'
        self.assertEqual(infra.unredirected_az(code), [])
        self.assertEqual(infra.unredirected_az('az tag update --tags a=1 > /dev/null 2> p.err\n'), [])

    def test_flags_printing_an_error_file_or_a_captured_error(self):
        self.assertEqual(len(infra.unredirected_az('cat "$RUNNER_TEMP/last.err"\n')), 1)
        self.assertEqual(len(infra.unredirected_az('echo "$out"\n')), 1)
        self.assertEqual(infra.unredirected_az('grep -q X "$RUNNER_TEMP/last.err"\n'), [])


class FederatedCredentialPolicyGuard(TempTree):
    GOOD_BOOTSTRAP = """resource p 'Microsoft.Authorization/policyDefinitions@2023-04-01' = {
  properties: { policyRule: { if: { field: 'type', equals: '%s' }, then: { effect: 'deny' } } }
}
module workloadPolicy 'modules/workload-policy.bicep' = {
  name: 'x'
  scope: workloadGroup
}
""" % infra.FIC_TYPE
    MODULE = "resource a 'Microsoft.Authorization/policyAssignments@2024-04-01' = {}\n"

    def tree(self, bootstrap, module=MODULE):
        self.write("infra/azure/subscription/bootstrap.bicep", bootstrap)
        if module is not None:
            self.write("infra/azure/subscription/modules/workload-policy.bicep", module)

    def test_a_workload_scoped_deny_passes(self):
        self.tree(self.GOOD_BOOTSTRAP)
        self.assertEqual(infra.check_fic_policy(self.root), [])

    def test_a_missing_bootstrap_is_reported(self):
        self.assertEqual(len(infra.check_fic_policy(self.root)), 1)

    def test_an_audit_effect_is_not_enough(self):
        self.tree(self.GOOD_BOOTSTRAP.replace("effect: 'deny'", "effect: 'audit'"))
        self.assertTrue(any("deny policy" in e for e in infra.check_fic_policy(self.root)))

    def test_an_assignment_at_any_other_scope_fails(self):
        self.tree(self.GOOD_BOOTSTRAP.replace("scope: workloadGroup", "scope: identityGroup"))
        self.assertTrue(any("WORKLOAD group" in e for e in infra.check_fic_policy(self.root)))

    def test_a_missing_assignment_module_fails(self):
        self.tree(self.GOOD_BOOTSTRAP, module=None)
        self.assertTrue(any("workload-policy.bicep" in e for e in infra.check_fic_policy(self.root)))


if __name__ == "__main__":
    unittest.main()
