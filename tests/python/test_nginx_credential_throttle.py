"""The proxy keeps a dedicated sign-in throttle and an allow-listed, bounded MinIO vhost.

Behaviour was proven with a live harness when this landed (the pinned nginx image, rendered from the
template, stub-free: 11 credential POSTs pass, the 12th gets 429; GETs, /connect/token bursts and
static paths are never counted; an address outside the allow-list gets 403 on the MinIO vhost, and
the default private ranges still reach it). This is the cheap guard that keeps the configuration that
harness proved. It also evaluates the template's own `map` regexes against sample requests, so a
pattern that stops matching a credential path fails here rather than silently un-throttling it.
"""

import pathlib
import re
import unittest

from test_nginx_limits_and_tls import TEMPLATE, server_block, strip_comments, top_level

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
COMPOSE = REPO_ROOT / "docker-compose.prod.yml"
ENV_EXAMPLE = REPO_ROOT / "env.prod.example"
AUTH_HOST = "*.auth.${BASE_DOMAIN}"
MINIO_HOST = "minio.${BASE_DOMAIN}"


def map_rules(http_text, variable):
    """The ordered (pattern, value) pairs of `map ... $variable { ... }`, regexes only."""
    match = re.search(r"map\s+\"[^\"]+\"\s+\$" + variable + r"\s*\{(.*?)\n\}", http_text, re.DOTALL)
    assert match, f"no map for {variable}"
    rules = []
    for line in match.group(1).splitlines():
        line = line.strip().rstrip(";")
        if line.startswith("~*"):
            pattern, value = line[2:].rsplit(None, 1)
            rules.append((re.compile(pattern, re.IGNORECASE), value))
    return rules


def throttled(rules, method, uri):
    for pattern, value in rules:
        if pattern.search(f"{method}:{uri}"):
            return value != '""'
    return False


class CredentialThrottle(unittest.TestCase):
    def setUp(self):
        self.text = strip_comments(TEMPLATE.read_text(encoding="utf-8"))
        self.http = top_level(self.text)
        self.signin = map_rules(self.http, "signin_throttle_key")
        self.token = map_rules(self.http, "token_throttle_key")

    def test_dedicated_zones_are_keyed_by_the_maps_and_far_tighter_than_the_generic_zone(self):
        for zone, var, unit in (("signin_per_ip", "signin_throttle_key", "r/m"), ("token_per_ip", "token_throttle_key", "r/m")):
            with self.subTest(zone=zone):
                self.assertRegex(self.http, r"limit_req_zone\s+\$" + var + r"\s+zone=" + zone + r":\d+m\s+rate=\d+" + unit)
        self.assertRegex(self.http, r"map\s+\"\$request_method:\$uri\"\s+\$signin_throttle_key")
        self.assertRegex(self.http, r"(?m)^\s*limit_req_status\s+429\s*;")

    def test_the_auth_vhost_applies_both_zones_and_keeps_the_generic_one(self):
        body = server_block(self.text, AUTH_HOST)
        self.assertIsNotNone(body)
        for zone in ("auth_per_ip", "signin_per_ip", "token_per_ip"):
            with self.subTest(zone=zone):
                self.assertRegex(body, r"limit_req\s+zone=" + zone + r"\b")

    def test_no_location_block_was_added_so_no_header_has_to_be_repeated(self):
        body = server_block(self.text, AUTH_HOST)
        self.assertEqual(len(re.findall(r"\blocation\b", body)), 1)
        location = body[body.index("location"):]
        self.assertNotIn("add_header", location)  # the server-level HSTS stays inherited

    def test_credential_posts_are_counted(self):
        for uri in (
            "/Account/Login", "/account/login", "/Account/Login/", "/Account/ForgotPassword",
            "/Account/ResetPassword", "/Account/ResendVerification", "/Account/Register",
            "/api/account/login", "/API/Account/checkPassword", "/api/account/send-password-reset-code",
            "/api/account/reset-password", "/api/account/register", "/api/account/my-profile/change-password",
            "/api/account/some-future-endpoint",
        ):
            with self.subTest(uri=uri):
                self.assertTrue(throttled(self.signin, "POST", uri))
        self.assertTrue(throttled(self.token, "POST", "/connect/token"))
        self.assertTrue(throttled(self.token, "POST", "/Connect/Token/"))

    def test_page_loads_the_authorize_redirect_and_assets_are_not_counted(self):
        for method, uri in (
            ("GET", "/Account/Login"), ("GET", "/Account/ForgotPassword"), ("GET", "/connect/authorize"),
            ("POST", "/connect/authorize"), ("GET", "/api/account/my-profile"), ("GET", "/libs/x.js"),
            ("POST", "/api/account/dynamic-claims/refresh"), ("POST", "/Account/Logout"),
        ):
            with self.subTest(method=method, uri=uri):
                self.assertFalse(throttled(self.signin, method, uri))
        self.assertFalse(throttled(self.token, "GET", "/connect/token"))
        self.assertFalse(throttled(self.token, "POST", "/connect/authorize"))


class MinioVhost(unittest.TestCase):
    def setUp(self):
        self.text = strip_comments(TEMPLATE.read_text(encoding="utf-8"))
        self.body = server_block(self.text, MINIO_HOST)

    def test_vhost_is_allow_listed_and_denies_everyone_else(self):
        self.assertIsNotNone(self.body)
        allow = self.body.index("${MINIO_ALLOW_DIRECTIVES}")
        deny = self.body.index("deny all;")
        self.assertLess(allow, deny, "deny all must come after every allow")
        self.assertRegex(self.body, r"allow\s+127\.0\.0\.1\s*;")

    def test_body_size_is_bounded_and_requests_are_limited(self):
        self.assertNotRegex(self.body, r"client_max_body_size\s+0\s*;")
        self.assertRegex(self.body, r"client_max_body_size\s+\d+m\s*;")
        self.assertRegex(self.body, r"limit_req\s+zone=minio_per_ip\b")
        self.assertRegex(top_level(self.text), r"limit_req_zone\s+\$binary_remote_addr\s+zone=minio_per_ip:\d+m")

    def test_compose_always_defines_the_variable_with_a_private_range_default_and_never_requires_it(self):
        compose = COMPOSE.read_text(encoding="utf-8")
        match = re.search(r"MINIO_ALLOW_DIRECTIVES:\s*\"\$\{MINIO_ALLOW_DIRECTIVES:-([^}]*)\}\"", compose)
        self.assertIsNotNone(match, "compose must define it with a :- default")
        for cidr in ("10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"):
            self.assertIn(f"allow {cidr};", match.group(1))
        self.assertNotRegex(compose, r"MINIO_ALLOW_DIRECTIVES:[?]")
        self.assertNotIn("${MINIO_ALLOW_DIRECTIVES:?", compose)

    def test_env_example_documents_it_commented_out(self):
        env = ENV_EXAMPLE.read_text(encoding="utf-8")
        self.assertRegex(env, r"(?m)^# MINIO_ALLOW_DIRECTIVES=")
        self.assertNotRegex(env, r"(?m)^MINIO_ALLOW_DIRECTIVES=")


if __name__ == "__main__":
    unittest.main()
