---
title: Localization probes + brand-name locale gap
date: 2026-05-26
status: ready
audience: Adrian (presenter)
---

# Localization quirks

## ABP-bundled locales available (~20+)

The Razor login pages have a language switcher with these
options (all ABP-bundled, found in dropdown):

English, Arabic, Chinese (Simplified), Chinese (Traditional),
English (United Kingdom), Czech, Finnish, French, Icelandic,
Hungarian, Hindi, German (Germany), Romanian (Romania),
Portuguese (Brazil), Italian, Russian, Slovak, Spanish, Swedish,
Turkish (and more below the fold).

## Brand-name gap (parity break)

**Document title in English:** `Sign in | Appointment Portal`
**Document title in Spanish (and presumably other locales):**
`Sign in | CaseEvaluation`

The OLD parity rename to "Appointment Portal" only applied to
`en.json`. Other locales fall back to the project name
"CaseEvaluation".

Affected:
- Browser tab title (`<title>` tag)
- LeptonX brand name in top bar
- (Possibly) email subject lines if the system fires emails in
  non-English locales

**Demo tactic:** Stay in English. If audience asks "does it
support other languages?" — verbal answer: "Yes, the framework
ships with ~20 locales. We've localized the English copy fully;
post-parity, we'll either fall back to English for unlocalized
strings or roll a translation pack as customers ask."

## Other locale observations

- Switching to Spanish keeps login form labels in English
  ("Sign in", "Email address", "Password") -- ABP framework's
  Spanish translation either isn't loaded or isn't applied.
  Suggests we're only shipping English UI strings; framework
  i18n is partial.
- The language dropdown itself worked correctly -- the "English"
  button became "Spanish" after switch, confirming the cookie /
  CurrentCulture switch took effect server-side.

## Risk

Low. None of the 5 demo flows trigger a locale switch. The
language dropdown is visible on the login page but unlikely an
audience member would click it during the demo.

If a Spanish-speaking user picks Spanish via the dropdown on
their own, they'd see:
- Brand title degrade to "CaseEvaluation"
- Most app strings in English (since we only have en.json)
- ABP framework strings in mixed English/Spanish depending on
  what's in the ABP locale pack

For Tuesday demo: leave locale as English. Don't touch the
language switcher.
