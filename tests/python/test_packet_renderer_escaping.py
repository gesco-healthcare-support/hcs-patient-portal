"""Packet token values are escaped and the renderer fetches nothing remote.

Token VALUES are patient/party-entered text. `app.py` substitutes them into the template HTML
before WeasyPrint renders it, so an unescaped value is markup: it can restyle or rewrite a
document carrying PHI, and a `<img src=http://...>` makes the renderer issue a request.

Runs against the stubbed third-party imports used by test_packet_renderer_paths.py. The real
render is proven separately against the built image.
"""

import types
import unittest

from test_packet_renderer_paths import load_app

TOK = "##Patients.FirstName##"
HOSTILE = (
    '<img src="http://169.254.1.1/x"><link rel=stylesheet href="http://10.0.0.1/a.css">'
    "<script>alert(1)</script> Tom & \"Jerry\" 'x'"
)


class SubstituteTokensTests(unittest.TestCase):
    def setUp(self):
        self.app = load_app()

    def test_markup_in_a_value_is_escaped(self):
        out = self.app.substitute_tokens(f"<p>{TOK}</p>", {TOK: HOSTILE})
        self.assertNotIn("<img", out)
        self.assertNotIn("<link", out)
        self.assertNotIn("<script", out)
        self.assertIn("&lt;img src=&quot;http://169.254.1.1/x&quot;&gt;", out)
        self.assertIn("&lt;script&gt;", out)
        self.assertIn("Tom &amp; &quot;Jerry&quot; &#x27;x&#x27;", out)
        self.assertTrue(out.startswith("<p>") and out.endswith("</p>"))

    def test_value_cannot_break_out_of_an_attribute(self):
        out = self.app.substitute_tokens(f'<input value="{TOK}">', {TOK: '"><img src=x>'})
        self.assertEqual(out, '<input value="&quot;&gt;&lt;img src=x&gt;">')

    def test_ordinary_values_are_unchanged(self):
        out = self.app.substitute_tokens(TOK, {TOK: "Maria Gonzalez-Lopez 123 Main St, Apt 4"})
        self.assertEqual(out, "Maria Gonzalez-Lopez 123 Main St, Apt 4")

    def test_unknown_tokens_stay_literal_and_none_is_blank(self):
        out = self.app.substitute_tokens(f"{TOK}|##A.B##", {TOK: None})
        self.assertEqual(out, "|##A.B##")

    def test_a_value_that_looks_like_a_token_is_not_resubstituted(self):
        out = self.app.substitute_tokens(f"{TOK} ##A.B##", {TOK: "##A.B##", "##A.B##": "X"})
        self.assertEqual(out, "##A.B## X")

    def test_newlines_pass_through_as_whitespace(self):
        # No token carries deliberate markup or line breaks (checked against the .NET resolver);
        # a newline stays a newline, which HTML renders as ordinary whitespace, as before.
        out = self.app.substitute_tokens(TOK, {TOK: "line1\nline2"})
        self.assertEqual(out, "line1\nline2")


class UrlFetcherTests(unittest.TestCase):
    def setUp(self):
        self.app = load_app()

    def test_remote_and_local_schemes_are_refused(self):
        for url in (
            "http://10.0.0.1/x",
            "https://example.test/a.css",
            "HTTP://EXAMPLE.TEST/",
            "ftp://example.test/x",
            "file:///etc/passwd",
            "/etc/passwd",
            "relative.png",
            "//host/x",
        ):
            with self.subTest(url=url), self.assertRaises(ValueError):
                self.app.refuse_remote_fetch(url)

    def test_refusal_message_does_not_echo_the_url(self):
        with self.assertRaises(ValueError) as ctx:
            self.app.refuse_remote_fetch("http://secret-host.internal/p?q=PHI")
        self.assertNotIn("secret-host", str(ctx.exception))

    def test_data_uris_are_delegated_to_weasyprint(self):
        calls = []
        fake = types.ModuleType("weasyprint")
        fake.default_url_fetcher = lambda url, *a, **k: calls.append(url) or {"string": b"x"}
        import sys

        saved = sys.modules.get("weasyprint")
        sys.modules["weasyprint"] = fake
        try:
            result = self.app.refuse_remote_fetch("data:image/png;base64,AAAA")
        finally:
            if saved is None:
                sys.modules.pop("weasyprint", None)
            else:
                sys.modules["weasyprint"] = saved
        self.assertEqual(calls, ["data:image/png;base64,AAAA"])
        self.assertEqual(result, {"string": b"x"})


class RenderWiringTests(unittest.TestCase):
    """render() must escape and must hand WeasyPrint the refusing fetcher."""

    def _render(self, tokens):
        app = load_app()
        seen = {}

        class FakeHTML:
            def __init__(self, string=None, **kwargs):
                seen["html"] = string
                seen["kwargs"] = kwargs

            def write_pdf(self, path, **kw):
                with open(path, "wb") as fh:
                    fh.write(b"%PDF-fake")

        app.HTML = FakeHTML
        app.post_process = types.SimpleNamespace(finalize=lambda path: None)
        app.request = types.SimpleNamespace(
            get_json=lambda silent=True: {"template": "doctor", "tokens": tokens}
        )
        app.Response = lambda body, mimetype=None: (body, mimetype)
        app._TEMPLATE_CACHE["doctor"] = f"<html><body>{TOK}</body></html>"
        app.render()
        return app, seen

    def test_render_escapes_values_before_weasyprint_sees_them(self):
        _, seen = self._render({TOK: HOSTILE})
        self.assertNotIn("<img", seen["html"])
        self.assertIn("&lt;img", seen["html"])

    def test_render_passes_the_refusing_fetcher(self):
        app, seen = self._render({TOK: "x"})
        self.assertIs(seen["kwargs"].get("url_fetcher"), app.refuse_remote_fetch)


if __name__ == "__main__":
    unittest.main()
