"""global.json and the .NET image SDK pins name the same SDK (B8).

global.json pins the SDK that CI installs (setup-dotnet reads it) and that every image build uses
(each SDK stage copies it in). The images also name an SDK in their FROM line. The two must agree,
because `rollForward: latestPatch` makes the SDK inside an image refuse a global.json that asks for a
newer patch, and accept one that asks for an older patch only within the same feature band.

They do not reference each other, and Dependabot moves them in SEPARATE pull requests: the
`dotnet-sdk` entry bumps global.json and the `docker` entry bumps the FROM lines. No workflow builds
the images, so a half-landed bump would pass CI and break at the next manual deploy. This test turns
that into a red check on the pull request that does it. When Dependabot opens the two halves, they
are merged together.

Same shape as TenantNamingTests.ProxyReservedSlugs_matches_the_exact_single_label_hosts_in_the_nginx_template:
two files that must agree and never reference each other need a test, because nothing else connects
them.
"""

import json
import pathlib
import re
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
GLOBAL_JSON = REPO_ROOT / "global.json"
SDK_IMAGE = "mcr.microsoft.com/dotnet/sdk"
RUNTIME_IMAGE = "mcr.microsoft.com/dotnet/aspnet"

FROM_LINE = re.compile(r"(?m)^FROM\s+(?P<image>\S+?):(?P<tag>[^@\s]+)(?:@(?P<digest>sha256:[0-9a-f]{64}))?\s+AS\s+\S+")


def dockerfiles():
    """Every Dockerfile that references a .NET SDK or runtime image."""
    found = []
    for path in sorted(REPO_ROOT.glob("src/*/Dockerfile")):
        text = path.read_text(encoding="utf-8")
        if SDK_IMAGE in text or RUNTIME_IMAGE in text:
            found.append((path, text))
    return found


def from_lines(text, image):
    return [m for m in FROM_LINE.finditer(text) if m.group("image") == image]


class SdkPinAgreement(unittest.TestCase):
    def setUp(self):
        self.sdk = json.loads(GLOBAL_JSON.read_text(encoding="utf-8"))["sdk"]
        self.files = dockerfiles()

    def test_the_parser_finds_the_images_it_is_meant_to_check(self):
        # Without this, a change to the FROM syntax would make every check below loop over nothing.
        self.assertGreaterEqual(len(self.files), 3, [str(p) for p, _ in self.files])
        sdk_lines = sum(len(from_lines(text, SDK_IMAGE)) for _, text in self.files)
        self.assertGreaterEqual(sdk_lines, 6)

    def test_global_json_pins_an_exact_patch_with_latestPatch(self):
        self.assertRegex(self.sdk["version"], r"^\d+\.\d+\.\d{3}$")
        self.assertEqual(self.sdk.get("rollForward"), "latestPatch")

    def test_every_sdk_stage_names_exactly_the_global_json_version_by_digest(self):
        for path, text in self.files:
            for match in from_lines(text, SDK_IMAGE):
                with self.subTest(file=str(path.relative_to(REPO_ROOT)), line=match.group(0)):
                    self.assertEqual(match.group("tag"), self.sdk["version"])
                    self.assertIsNotNone(match.group("digest"), "SDK stage is not pinned by digest")

    def test_every_runtime_stage_is_on_the_same_major_and_minor(self):
        major_minor = ".".join(self.sdk["version"].split(".")[:2])
        for path, text in self.files:
            for match in from_lines(text, RUNTIME_IMAGE):
                with self.subTest(file=str(path.relative_to(REPO_ROOT)), line=match.group(0)):
                    self.assertTrue(match.group("tag").startswith(major_minor), match.group("tag"))

    def test_every_dockerfile_with_an_sdk_stage_copies_global_json_into_it(self):
        # The pin only reaches the build if the file is in the image; the stage's own SDK would
        # otherwise be used whatever global.json says.
        for path, text in self.files:
            stages = len(from_lines(text, SDK_IMAGE))
            if stages:
                with self.subTest(file=str(path.relative_to(REPO_ROOT))):
                    self.assertEqual(len(re.findall(r"(?m)^COPY global\.json \.$", text)), stages)


if __name__ == "__main__":
    unittest.main()
