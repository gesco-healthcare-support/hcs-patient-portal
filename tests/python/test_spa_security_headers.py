"""The SPA origin carries its security headers in EVERY location, and its CSP is ENFORCED.

nginx does not inherit add_header into a location that declares its own, and every location in
angular/nginx.conf declares Cache-Control, so a header set once at server level would silently
vanish. This guards each location, and guards that nobody flips the proxy's CSP back to Report-Only or loosens script-src.
"""

import pathlib
import re
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
REQUIRED = ["X-Content-Type-Options", "X-Frame-Options", "Referrer-Policy", "Permissions-Policy"]


def strip_comments(text):
    return "\n".join(line.split("#", 1)[0] for line in text.splitlines())


def locations(text):
    out = []
    for m in re.finditer(r"(?m)^\s*location\s+.*\{[ \t]*$", text):
        depth, i = 1, m.end()
        while depth and i < len(text):
            depth += {"{": 1, "}": -1}.get(text[i], 0)
            i += 1
        out.append((m.group(0).strip(), text[m.end():i - 1]))
    return out


class SpaSecurityHeaders(unittest.TestCase):
    def test_every_location_repeats_the_header_set(self):
        text = strip_comments((REPO_ROOT / "angular/nginx.conf").read_text(encoding="utf-8"))
        locs = locations(text)
        self.assertGreaterEqual(len(locs), 5, "parser found too few locations")
        for name, body in locs:
            for header in REQUIRED:
                with self.subTest(location=name, header=header):
                    self.assertRegex(body, r"add_header\s+" + header + r"\s+\"[^\"]+\"\s+always\s*;")

    def test_proxy_csp_is_enforced_and_script_src_is_strict(self):
        text = strip_comments((REPO_ROOT / "docker/nginx-proxy/default.conf.template").read_text(encoding="utf-8"))
        self.assertNotIn("Report-Only", text)
        policies = re.findall(r'add_header\s+Content-Security-Policy\s+"([^"]*)"', text)
        spa = [p for p in policies if "*.api" in p]
        self.assertEqual(len(spa), 1, "expected exactly one enforced SPA policy")
        policy = spa[0]
        self.assertIn("script-src 'self';", policy)
        self.assertNotIn("unsafe-eval", policy)
        self.assertNotIn("report-uri", policy)
        script_src = re.search(r"script-src([^;]*)", policy).group(1)
        self.assertNotIn("unsafe-inline", script_src)
        self.assertNotIn("unsafe-eval", script_src)

if __name__ == "__main__":
    unittest.main()
