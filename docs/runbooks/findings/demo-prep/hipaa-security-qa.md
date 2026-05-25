---
title: HIPAA / security Q&A defense brief
date: 2026-05-25
status: ready
audience: Adrian (presenter)
---

# HIPAA / Security Q&A -- Tuesday Demo Defense Brief

**Note on Gesco's actual posture:** Where the answer depends on
specific Gesco infrastructure (encryption choices, audit log
retention configuration, BAA inventory), I use the hedge phrase
**"This is the standard requirement; we should confirm our current
posture matches."** No invented claims.

## 1. Covered Entity / Business Associate Status

**Q: Is this portal regulated by HIPAA? Workers' comp insurers
aren't covered entities, right?**

A workers' compensation insurer is explicitly excluded from HIPAA's
definition of "health plan" (45 CFR 160.103), so the carrier itself
is not a covered entity. However, the clinics performing the medical
evaluations are covered entities (health care providers transmitting
electronic transactions), and Gesco -- by creating, receiving,
maintaining, or transmitting PHI on their behalf -- meets the
definition of a business associate under 45 CFR 160.103. We are
therefore directly liable under the HIPAA Privacy, Security, and
Breach Notification Rules and operate under signed Business Associate
Agreements with each clinic. This is the standard posture; we should
confirm BAAs are executed with every covered-entity clinic.

Sources: HHS Covered Entities and Business Associates; HHS Business
Associates FAQ.

## 2. HIPAA Security Rule Technical Safeguards (45 CFR § 164.312)

**Q: Walk me through your technical safeguards.**

| Safeguard | Citation | Portal implementation |
|---|---|---|
| (a) Access Control | 164.312(a) | RBAC, unique user IDs via OpenIddict -- no shared logins. Auto-logoff + encryption are addressable. |
| (b) Audit Controls | 164.312(b) | Required, no opt-out. Structured audit events (who, what, when, record ID, source IP). |
| (c) Integrity | 164.312(c) | EF Core change tracking, TLS integrity, DB constraints, checksum-verified backups. |
| (d) Person/Entity Authentication | 164.312(d) | OpenIddict OAuth2/OIDC + ABP Identity (PBKDF2). MFA recommended for privileged roles. |
| (e) Transmission Security | 164.312(e) | TLS 1.2+ end-to-end, HSTS enforced. |

Note: A 2025 NPRM proposes to eliminate the addressable/required
distinction -- every spec would become mandatory. We are tracking
the rule.

Sources: eCFR 45 CFR 164.312; HHS Technical Safeguards Guidance.

## 3. Minimum Necessary Standard -- Justifying the F4-01 SSN Redaction

**Q: Why are some users seeing the SSN masked? Isn't that data
already in the system?**

The HIPAA Privacy Rule's Minimum Necessary standard (45 CFR
164.502(b) and 164.514(d)) requires us to limit any use or disclosure
of PHI to the smallest amount reasonably needed for the role's
purpose. Front-desk staff scheduling appointments do not need full
SSNs to perform their function, so we apply server-side role-based
redaction -- only roles with an evidenced clinical or billing need
see the full identifier. Failure mode if violated: an OCR enforcement
finding for "impermissible use," potential CMIA private-right-of-
action class action in California, and audit-log evidence of
unnecessary access would be cited as proof we did not operationalize
least privilege.

Source: HHS Minimum Necessary guidance.

## 4. Encryption At Rest vs In Transit

**Q: Is the data encrypted? Is encryption actually required?**

Both specifications today are technically "addressable" -- 164.312
(a)(2)(iv) for encryption at rest and 164.312(e)(2)(ii) for
encryption in transit -- meaning we must either implement them or
document a risk-based reason for an equivalent alternative. In
practice OCR treats unencrypted ePHI as evidence of neglect, and the
2025 NPRM proposes making both required. The portal uses TLS 1.2+
for all transit (Angular <-> HttpApi.Host <-> AuthServer <-> SQL
Server) and SQL Server TDE / Azure encryption at rest consistent
with NIST SP 800-111 (storage) and SP 800-52 (transport). Using
NIST-aligned encryption also qualifies for breach safe-harbor --
encrypted ePHI that is lost is not a reportable breach. This is the
standard posture; we should confirm TDE and disk-level encryption
are enabled in production.

Sources: HHS HIPAA Guidance on Encryption; NIST SP 800-52 Rev. 2.

## 5. Audit Logs -- Events, Retention, Access

**Q: What gets logged, how long, and who can read the logs?**

45 CFR 164.312(b) requires audit controls. 45 CFR 164.316(b)(2)(i)
sets the retention period at six years from creation or last
effective date, whichever is later -- California may impose longer.
The portal logs: authentication (success/fail), PHI read/create/
update/delete with subject record ID, role/permission changes,
exports, and administrative actions. Logs are written to an append-
only sink with restricted access. 45 CFR 164.308(a)(1)(ii)(D) also
requires periodic log review. This is the standard requirement; we
should confirm our retention is configured to at least 6 years and
that log-read permission is scoped to a small security role.

Sources: eCFR 45 CFR 164.312, 164.316.

## 6. Breach Notification (500+ Records)

**Q: What happens if 500 records leak?**

Under the Breach Notification Rule (45 CFR §§ 164.400-414), we must
notify: (a) each affected individual within 60 days of discovery;
(b) the HHS Secretary within 60 days for any breach affecting 500+
individuals; (c) prominent media outlets serving the jurisdiction
when 500+ residents of a single state are affected -- also within
60 days. Substitute notice on our website (90 days) is required if
we lack current contact info for 10+ individuals. As a business
associate, Gesco must notify the covered-entity clinic without
unreasonable delay (no later than 60 days). Encrypted data lost per
NIST standards qualifies for breach safe harbor and is not
reportable.

Source: HHS Breach Notification Rule.

## 7. De-Identification / Safe Harbor -- Why Test Data Matters

**Q: Are you using real patient data in dev or testing?**

The Safe Harbor method under 45 CFR 164.514(b)(2) treats data as
de-identified only when all 18 enumerated identifiers are removed.
We follow a synthetic-data convention (.claude/rules/test-data.md,
hipaa-data.md): no real PHI ever appears in code, fixtures,
screenshots, or logs. Why it matters: real-looking data in
screenshots or test fixtures can leak via repos, CI logs, and bug
reports -- that's a reportable breach with no safe-harbor protection.

Source: HHS De-identification Guidance.

## 8. California Overlay -- CMIA and CCPA/CPRA

**Q: How does California law change things?**

CMIA (Cal. Civ. Code § 56 et seq.) is generally stricter than
HIPAA: it covers a broader set of "providers" and "medical
information," and provides a private right of action with statutory
damages of $1,000 per violation plus actuals and attorney's fees.
HIPAA preempts only less protective state laws, so where they
conflict the stricter CMIA rule applies. CCPA/CPRA exempts
PHI/medical information at the data level when held by a HIPAA
covered entity or business associate -- but does NOT exempt employee
data, marketing/analytics data, or non-PHI personal information
collected outside the clinical context. Practical takeaway: our
portal data is governed by the strictest of HIPAA-or-CMIA. This is
the standard posture; we should confirm CMIA-compliant disclosure
language exists in our patient consents.

Sources: California Civ. Code § 56; CA AG CCPA.

## 9. NIST SP 800-66 Rev. 2 and SP 800-122

**Q: Do you follow NIST guidance?**

Both are recommended, not legally mandatory -- but they are the most
defensible interpretation of "reasonable and appropriate" under the
Security Rule, and OCR cites them in enforcement. NIST SP 800-66
Rev. 2 (Feb 2024) is the official HIPAA Security Rule implementation
guide; it maps every § 164.312 standard to NIST CSF subcategories
and SP 800-53 r5 controls. NIST SP 800-122 is the broader PII
confidentiality guide. We use SP 800-66r2 as the control-mapping
reference. This is the standard practice; we should confirm our risk
analysis explicitly references the SP 800-66r2 crosswalk.

Sources: NIST SP 800-66 Rev. 2; NIST SP 800-122.

## 10. OWASP Top 10 (2021) -> Stack Mitigations

**Q: What about OWASP risks specific to your stack?**

| OWASP 2021 | Mitigation |
|---|---|
| A01 Broken Access Control | ABP `[Authorize]` policies + nested `CaseEvaluationPermissions` |
| A02 Cryptographic Failures | TLS 1.2+, HSTS, SQL Server TDE, NIST-aligned ciphers |
| A03 Injection (SQLi + XSS) | EF Core parameterized LINQ; Angular default DOM sanitization, strict CSP |
| A04 Insecure Design | Domain-service layer for business rules (AppointmentManager) |
| A05 Security Misconfiguration | Secrets in appsettings.secrets.json/env vars, never hardcoded |
| A06 Vulnerable Components | Dependabot + npm audit + NuGet vuln scanning |
| A07 Identification & Auth Failures | OpenIddict + ABP Identity (PBKDF2); secure HttpOnly SameSite cookies |
| A08 Software & Data Integrity | Lockfiles + checksum verification |
| A09 Logging & Monitoring Failures | Append-only audit logs |
| A10 SSRF | Allowlist of external endpoints |

This is the standard mapping; we should confirm CSP headers and
SAST/DAST are wired into the build pipeline.

Source: OWASP Top 10 2021.
