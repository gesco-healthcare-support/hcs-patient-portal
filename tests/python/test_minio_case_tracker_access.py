"""scripts/hosting/minio-case-tracker-access.sh gives the Case Tracker read access to ONE office's documents.

What these tests pin:

  - the policy grants s3:GetObject on <bucket>/tenants/<office id>/* and nothing else;
  - the secret goes only into the credential file, never to the terminal, and no MinIO credential appears on a
    docker command line (where any local user could read it from the process list);
  - creating refuses when the office already has a credential, here or in MinIO, so a second run cannot replace a
    secret the Case Tracker is using; rotating refuses when there is nothing to rotate;
  - once MinIO has ACCEPTED a new secret, the file is kept even if a later step fails, so a live secret is never
    thrown away;
  - the SHARED credential (--shared, 2026-10-14) grants s3:GetObject on each listed office's prefix and nothing else,
    and --update replaces the office list while keeping the secret, so the Case Tracker keeps its one key.
    Proven against a real MinIO by hand on 2026-10-14 (15 checks): listed offices readable; an unlisted office,
    writes, deletes, listings and admin refused; an added office readable with the same key; a removed one refused.

The real MinIO images cannot be pulled in CI (see docker-compose.prod.yml), so `docker` here is a fake that runs
the piped script locally, against a fake `mc` that keeps its users and policies in a directory. The script was
also proven against a real MinIO by hand when it was written: own office readable; another office, writes,
deletes, listings, the Case Tracker bucket and admin actions all refused; rotation invalidating the old secret.

They need a POSIX bash. CI runs on Linux. On Windows set TEST_BASH to Git Bash's plain shell,
C:/Program Files/Git/usr/bin/bash.exe, or these tests are skipped.
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
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "minio-case-tracker-access.sh"
OFFICE = "0f8fad5b-d9cb-469f-a165-70867728950e"
OTHER_OFFICE = "7c9e6679-7425-40de-944b-e07fc1f90ae7"
SEP = "\x1f"

# Logs its arguments, then runs the piped script with the fake mc first on PATH.
FAKE_DOCKER = r"""#!/usr/bin/env bash
{ printf '%s\x1f' "$@"; printf '\n'; } >> "$FAKE_DIR/docker.log"
script="$(cat)"
printf '%s\n---\n' "$script" >> "$FAKE_DIR/stdin.log"
PATH="$FAKE_DIR/bin:$PATH" bash -s <<< "$script"
"""

# A tiny stateful stand-in for `mc admin`. users/<key> holds the secret; attached/<key> the policy name.
FAKE_MC = r"""#!/usr/bin/env bash
state="$FAKE_DIR/state"; mkdir -p "$state/users" "$state/attached" "$state/policies"
case "$1 $2 $3" in
  "alias set local") exit 0 ;;
  "admin policy create") cp "$6" "$state/policies/$5.json"; exit 0 ;;
  "admin policy info") [ -f "$state/policies/$5.json" ] && cat "$state/policies/$5.json"; exit $? ;;
  "admin user add") printf '%s' "$6" > "$state/users/$5"; exit 0 ;;
  "admin user info")
    [ -f "$state/users/$5" ] || { echo "mc: user $5 does not exist" >&2; exit 1; }
    echo "AccessKey: $5"; echo "Status: enabled"
    [ -f "$state/attached/$5" ] && echo "PolicyName: $(cat "$state/attached/$5")"
    exit 0 ;;
  "admin policy attach")
    [ -n "${FAKE_FAIL_ATTACH:-}" ] && { echo "mc: attach failed" >&2; exit 1; }
    printf '%s' "$5" > "$state/attached/$7"; exit 0 ;;
esac
echo "fake mc: unexpected: $*" >&2
exit 2
"""


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class CaseTrackerAccess(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="ct-minio-"))
        (self.dir / "bin").mkdir()
        for name, body in (("docker", FAKE_DOCKER), ("mc", FAKE_MC)):
            path = self.dir / "bin" / name
            path.write_text(body, encoding="utf-8", newline="\n")
            path.chmod(path.stat().st_mode | stat.S_IEXEC)
        self.root_user = "root" + secrets.token_hex(4)
        self.root_password = secrets.token_hex(16)
        (self.dir / "env.prod").write_text(
            "BASE_DOMAIN=portal.example.test\n"
            f"MINIO_ROOT_USER={self.root_user}\n"
            f"MINIO_ROOT_PASSWORD={self.root_password}\n"
            "MINIO_BUCKET_NAME=case-evaluation-documents\n", encoding="utf-8", newline="\n")
        self.out = self.dir / "out"

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def run_script(self, *args, **extra_env):
        env = dict(os.environ,
                   PATH=shell_path(self.dir / "bin") + os.pathsep + os.environ.get("PATH", ""),
                   FAKE_DIR=shell_path(self.dir),
                   ENV_FILE=shell_path(self.dir / "env.prod"),
                   OUT_DIR=shell_path(self.out),
                   **extra_env)
        if sys.platform == "win32":
            env["PATH"] = shell_path(self.dir / "bin") + ":/usr/bin:/bin"
        result = subprocess.run([BASH, str(SCRIPT), *args], env=env, capture_output=True,
                                encoding="utf-8", errors="replace", timeout=120)
        return result.returncode, result.stdout + result.stderr

    def credential(self, office=OFFICE):
        path = self.out / f"case-tracker-minio-{office}.txt"
        if not path.exists():
            return None
        return dict(line.split("=", 1) for line in path.read_text(encoding="utf-8").splitlines()
                    if line and not line.startswith("#"))

    def docker_calls(self):
        log = self.dir / "docker.log"
        return log.read_text(encoding="utf-8").splitlines() if log.exists() else []

    def minio_secret(self, key):
        path = self.dir / "state" / "users" / key
        return path.read_text(encoding="utf-8") if path.exists() else None

    def test_create_grants_get_object_on_the_office_prefix_and_nothing_else(self):
        code, output = self.run_script(OFFICE)
        self.assertEqual(code, 0, output)
        policy = json.loads((self.dir / "state" / "policies" / f"ct-read-{OFFICE}.json").read_text(encoding="utf-8"))
        self.assertEqual(policy["Statement"], [{
            "Effect": "Allow",
            "Action": ["s3:GetObject"],
            "Resource": [f"arn:aws:s3:::case-evaluation-documents/tenants/{OFFICE}/*"],
        }])
        self.assertEqual((self.dir / "state" / "attached" / "ct-0f8fad5bd9cb469f").read_text(encoding="utf-8"),
                         f"ct-read-{OFFICE}")

    def test_the_secret_goes_only_to_the_file(self):
        code, output = self.run_script(OFFICE)
        self.assertEqual(code, 0, output)
        cred = self.credential()
        self.assertEqual(cred["access_key"], "ct-0f8fad5bd9cb469f")
        self.assertRegex(cred["secret_key"], r"^[0-9a-f]{40}$")
        self.assertEqual(cred["secret_key"], self.minio_secret(cred["access_key"]))
        self.assertEqual(cred["endpoint"], "https://minio.portal.example.test")
        self.assertEqual(cred["prefix"], f"tenants/{OFFICE}/")
        self.assertNotIn(cred["secret_key"], output)
        self.assertNotIn(self.root_password, output)

    def test_no_credential_is_ever_on_a_docker_command_line(self):
        code, output = self.run_script(OFFICE)
        self.assertEqual(code, 0, output)
        secret = self.credential()["secret_key"]
        calls = self.docker_calls()
        self.assertTrue(calls)
        for call in calls:
            self.assertNotIn(self.root_password, call)
            self.assertNotIn(self.root_user, call)
            self.assertNotIn(secret, call)
        # It travelled on stdin instead.
        self.assertIn(self.root_password, (self.dir / "stdin.log").read_text(encoding="utf-8"))

    def test_an_uppercase_office_id_is_accepted_and_lowercased(self):
        code, output = self.run_script(OFFICE.upper())
        self.assertEqual(code, 0, output)
        self.assertIsNotNone(self.credential())

    def test_an_argument_that_is_not_an_office_id_is_refused_before_minio_is_touched(self):
        for bad in ("falkinstein", f"{OFFICE}x", "../../etc", ""):
            with self.subTest(argument=bad):
                code, output = self.run_script(bad)
                self.assertNotEqual(code, 0)
                self.assertIn("must be the office id", output)
        self.assertEqual(self.docker_calls(), [])

    def test_a_second_create_is_refused_and_changes_nothing(self):
        self.assertEqual(self.run_script(OFFICE)[0], 0)
        before = self.credential()
        calls = len(self.docker_calls())
        code, output = self.run_script(OFFICE)
        self.assertNotEqual(code, 0)
        self.assertIn("already exists", output)
        self.assertEqual(self.credential(), before)
        self.assertEqual(len(self.docker_calls()), calls, "a refused create must not reach MinIO")

    def test_create_is_refused_when_minio_already_has_the_user(self):
        self.assertEqual(self.run_script(OFFICE)[0], 0)
        live = self.minio_secret("ct-0f8fad5bd9cb469f")
        (self.out / f"case-tracker-minio-{OFFICE}.txt").unlink()
        code, output = self.run_script(OFFICE)
        self.assertNotEqual(code, 0)
        self.assertIn("already has user", output)
        self.assertIsNone(self.credential())
        self.assertFalse((self.out / f"case-tracker-minio-{OFFICE}.txt.tmp").exists())
        self.assertEqual(self.minio_secret("ct-0f8fad5bd9cb469f"), live, "the live secret must be untouched")

    def test_rotate_replaces_the_secret(self):
        self.assertEqual(self.run_script(OFFICE)[0], 0)
        old = self.credential()["secret_key"]
        code, output = self.run_script(OFFICE, "--rotate")
        self.assertEqual(code, 0, output)
        new = self.credential()["secret_key"]
        self.assertNotEqual(new, old)
        self.assertEqual(self.minio_secret("ct-0f8fad5bd9cb469f"), new)
        self.assertNotIn(new, output)

    def test_rotate_is_refused_when_there_is_nothing_to_rotate(self):
        code, output = self.run_script(OTHER_OFFICE, "--rotate")
        self.assertNotEqual(code, 0)
        self.assertIn("does not exist", output)
        self.assertEqual(self.docker_calls(), [])

    def test_a_secret_minio_accepted_is_kept_even_when_a_later_step_fails(self):
        code, output = self.run_script(OFFICE, FAKE_FAIL_ATTACH="1")
        self.assertNotEqual(code, 0)
        self.assertIn("ACCEPTED the new secret", output)
        cred = self.credential()
        self.assertIsNotNone(cred, "the only copy of a live secret was thrown away")
        self.assertEqual(cred["secret_key"], self.minio_secret(cred["access_key"]))

    def test_show_prints_the_policy_and_never_a_secret(self):
        self.assertEqual(self.run_script(OFFICE)[0], 0)
        secret = self.credential()["secret_key"]
        code, output = self.run_script(OFFICE, "--show")
        self.assertEqual(code, 0, output)
        self.assertIn("s3:GetObject", output)
        self.assertIn(f"tenants/{OFFICE}/*", output)
        self.assertNotIn(secret, output)

    def test_unknown_options_are_refused(self):
        code, output = self.run_script(OFFICE, "--force")
        self.assertNotEqual(code, 0)
        self.assertIn("unknown option", output)
        code, output = self.run_script(OFFICE, "--update")
        self.assertNotEqual(code, 0, "--update belongs to the shared credential only")
        self.assertIn("unknown option", output)

    # ---------------------------------------------------------------- the shared credential (2026-10-14)

    def shared_policy(self):
        path = self.dir / "state" / "policies" / "ct-read-shared.json"
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else None

    def shared_credential(self):
        return self.credential("shared")

    def test_the_shared_credential_grants_get_object_on_each_listed_office_and_nothing_else(self):
        # Unsorted, duplicated and upper case on purpose: the same SET must give the same policy.
        code, output = self.run_script("--shared", OTHER_OFFICE, OFFICE.upper(), OFFICE)
        self.assertEqual(code, 0, output)
        self.assertEqual(self.shared_policy()["Statement"], [{
            "Effect": "Allow",
            "Action": ["s3:GetObject"],
            "Resource": [f"arn:aws:s3:::case-evaluation-documents/tenants/{office}/*"
                         for office in sorted([OFFICE, OTHER_OFFICE])],
        }])
        self.assertEqual((self.dir / "state" / "attached" / "ct-shared").read_text(encoding="utf-8"), "ct-read-shared")
        cred = self.shared_credential()
        self.assertEqual(cred["access_key"], "ct-shared")
        self.assertEqual(cred["prefixes"], ",".join(f"tenants/{o}/" for o in sorted([OFFICE, OTHER_OFFICE])))
        self.assertEqual(cred["secret_key"], self.minio_secret("ct-shared"))
        self.assertNotIn(cred["secret_key"], output)

    def test_update_changes_the_office_list_and_keeps_the_secret(self):
        self.assertEqual(self.run_script("--shared", OFFICE)[0], 0)
        secret = self.shared_credential()["secret_key"]
        third = "3f2504e0-4f89-41d3-9a0c-0305e82c3301"
        code, output = self.run_script("--shared", OFFICE, OTHER_OFFICE, third, "--update")
        self.assertEqual(code, 0, output)
        resources = self.shared_policy()["Statement"][0]["Resource"]
        self.assertEqual(len(resources), 3)
        self.assertIn(f"arn:aws:s3:::case-evaluation-documents/tenants/{third}/*", resources)
        self.assertEqual(self.minio_secret("ct-shared"), secret, "an office-list change must not change the key")
        cred = self.shared_credential()
        self.assertEqual(cred["secret_key"], secret)
        self.assertIn(f"tenants/{third}/", cred["prefixes"])
        self.assertNotIn(secret, output)
        self.assertIn("UNCHANGED", output)

    def test_update_is_refused_without_a_shared_credential_and_touches_nothing(self):
        code, output = self.run_script("--shared", OFFICE, "--update")
        self.assertNotEqual(code, 0)
        self.assertIn("does not exist", output)
        self.assertEqual(self.docker_calls(), [])

    def test_update_is_refused_when_minio_has_no_shared_user_and_the_file_is_unchanged(self):
        self.assertEqual(self.run_script("--shared", OFFICE)[0], 0)
        before = self.shared_credential()
        (self.dir / "state" / "users" / "ct-shared").unlink()
        code, output = self.run_script("--shared", OFFICE, OTHER_OFFICE, "--update")
        self.assertNotEqual(code, 0)
        self.assertIn("refused the policy update", output)
        self.assertEqual(self.shared_credential(), before)
        self.assertFalse((self.out / "case-tracker-minio-shared.txt.tmp").exists())

    def test_a_second_shared_create_is_refused_and_points_at_update(self):
        self.assertEqual(self.run_script("--shared", OFFICE)[0], 0)
        code, output = self.run_script("--shared", OFFICE, OTHER_OFFICE)
        self.assertNotEqual(code, 0)
        self.assertIn("already exists", output)
        self.assertIn("--update", output)

    def test_shared_rotate_replaces_the_secret(self):
        self.assertEqual(self.run_script("--shared", OFFICE)[0], 0)
        old = self.shared_credential()["secret_key"]
        code, output = self.run_script("--shared", OFFICE, "--rotate")
        self.assertEqual(code, 0, output)
        self.assertNotEqual(self.shared_credential()["secret_key"], old)
        self.assertEqual(self.minio_secret("ct-shared"), self.shared_credential()["secret_key"])

    def test_shared_arguments_are_checked_before_minio_is_touched(self):
        for args, message in ((("--shared",), "at least one office id"),
                              (("--shared", "falkinstein"), "must be the office id"),
                              (("--shared", OFFICE, "--force"), "unknown option"),
                              (("--shared", OFFICE, "--update", "--rotate"), "at most one")):
            with self.subTest(args=args):
                code, output = self.run_script(*args)
                self.assertNotEqual(code, 0)
                self.assertIn(message, output)
        self.assertEqual(self.docker_calls(), [])

    def test_shared_show_prints_the_policy_and_never_a_secret(self):
        self.assertEqual(self.run_script("--shared", OFFICE, OTHER_OFFICE)[0], 0)
        secret = self.shared_credential()["secret_key"]
        code, output = self.run_script("--shared", "--show")
        self.assertEqual(code, 0, output)
        self.assertIn(f"tenants/{OTHER_OFFICE}/*", output)
        self.assertNotIn(secret, output)


class TheScriptIsShippedExecutable(unittest.TestCase):
    def test_git_tracks_it_executable(self):
        # A non-executable hosting script silently broke the backups for ten days in 2026-08 (status=203/EXEC).
        mode = subprocess.run(["git", "ls-files", "-s", str(SCRIPT.relative_to(REPO_ROOT))], cwd=REPO_ROOT,
                              capture_output=True, encoding="utf-8").stdout.split(" ")[0]
        self.assertEqual(mode, "100755")


if __name__ == "__main__":
    unittest.main()
