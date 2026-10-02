"""The proxy template keeps its per-client limits, TLS settings and hidden version banner (B5).

These are http-level and per-server-block directives that a later edit can drop without any build
failing: nginx starts happily without them. Behaviour was proven with a live harness when B5 landed
(stub upstreams, separate buckets per client, 429 on burst, TLS 1.2/1.3 with RSA and ECDSA
certificates, TLS 1.1 refused). This test is the cheap guard that keeps the configuration that
harness proved.

The ECDSA assertion is the one most likely to be "simplified" away. acme.sh issues an ECDSA
certificate by default, and an RSA-only cipher list shares no TLS 1.2 suite with it, so every TLS
1.2 handshake would fail while TLS 1.3 kept working, which is easy to miss.
"""

import pathlib
import re
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
TEMPLATE = REPO_ROOT / "docker" / "nginx-proxy" / "default.conf.template"


def strip_comments(text):
    return "\n".join(line.split("#", 1)[0] for line in text.splitlines())


def top_level(text):
    """The template with every `server { ... }` body removed, i.e. the http-level directives."""
    out, i = [], 0
    for match in re.finditer(r"(?m)^\s*server\s*\{", text):
        if match.start() < i:
            continue
        out.append(text[i:match.start()])
        depth, j = 1, match.end()
        while depth and j < len(text):
            depth += {"{": 1, "}": -1}.get(text[j], 0)
            j += 1
        i = j
    out.append(text[i:])
    return "".join(out)


def server_block(text, server_name):
    """The body of the server block whose server_name line is exactly `server_name`."""
    for match in re.finditer(r"(?m)^\s*server\s*\{", text):
        depth, j = 1, match.end()
        while depth and j < len(text):
            depth += {"{": 1, "}": -1}.get(text[j], 0)
            j += 1
        body = text[match.end():j - 1]
        if re.search(r"(?m)^\s*server_name\s+" + re.escape(server_name) + r"\s*;", body):
            return body
    return None


class NginxLimitsAndTls(unittest.TestCase):
    def setUp(self):
        self.text = strip_comments(TEMPLATE.read_text(encoding="utf-8"))
        self.http = top_level(self.text)

    def test_the_version_banner_is_off(self):
        self.assertRegex(self.http, r"(?m)^\s*server_tokens\s+off\s*;")

    def test_two_separate_per_client_zones_and_a_429(self):
        self.assertRegex(self.http, r"limit_req_zone\s+\$binary_remote_addr\s+zone=api_per_ip:\d+m\s+rate=\d+r/s\s*;")
        self.assertRegex(self.http, r"limit_req_zone\s+\$binary_remote_addr\s+zone=auth_per_ip:\d+m\s+rate=\d+r/s\s*;")
        self.assertRegex(self.http, r"(?m)^\s*limit_req_status\s+429\s*;")

    def test_the_api_and_auth_blocks_apply_their_own_zone(self):
        cases = (("*.api.${BASE_DOMAIN}", "api_per_ip"), ("*.auth.${BASE_DOMAIN}", "auth_per_ip"))
        for server_name, zone in cases:
            with self.subTest(server_name=server_name):
                body = server_block(self.text, server_name)
                self.assertIsNotNone(body, f"no server block for {server_name}; the parser or the template changed")
                self.assertRegex(body, r"limit_req\s+zone=" + zone + r"\b")

    def test_only_tls_1_2_and_1_3(self):
        match = re.search(r"(?m)^\s*ssl_protocols\s+([^;]+);", self.http)
        self.assertIsNotNone(match, "no http-level ssl_protocols")
        self.assertEqual(set(match.group(1).split()), {"TLSv1.2", "TLSv1.3"})

    def test_the_cipher_list_keeps_ecdsa_suites_for_tls_1_2(self):
        match = re.search(r"(?m)^\s*ssl_ciphers\s+([^;]+);", self.http)
        self.assertIsNotNone(match, "no http-level ssl_ciphers")
        suites = match.group(1).split(":")
        self.assertTrue(any(s.startswith("ECDHE-ECDSA-") for s in suites), suites)
        self.assertTrue(any(s.startswith("ECDHE-RSA-") for s in suites), suites)

    def test_the_parser_sees_a_block_level_directive_as_not_http_level(self):
        sample = "server_tokens on;\nserver {\n server_name a.b;\n server_tokens off;\n}\n"
        self.assertNotRegex(top_level(sample), r"server_tokens\s+off")
        self.assertIsNotNone(server_block(sample, "a.b"))
        self.assertIsNone(server_block(sample, "c.d"))


if __name__ == "__main__":
    unittest.main()
