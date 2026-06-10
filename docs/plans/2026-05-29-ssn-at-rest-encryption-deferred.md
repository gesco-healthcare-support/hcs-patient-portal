---
feature: ssn-at-rest-encryption
date: 2026-05-29
status: deferred
base-branch: main
related-issues: []
note: Captured research only. Its own RPE cycle later. Does NOT gate the ssn-redact-on-type front-end PR.
---

## Status

DEFERRED by Adrian (2026-05-29). This is a research capture so the work is not
lost; it is not scheduled for the current 6-feature batch. The front-end
`ssn-redact-on-type` feature proceeds independently.

## Problem

`Patient.SocialSecurityNumber` is stored in plaintext in SQL Server. The app
ALSO looks patients up by SSN (deduplication/matching), so any encryption scheme
must keep the value queryable by equality.

## Recommendation (research 2026-05-29, HIGH confidence / official sources)

- Baseline: enable **TDE** (protects backups/files; low effort) -- necessary but
  not sufficient (a privileged DB login still reads plaintext).
- Primary: **SQL Server Always Encrypted, deterministic encryption** on the SSN
  column (supports `=` / `IN` -- the patient-matching lookups), with the Column
  Master Key in **Azure Key Vault**. Handled at the ADO.NET driver level, so it
  sidesteps EF Core value-converter query limitations.
- Critical caveat: SSN is low-entropy (~1e9 values). Any equality-preserving
  scheme (deterministic encryption OR a hash) leaks "same SSN = same
  ciphertext/hash" and is brute-forceable if the key is exposed. The key/pepper
  MUST live outside the database (Key Vault).
- Alternative if Always Encrypted is ruled out (no Azure / EF migration
  friction): app-level **AES-256-GCM** via EF Core value converter (random IV) +
  a separate `SsnHash = HMAC-SHA256(pepper, normalize(ssn))` **blind-index**
  column for lookups; pepper in Key Vault. More code; equivalent guarantees.
- Do NOT use ABP `IStringEncryptionService` (fixed IV -- meant for config
  strings, not PII columns).
- Standards: NIST SP 800-122 (PII), NIST SP 800-53 SC-28/SC-13, HIPAA addressable
  encryption 45 CFR 164.312(a), OWASP Cryptographic Storage + Top 10:2025 A04.

## Open questions before this becomes a plan

- Is Azure Key Vault available in the target environment? (drives CMK/pepper
  storage and whether Always Encrypted is operationally viable.)
- Is `Microsoft.Data.SqlClient >= 5.1` in the project? (first-class Always
  Encrypted support.)
- Is partial-SSN (last-4) SEARCH needed beyond display? (deterministic supports
  equality only; partial needs secure enclaves or a separate token.)
- Backfill plan for existing plaintext rows (Always Encrypted requires moving
  data through the client, not an in-place schema migration).

## Sources

- https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-database-engine
- https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/transparent-data-encryption
- https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions
- https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html
- https://nvlpubs.nist.gov/nistpubs/legacy/sp/nistspecialpublication800-122.pdf
- https://owasp.org/Top10/2025/A04_2025-Cryptographic_Failures/
