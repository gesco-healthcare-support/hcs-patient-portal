"""Every nginx server block logs through the query-free access log format (B10).

Query strings here carry patient search fields, the booking lookup's email and the one-time tokens
in emailed links, and referers carry the previous page's full URL. The stock nginx.conf declares
`access_log ... main` at http level, and `main` logs `$request` (query included) and
`$http_referer`. A server block with no `access_log` of its own INHERITS that, silently.

So the property that matters is per server block, and it breaks the moment someone adds a block
without the line. B5 and later work add blocks to the proxy template. This is the guard.
"""

import pathlib
import re
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
CONFIGS = ["docker/nginx-proxy/default.conf.template", "angular/nginx.conf"]
FORMAT_NAME = "portal_noquery"


def strip_comments(text):
    return "\n".join(line.split("#", 1)[0] for line in text.splitlines())


def server_blocks(text):
    """The bodies of top-level `server { ... }` blocks, by brace matching (comments already removed)."""
    blocks = []
    for match in re.finditer(r"(?m)^\s*server\s*\{", text):
        depth, i = 1, match.end()
        while depth and i < len(text):
            depth += {"{": 1, "}": -1}.get(text[i], 0)
            i += 1
        blocks.append(text[match.end():i - 1])
    return blocks


def log_format(text, name):
    match = re.search(r"log_format\s+" + name + r"\s+(.*?);", text, re.DOTALL)
    return match.group(1) if match else None


class NginxAccessLog(unittest.TestCase):
    def test_every_server_block_logs_with_the_query_free_format(self):
        for relative in CONFIGS:
            text = strip_comments((REPO_ROOT / relative).read_text(encoding="utf-8"))
            blocks = server_blocks(text)
            self.assertGreater(len(blocks), 0, f"{relative}: found no server blocks; the parser is broken")
            for number, block in enumerate(blocks, start=1):
                with self.subTest(config=relative, block=number):
                    self.assertRegex(block, r"access_log\s+\S+\s+" + FORMAT_NAME + r"\s*;")

    def test_the_format_logs_the_path_without_its_query_and_no_referer(self):
        for relative in CONFIGS:
            text = strip_comments((REPO_ROOT / relative).read_text(encoding="utf-8"))
            fmt = log_format(text, FORMAT_NAME)
            with self.subTest(config=relative):
                self.assertIsNotNone(fmt, f"{relative} declares no log_format {FORMAT_NAME}")
                self.assertIn("$uri", fmt)
                for leaking in ("$request ", "$request\"", "$request_uri", "$args", "$query_string", "$http_referer"):
                    self.assertNotIn(leaking, fmt)

    def test_the_parser_finds_every_block_and_sees_a_missing_line(self):
        sample = "server {\n listen 80;\n access_log x portal_noquery;\n location / { }\n}\nserver {\n listen 443;\n}\n"
        blocks = server_blocks(sample)
        self.assertEqual(len(blocks), 2)
        self.assertRegex(blocks[0], r"access_log\s+\S+\s+portal_noquery\s*;")
        self.assertNotRegex(blocks[1], r"access_log")
        self.assertEqual(server_blocks(strip_comments("# server {\n")), [])


if __name__ == "__main__":
    unittest.main()
