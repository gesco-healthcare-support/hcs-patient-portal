"""scripts/hosting/office-signature.sh stores ONE office's doctor signature image for the PQME Form 110 page.

What these tests pin:

  - the object key is exactly the one OfficeSignatureReader reads: host/office-signatures/<office id, no hyphens>.png
    in the documents bucket (never the office logo's host/<id>.png, never under tenants/<id>/);
  - a file that is not a PNG (by its first eight bytes, whatever its name), is empty, or is over 200 KB is refused
    BEFORE MinIO is touched, as is anything that is not an office id;
  - what the script prints is only the key, the stored size and the sha256: never the image bytes or base64;
  - the application credential is used (not root), and no credential is ever on a docker command line;
  - --remove deletes exactly that object, and the script refuses to run without the env file.

Every image here is SYNTHETIC bytes (a PNG signature plus filler), never a real signature. `docker` is a fake that
runs the piped script locally against a fake `mc` that keeps objects in a directory; it honours the one -v mount.

They need a POSIX bash. CI runs on Linux. On Windows set TEST_BASH to Git Bash's plain shell,
C:/Program Files/Git/usr/bin/bash.exe, or these tests are skipped.
"""

import base64
import hashlib
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
SCRIPT = REPO_ROOT / "scripts" / "hosting" / "office-signature.sh"
OFFICE = "0f8fad5b-d9cb-469f-a165-70867728950e"
OFFICE_KEY = "host/office-signatures/0f8fad5bd9cb469fa16570867728950e.png"
PNG_MAGIC = bytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])

# Logs its arguments, remembers the one -v mount, then runs the piped script with the fake mc first on PATH.
FAKE_DOCKER = r"""#!/usr/bin/env bash
{ printf '%s\x1f' "$@"; printf '\n'; } >> "$FAKE_DIR/docker.log"
while [ $# -gt 0 ]; do
  if [ "$1" = "-v" ]; then
    spec="$2"
    export FAKE_MOUNT_HOST="${spec%%:*}"
    rest="${spec#*:}"
    export FAKE_MOUNT_CONT="${rest%%:*}"
  fi
  shift
done
script="$(cat)"
printf '%s\n---\n' "$script" >> "$FAKE_DIR/stdin.log"
PATH="$FAKE_DIR/bin:$PATH" bash -s <<< "$script"
"""

# A tiny stateful stand-in for `mc`: objects live in state/objects/<alias path>.
FAKE_MC = r"""#!/usr/bin/env bash
state="$FAKE_DIR/state"; mkdir -p "$state/objects"
case "$1" in
  alias) exit 0 ;;
  cp)
    [ "$2" = "$FAKE_MOUNT_CONT" ] || { echo "fake mc: the source is not the mounted file" >&2; exit 2; }
    [ -n "${FAKE_FAIL_CP:-}" ] && { echo "mc: upload failed" >&2; exit 1; }
    mkdir -p "$(dirname "$state/objects/$3")"; cp "$FAKE_MOUNT_HOST" "$state/objects/$3"; exit 0 ;;
  stat)
    [ -f "$state/objects/$3" ] || { echo "mc: object does not exist" >&2; exit 1; }
    echo "{\"status\":\"success\",\"name\":\"x\",\"size\":$(wc -c < "$state/objects/$3" | tr -d ' '),\"type\":\"file\"}"
    exit 0 ;;
  rm) [ -f "$state/objects/$2" ] || { echo "mc: object does not exist" >&2; exit 1; }
      rm -f "$state/objects/$2"; exit 0 ;;
esac
echo "fake mc: unexpected: $*" >&2
exit 2
"""


@unittest.skipUnless(BASH, "needs a POSIX bash (set TEST_BASH on Windows)")
class OfficeSignature(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="office-sig-"))
        (self.dir / "bin").mkdir()
        for name, body in (("docker", FAKE_DOCKER), ("mc", FAKE_MC)):
            path = self.dir / "bin" / name
            path.write_text(body, encoding="utf-8", newline="\n")
            path.chmod(path.stat().st_mode | stat.S_IEXEC)
        self.app_key = "app" + secrets.token_hex(4)
        self.app_secret = secrets.token_hex(16)
        self.root_password = secrets.token_hex(16)
        (self.dir / "env.prod").write_text(
            f"MINIO_ROOT_USER=root\nMINIO_ROOT_PASSWORD={self.root_password}\n"
            f"MINIO_APP_ACCESS_KEY={self.app_key}\nMINIO_APP_SECRET_KEY={self.app_secret}\n"
            "MINIO_BUCKET_NAME=case-evaluation-documents\n", encoding="utf-8", newline="\n")
        self.png = self.dir / "synthetic.png"
        self.png_bytes = PNG_MAGIC + bytes(range(1, 200))
        self.png.write_bytes(self.png_bytes)

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def run_script(self, *args, env_file=None, **extra_env):
        env = dict(os.environ,
                   PATH=shell_path(self.dir / "bin") + os.pathsep + os.environ.get("PATH", ""),
                   FAKE_DIR=shell_path(self.dir),
                   ENV_FILE=env_file or shell_path(self.dir / "env.prod"),
                   **extra_env)
        if sys.platform == "win32":
            env["PATH"] = shell_path(self.dir / "bin") + ":/usr/bin:/bin"
        result = subprocess.run([BASH, str(SCRIPT), *args], env=env, capture_output=True,
                                encoding="utf-8", errors="replace", timeout=120)
        return result.returncode, result.stdout + result.stderr

    def stored(self, key=OFFICE_KEY):
        path = self.dir / "state" / "objects" / "local" / "case-evaluation-documents" / key
        return path.read_bytes() if path.exists() else None

    def docker_calls(self):
        log = self.dir / "docker.log"
        return log.read_text(encoding="utf-8").splitlines() if log.exists() else []

    def test_the_image_is_stored_at_exactly_the_key_the_application_reads(self):
        code, output = self.run_script(OFFICE, shell_path(self.png))
        self.assertEqual(code, 0, output)
        self.assertEqual(self.stored(), self.png_bytes)
        # Nowhere else: not the office logo's key, not under the Case Tracker's tenants/ prefix.
        objects = self.dir / "state" / "objects"
        names = sorted(p.relative_to(objects).as_posix() for p in objects.rglob("*") if p.is_file())
        self.assertEqual(names, ["local/case-evaluation-documents/" + OFFICE_KEY])

    def test_it_prints_only_the_key_the_size_and_the_sha256(self):
        code, output = self.run_script(OFFICE, shell_path(self.png))
        self.assertEqual(code, 0, output)
        self.assertIn(OFFICE_KEY, output)
        self.assertIn("size=%d" % len(self.png_bytes), output)
        self.assertIn(hashlib.sha256(self.png_bytes).hexdigest(), output)
        self.assertNotIn(base64.b64encode(self.png_bytes).decode(), output)
        self.assertNotIn(base64.b64encode(self.png_bytes[:40]).decode(), output)
        self.assertNotIn("iVBOR", output)
        self.assertNotIn("data:image", output)

    def test_an_uppercase_office_id_is_accepted_and_lowercased(self):
        code, output = self.run_script(OFFICE.upper(), shell_path(self.png))
        self.assertEqual(code, 0, output)
        self.assertEqual(self.stored(), self.png_bytes)

    def test_a_file_that_is_not_a_png_is_refused_whatever_its_name(self):
        fake = self.dir / "looks-like.png"
        fake.write_bytes(b"GIF89a" + b"\x00" * 50)
        code, output = self.run_script(OFFICE, shell_path(fake))
        self.assertNotEqual(code, 0)
        self.assertIn("not a PNG", output)
        self.assertEqual(self.docker_calls(), [], "a refused file must not reach MinIO")

    def test_a_png_signature_cut_short_is_refused(self):
        short = self.dir / "short.png"
        short.write_bytes(PNG_MAGIC[:5])
        code, output = self.run_script(OFFICE, shell_path(short))
        self.assertNotEqual(code, 0)
        self.assertEqual(self.docker_calls(), [])

    def test_an_empty_file_is_refused(self):
        empty = self.dir / "empty.png"
        empty.write_bytes(b"")
        code, output = self.run_script(OFFICE, shell_path(empty))
        self.assertNotEqual(code, 0)
        self.assertIn("empty", output)
        self.assertEqual(self.docker_calls(), [])

    def test_a_file_over_200_kb_is_refused_and_one_at_the_limit_is_accepted(self):
        big = self.dir / "big.png"
        big.write_bytes(PNG_MAGIC + b"\x00" * (204800 - 8 + 1))
        code, output = self.run_script(OFFICE, shell_path(big))
        self.assertNotEqual(code, 0)
        self.assertIn("204800", output)
        self.assertEqual(self.docker_calls(), [])
        exact = self.dir / "exact.png"
        exact.write_bytes(PNG_MAGIC + b"\x00" * (204800 - 8))
        code, output = self.run_script(OFFICE, shell_path(exact))
        self.assertEqual(code, 0, output)

    def test_an_argument_that_is_not_an_office_id_is_refused_before_minio_is_touched(self):
        for bad in ("falkinstein", f"{OFFICE}x", "../../etc", ""):
            with self.subTest(argument=bad):
                code, output = self.run_script(bad, shell_path(self.png))
                self.assertNotEqual(code, 0)
                self.assertIn("must be a GUID", output)
        self.assertEqual(self.docker_calls(), [])

    def test_a_missing_file_and_a_wrong_argument_count_are_refused(self):
        self.assertNotEqual(self.run_script(OFFICE, shell_path(self.dir / "nope.png"))[0], 0)
        self.assertNotEqual(self.run_script(OFFICE)[0], 0)
        self.assertNotEqual(self.run_script()[0], 0)
        self.assertEqual(self.docker_calls(), [])

    def test_it_refuses_to_run_without_the_env_file(self):
        code, output = self.run_script(OFFICE, shell_path(self.png), env_file=shell_path(self.dir / "missing.env"))
        self.assertNotEqual(code, 0)
        self.assertIn("env file not found", output)
        self.assertEqual(self.docker_calls(), [])

    def test_the_application_credential_is_used_and_never_on_a_command_line(self):
        code, output = self.run_script(OFFICE, shell_path(self.png))
        self.assertEqual(code, 0, output)
        calls = self.docker_calls()
        self.assertTrue(calls)
        for call in calls:
            self.assertNotIn(self.app_key, call)
            self.assertNotIn(self.app_secret, call)
            self.assertNotIn(self.root_password, call)
        stdin = (self.dir / "stdin.log").read_text(encoding="utf-8")
        self.assertIn(self.app_secret, stdin)
        self.assertNotIn(self.root_password, stdin, "the root login must not be used to write a signature")
        self.assertNotIn(self.app_secret, output)

    def test_a_failed_upload_is_reported_and_confirms_nothing(self):
        code, output = self.run_script(OFFICE, shell_path(self.png), FAKE_FAIL_CP="1")
        self.assertNotEqual(code, 0)
        self.assertNotIn("stored:", output)
        self.assertIsNone(self.stored())

    def test_remove_deletes_exactly_that_object(self):
        self.assertEqual(self.run_script(OFFICE, shell_path(self.png))[0], 0)
        other = "7c9e6679-7425-40de-944b-e07fc1f90ae7"
        self.assertEqual(self.run_script(other, shell_path(self.png))[0], 0)
        code, output = self.run_script("--remove", OFFICE)
        self.assertEqual(code, 0, output)
        self.assertIsNone(self.stored())
        self.assertEqual(self.stored("host/office-signatures/7c9e6679742540de944be07fc1f90ae7.png"), self.png_bytes)

    def test_remove_refuses_a_bad_office_id(self):
        code, output = self.run_script("--remove", "not-a-guid")
        self.assertNotEqual(code, 0)
        self.assertEqual(self.docker_calls(), [])


if __name__ == "__main__":
    unittest.main()
