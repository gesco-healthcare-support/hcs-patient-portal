# Session key encryption at rest

**Status:** finding recorded. **Design decided (section 6). Session-survival question settled by
measurement (section 4). No code change applied yet** -- that is a separate step, on Adrian's go.
**Raised:** 2026-09-01, during the production-hardening epic (phase 1 item 1.6).
**Moved out of that epic by Adrian's decision on 2026-09-01**, to be designed and handled separately
on `main`. The epic's phase-1 file now points here.

**If you are picking this up cold, read section 6 first.** It carries the decisions and why they
were made; sections 1 to 5 are the evidence those decisions rest on.

This document is written to be read cold. It assumes no knowledge of the conversation that produced
it, and every claim below can be re-checked from the anchors given.

---

## 1. The finding, with its context

The cryptographic keys that protect signed-in sessions and email-confirmation tokens are **stored
unencrypted** in Redis.

**Read the mitigation before reacting to that sentence.** In production those keys are reachable
only from inside the container network:

- the Redis service publishes **no port** in `docker-compose.prod.yml` -- no `ports:`, no `expose:`
- so it is not reachable from the host, the office LAN, or the internet
- an attacker would already need code execution inside the deployment to read them

This is a **defence-in-depth gap, not an open door.** It raises the cost of any other breach rather
than creating one on its own. It is worth fixing properly and it is not worth rushing.

**What the keys actually protect.** ASP.NET Core Data Protection keys underwrite the authentication
cookie, ABP Identity tokens such as email confirmation and password reset, antiforgery tokens, and
anything else the framework protects. Practically:

- **stolen keys** are a session-forgery primitive -- an attacker who can read them can mint a cookie
  that the application accepts as a signed-in user
- **lost keys** make everything already protected permanently undecipherable -- every live session
  ends and every outstanding confirmation and reset link stops working

That second consequence is not hypothetical, and the codebase already knows it. From
`docker-compose.prod.yml`, on the Redis service:

```yaml
# AOF persistence: the DataProtection keyring (login + email-confirmation tokens,
# shared across the authserver + api containers) MUST survive restarts, else every
# deploy/reboot logs everyone out and breaks pending confirmation links (G5).
command: ["redis-server", "--appendonly", "yes"]
```

---

## 2. Evidence

### 2.1 What the code does

Two processes configure Data Protection, and they do the same thing.

**API host** -- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`,
method `ConfigureDataProtection` at `:1103`, called from `:101`. The entire body after the
analyze-mode guard:

```csharp
var dataProtectionBuilder = context.Services.AddDataProtection().SetApplicationName("CaseEvaluation");

// Persist DataProtection keys to Redis whenever a Redis connection is
// configured, in BOTH dev and prod. Reason: AuthServer + HttpApi.Host
// run as separate Docker containers (separate filesystems), so the
// default key store at /root/.aspnet/DataProtection-Keys is per-
// container. ABP-Identity tokens (e.g. EmailConfirmation) generated
// by the API host fail validation when the AuthServer's confirm-email
// endpoint tries to decrypt them with a different key ring -- the
// request returns 403 with "Volo.Abp.Identity:InvalidToken".
// Redis-backed shared keys + matching SetApplicationName above make
// both processes interchangeable validators.
var redisConfig = configuration["Redis:Configuration"];
if (!string.IsNullOrWhiteSpace(redisConfig))
{
    var redis = ConnectionMultiplexer.Connect(redisConfig);
    dataProtectionBuilder.PersistKeysToStackExchangeRedis(redis, "CaseEvaluation-Protection-Keys");
}
```

**AuthServer** -- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs`
at `:384`. The same pattern, the same Redis key name, the same application name.

**Why it is written this way, which matters when changing it:** the two processes run as separate
containers with separate filesystems. Sharing one key ring is what lets a confirmation token minted
by the API validate at the AuthServer. This is deliberate and correct; the gap is that nothing
encrypts the shared ring.

### 2.2 What the code does not do

No key-protection mechanism is configured anywhere in the solution:

```bash
grep -rn "ProtectKeysWith\|UnprotectKeysWithAnyCertificate\|XmlEncryptor" --include=*.cs src/
# 0 matches
```

That is a whole-source result, not a reading of one file. Neither module registers a custom
`IXmlEncryptor`, and neither sets `KeyManagementOptions.XmlEncryptor`.

### 2.3 Why persisting to Redis removes the protection

This is the load-bearing claim, and it is stated by Microsoft rather than inferred here. Both
quotations are from the ASP.NET Core 10 documentation, matching the framework this system targets:

> "If you specify an explicit key persistence location, the data protection system **deregisters the
> default key encryption at rest mechanism**, so keys are no longer encrypted at rest. It's
> recommended that you additionally specify an explicit key encryption mechanism for production
> deployments."
> -- _Key storage providers in ASP.NET Core_

> "If the developer overrides the rules outlined above and points the Data Protection system at a
> specific key repository, **automatic encryption of keys at rest is disabled.** At-rest protection
> can be re-enabled via configuration."
> -- _Data Protection key management and lifetime in ASP.NET Core_

`PersistKeysToStackExchangeRedis` is exactly such an override. So the keys are written as plaintext
XML into Redis, and the platform will not encrypt them until something is configured to.

### 2.4 What protects the store itself

The Redis service in `docker-compose.prod.yml`, in full: image, `restart`, `mem_limit`, `logging`,
a `healthcheck`, `command: ["redis-server", "--appendonly", "yes"]`, and the `redisdata` volume.

| Property       | State                            | Consequence                                                   |
| -------------- | -------------------------------- | ------------------------------------------------------------- |
| Published port | **None** in production           | Not reachable from host, LAN or internet                      |
| Authentication | **None** (`requirepass` not set) | Anything on the container network can read the keys           |
| Persistence    | AOF enabled                      | The ring survives restarts, by design (see the comment above) |

The dev compose file does publish Redis, but binds it to `127.0.0.1`, so it is loopback-only even
there.

The practical shape of the risk: the keys are safe from outside, and completely unprotected against
anything that gets a foothold inside. Any container compromise, or anyone with shell access to the
host, reads them.

---

## 3. Options, and what each costs on this deployment

These containers run Linux, which rules two options out before preference enters into it.

| Mechanism                                        | Available here?                                          | What it actually requires                                                                                                                 |
| ------------------------------------------------ | -------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Windows DPAPI                                    | **No** -- docs say "Only applies to Windows deployments" | n/a                                                                                                                                       |
| Windows DPAPI-NG                                 | **No** -- Windows 8 / Server 2012+ only                  | n/a                                                                                                                                       |
| X.509 certificate (`ProtectKeysWithCertificate`) | Yes                                                      | A `.pfx` distributed to BOTH containers, plus its password; and a lifecycle for generating, storing, backing up, rotating and revoking it |
| Azure Key Vault (`ProtectKeysWithAzureKeyVault`) | Yes                                                      | An Azure dependency and credentials for both containers; ties this deployment to Azure                                                    |
| Custom `IXmlEncryptor`                           | Yes                                                      | Writing and owning cryptographic plumbing. Not recommended unless the two above are genuinely unworkable                                  |

**The code change is two lines. That is not the work.** Every available option requires distributing
a secret or adopting an external service, and deciding who holds it, how it is backed up, and what
happens when it changes. That is the part worth designing, and it is why this was moved out of the
hardening epic rather than shipped as a pull request.

**Registry check, 2026-09-01, so the Azure/AWS comparison is not taken on trust.** Azure has a
first-party encryptor package (`Azure.Extensions.AspNetCore.DataProtection.Keys` v1.6.4, owned by
`azure-sdk` / Microsoft). AWS has NO first-party equivalent -- only community KMS packages, and the
AWS-owned `Amazon.AspNetCore.DataProtection.SSM` provides key persistence rather than key
encryption. This matters because the Azure-versus-AWS decision for this deployment is still open;
see 6.1.

Relevant precedent already in this system: `docs/security/SECRETS-MANAGEMENT.md` records an existing
OpenIddict signing certificate whose password lives in configuration, and an outstanding rotation
task for it. Whatever is designed here should probably answer for both certificates rather than
creating a second, separate lifecycle.

---

## 4. Would turning it on break existing sessions?

**No. MEASURED 2026-09-01 on the local Docker stack, not inferred.** This section previously carried
a medium-high confidence answer assembled from documented behaviours. Adrian directed that it be
settled empirically before any design work continued, and it was. The original reasoning is kept
further down because it explains WHY the result comes out this way.

### 4.1 What was done, and what was observed

`ProtectKeysWithCertificate` was enabled in BOTH modules against a throwaway self-signed
certificate, on a stack whose Redis already held a real key ring created **2026-08-28** -- four days
before the test, so a genuinely pre-existing key rather than one minted for the occasion.

| Check                                            | Result                                                                 |
| ------------------------------------------------ | ---------------------------------------------------------------------- |
| Baseline ring                                    | 1 key, `07f9d340-73a1-4a70-bfd6-e5c79b3277c4`, zero encryption markers |
| Both containers start with encryption on         | Healthy -- certificate loaded, ring read                               |
| Ring after the change                            | **Byte-identical** to the baseline capture (`cmp`)                     |
| Ring length after the change                     | Still **1**                                                            |
| Login while encryption is on                     | Succeeded -- 302 plus `.AspNetCore.Identity.Application`               |
| That session on an authenticated page            | **200** with the cookie, **302** without it (control)                  |
| Same session after reverting to plaintext config | Still **200** vs **302**, still the same user                          |
| Ring at the end of the exercise                  | Byte-identical to the original capture                                 |

**The load-bearing observation is the ring length.** Data Protection mints a replacement key when it
cannot use what it finds in the store. It did not. So it read and used the pre-existing PLAINTEXT
key while configured to encrypt, which is the direction that was in doubt. Enabling encryption does
not rewrite, re-encrypt or invalidate keys already in the ring; it applies to keys written after it.

A second piece of evidence worth recording, because it is stronger than any quotation: the stored
master key element is tagged `requiresEncryption="true"` by the framework itself, and is held in
plaintext regardless. The system's own key file states the requirement that the Redis persistence
override cancelled.

### 4.2 Two test designs that were DISCARDED

Recorded because either would have read as a clean pass without a control, and anyone repeating this
work will be tempted by both:

1. **Antiforgery POST round-trip.** Re-posting the login form with a pre-change cookie returned 200.
   The control -- the same request with a deliberately corrupted cookie -- returned a BYTE-IDENTICAL
   200, and the log showed the antiforgery filter being skipped. The endpoint was not validating, so
   the test could not fail and therefore proved nothing.
2. **Cookie-reissue signal.** The theory was that an undecryptable antiforgery cookie would be
   reissued and a valid one reused. It was reissued unconditionally, including when no cookie was
   sent at all.

Neither is evidence. The evidence is the table above, whose authenticated probe has a working
negative control (200 with the session versus 302 without).

### 4.3 What remains inferred rather than measured

The measured session was created while encryption was ENABLED and survived the switch back to
plaintext. A session created BEFORE a switch TO encryption was not separately carried across. The
inference bridging that gap: the ring is byte-identical in both states, the same single key protects
both, and a Data Protection payload names the key that protected it -- so the two cases are
indistinguishable to the unprotect path. That is a short inference, but it is an inference.

### 4.4 Why the result comes out this way

The documented behaviours that produced the original estimate, and that the measurement now
confirms:

- "Created, active, and expired keys may all be used to unprotect incoming payloads."
- Keys are stored individually as XML, each with its own descriptor; an encrypted key carries a
  reference to the decryptor that opens it. A ring can therefore hold keys in different states.
- `UnprotectKeysWithAnyCertificate` exists specifically to "configure certificates which can be used
  to decrypt keys loaded from storage", for certificate rotation. It only makes sense if a ring can
  legitimately contain keys protected under different settings.
- On default-key selection: "new keys may have been configured with different algorithms or
  **encryption-at-rest mechanisms** than old keys, and the system should prefer the current
  configuration" -- a mechanism change rolls forward; it does not invalidate what came before.

**Settled.** The rollout does not need a maintenance window or a sign-out notice on account of this
change. Note the contrast with the OpenIddict signing certificate: rotating THAT does invalidate
issued tokens and does force re-authentication. The two changes have different blast radii, which is
why section 6 sequences them separately.

---

## 5. The override problem

**This is the part that deserves the most design attention, because the fix introduces a failure
mode that does not exist today.**

Right now the keys are readable by anything inside the deployment. That is the weakness. But it also
means there is no way to _lose_ them other than losing Redis itself, which AOF persistence already
guards against.

Encrypting them with a certificate inverts that. After the change:

- the key ring is readable **only** by whoever holds the certificate and its password
- **if that certificate is lost, the entire ring becomes undecipherable** -- and unlike a revoked
  key, there is no emergency override in the framework. Microsoft's own wording on deleting keys
  applies equally here: "all data protected by the key is permanently undecipherable, and there's no
  emergency override like there's with revoked keys"
- the practical result of losing it: every signed-in user is signed out, every outstanding
  confirmation and password-reset link is dead, and anything else protected by those keys cannot be
  read again

**AMENDED 2026-09-01: "anything else" turns out to be nothing, and that materially reduces how bad
this is.** The sentence above was written before the blast radius was measured. There are **zero**
consumers of `IDataProtector` / `IDataProtectionProvider` in application code, ABP's string
encryption uses a separate passphrase, and OpenIddict's tokens are protected by its own certificate
rather than this ring. So the complete cost of losing the ring is active logins, outstanding
confirmation and reset links, and antiforgery tokens -- all self-healing or re-issuable. **No durable
data and no PHI depend on these keys.** See 6.4.

So the change converts a **confidentiality** gap into an **availability** dependency. That is
usually the right trade, but only if the certificate is looked after at least as carefully as the
key ring it protects -- backed up, its location known, and its loss recoverable by a planned route
rather than an emergency.

The framework does provide a rotation path: `UnprotectKeysWithAnyCertificate` accepts several
certificates for decryption while `ProtectKeysWithCertificate` sets the one used for new keys. That
covers _planned_ rotation. It does not answer what happens when a certificate is lost unexpectedly,
which is the scenario that needs a deliberate answer.

**That answer is now recorded in 6.4:** a deliberate ring reset is a permitted last resort, invocable
by any admin developer, prevention-first. And the requirement to "retrieve stuff" after a genuine
loss is not achievable at all -- see 6.0 -- so it is met by escrow and multiple registered decryptors
rather than by any recovery route.

---

## 6. Design decisions -- ANSWERED by Adrian, 2026-09-01

All five questions that were recorded as open are now decided. Each is written with the reasoning
and, where one exists, the trade-off that was knowingly accepted, so a later reader can tell a
deliberate choice from an oversight.

### 6.0 The requirements these decisions serve

Adrian's own framing, and what each requirement can and cannot be given:

| Requirement                                                                      | Status                                                                                                                                                                                                                                                                           |
| -------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| "If we refresh the certificate, older data, logins and stuff should not be lost" | **Achievable.** `ProtectKeysWithCertificate` sets the encryptor for new keys; `UnprotectKeysWithAnyCertificate` keeps decrypting old ones. Condition: a retired certificate must be RETAINED while any key it protected is still in the ring.                                    |
| "Continue the application running in case the certificate is lost"               | **Achievable, and cheap** -- see 6.4.                                                                                                                                                                                                                                            |
| "We should be able to retrieve stuff in case the certificate is lost"            | **NOT ACHIEVABLE.** Data encrypted under a lost certificate cannot be recovered. That is what encryption is, not a missing feature. The requirement is therefore met by PREVENTION -- escrowed copies plus more than one registered decryptor -- and not by a recovery route.    |
| "Admin developer able to access and use any certificate to decrypt any data"     | **Partly, and the boundary matters.** This certificate decrypts the key ring, which unprotects cookies and identity tokens. It does NOT decrypt the database: PHI in SQL is not protected by these keys, and encrypted settings use ABP's separate string-encryption passphrase. |
| "Check any movement in the entire app"                                           | **Different subsystem, out of scope here.** That is audit logging -- ABP's audit log and entity-change history, already present and already surfaced in the admin hub. No certificate is involved. Building it into this design would produce the wrong design.                  |

There is a tension inside the fourth row worth naming rather than papering over: the security value
of encrypting the ring comes from NARROWING who can decrypt it, so a standing ability to decrypt is
functionally a standing ability to forge any user's session. That is why 6.3 names the holders and
why retrieval is a recorded act rather than an ambient privilege.

### 6.1 Mechanism: X.509 certificate

Not Azure Key Vault, and not because Key Vault is worse. The Azure-versus-AWS decision has not been
made, and it does not need to be made for this. Verified on nuget.org, 2026-09-01:

- Azure has a genuine first-party encryptor: `Azure.Extensions.AspNetCore.DataProtection.Keys`
  v1.6.4, owned by `azure-sdk` / Microsoft.
- **AWS has no first-party equivalent.** Only community packages
  (`AspNetCore.DataProtection.Aws.Kms`, latest v2.2.0); the AWS-owned
  `Amazon.AspNetCore.DataProtection.SSM` does key PERSISTENCE, not key encryption.

So the certificate is the only route that does not require settling the cloud question first, and it
migrates cleanly to Key Vault later if the answer turns out to be Azure. Committing to Key Vault now
and then choosing AWS would be the worst of the three outcomes.

### 6.2 Custody: extend the pattern that already exists

A `.pfx` on the host, bind-mounted read-only into BOTH containers, passphrase supplied by an
environment variable, validated at start-up by the existing `HostingConfigValidator`. This is not a
new pattern: it is exactly how `openiddict.pfx` already reaches the AuthServer in production.

**The gap to close:** the api container has NO certificate mount today. Only `authserver` receives
one, inside the `authserver:` service block of `docker-compose.prod.yml`. Data Protection needs the
certificate in BOTH processes, so this requires a new mount, not just a configuration line.

Accepted consequence: the certificate lives on the host filesystem, so host backups and host access
control become part of the security boundary. The runbook has to say that rather than imply it.

### 6.3 Who holds it: three people

Adrian, **Levon** (the other developer, and the person to whom this work is being handed off), and
the IT lead. The backup holders do not need to USE the certificate -- only to produce a sealed copy
if Adrian is unavailable.

Accepted trade-off, stated plainly because it is the cost of the choice: three people who can
retrieve the certificate are three people who can mint a cookie for any user, so the audit question
"who could have done this?" has three answers rather than one. That was weighed against the
single-point-of-failure risk of a sole holder, and availability won.

**Because Levon is the handoff target, the runbook must be written for someone who has never worked
on this system.** That is a requirement, not a nicety: a recovery procedure only its author can
follow is not a recovery procedure.

### 6.4 The override: a deliberate reset is permitted, and any admin developer may invoke it

There is no framework-level emergency override for a lost key, so the only real override is to
accept the loss and carry on. What makes that acceptable rather than desperate is the blast radius.

**What a reset actually costs.** Verified 2026-09-01: there are **zero** consumers of
`IDataProtector` / `IDataProtectionProvider` anywhere in application code, ABP's string encryption is
a separate passphrase mechanism, and OpenIddict's tokens are protected by its own certificate via
`AddProductionEncryptionAndSigningCertificate`. So discarding the ring costs active logins,
outstanding confirmation and password-reset links, and antiforgery tokens. Every one of those is
self-healing or re-issuable. **Nothing durable is lost and no PHI is touched.**

Procedure: discard the key ring, restart both containers, users sign in again, outstanding
confirmation and reset links are re-issued.

Two accepted trade-offs:

1. **It is externally visible.** Patients' outstanding confirmation links stop working and have to be
   re-sent. Cheap internally is not the same as invisible outside the company.
2. **A cheap reset can become the default response.** The risk is that someone resets the ring
   instead of noticing the certificate is merely missing and restoring it from escrow. The runbook
   must order the steps prevention-first: confirm the certificate is genuinely unrecoverable BEFORE
   resetting, and record who reset it and why.

### 6.5 Scope: TWO certificates, ONE shared custody design

One set of rules -- storage, backup, holders, rotation, recovery -- covering both the key-ring
certificate and the OpenIddict signing certificate. But two separate certificates, not one doing
both jobs.

Reasoning: the two have different rotation TRIGGERS and different blast radii. Rotating the signing
certificate invalidates issued tokens and forces re-authentication; rotating the key-ring certificate
does not disturb users at all, as section 4 measures. Sharing one certificate welds those together,
so a rotation performed for a token-forgery concern would sign out every user as an unavoidable side
effect -- and, worse, one would hesitate to rotate at exactly the moment it was most needed.

This also answers the standing gap in `SECRETS-MANAGEMENT.md` ("No secret rotation runbook"): this
design is where that runbook comes from, for both certificates.

### 6.6 Sequencing: key-ring encryption first, signing-certificate rotation second

Two changes, in order, not one.

The key-ring change now has measured evidence of zero user impact. Rotating the signing certificate
invalidates issued tokens and does force re-authentication. Shipping them together would merge a
change that cannot disturb anyone with one that certainly will, making any failure impossible to
attribute and forcing the safe change to inherit the risky one's maintenance window and user
communications.

Doing the key-ring change first also exercises the whole custody design -- generation, mounting,
backup, holders, runbook -- on the change with no blast radius, so any flaw in it surfaces where it
is cheap to fix.

**Stated plainly rather than glossed:** this leaves the existing High-severity signing-certificate
rotation (SEC-01, password in git history) open for longer. That is a deliberate ordering decision,
not an oversight.

### 6.7 Implementation facts learned by actually doing it

Found during the 2026-09-01 spike. Both would otherwise be discovered the hard way:

1. **`new X509Certificate2(...)` will not compile here.** That constructor is obsolete in .NET 9+
   (`SYSLIB0057`) and this repository builds with `TreatWarningsAsErrors`, so the obsolete warning is
   a build error. Use
   `System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(path, password)`.
2. **The api container has no certificate mount.** Only `authserver` receives `openiddict.pfx`. The
   change therefore touches `docker-compose.prod.yml` (a new read-only mount plus a passphrase
   environment variable on the api service) as well as the two module files -- so the "two-line
   change" framing in section 3 is true of the C# only.

Also worth recording for whoever runs the local stack: the dev compose publishes Redis on the host
(`127.0.0.1:${REDIS_HOST_PORT:-6379}`), which collides with the MRR AI stack's Redis. Set
`REDIS_HOST_PORT` to something free rather than stopping the other project's containers.

### 6.8 Certificate expiry does NOT break the key ring -- measured 2026-09-01

This was the one unknown that could have undermined the whole design, because if expiry stopped
decryption then certificate validity would be a hard deadline after which every session dies. It was
settled before any implementation work, rather than assumed either way.

**Method.** A certificate with `notBefore` 2023-01-01 and `notAfter` 2024-01-01 -- expired for over
twenty months -- was configured as the encryptor in BOTH modules, pointed at a SEPARATE Redis ring
name so the real ring was never touched. Both processes shared that spike ring.

**Results.**

| Observation                          | Result                                                                                                                                                                                                                                              |
| ------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Does an expired certificate encrypt? | **Yes.** The new key was written with the full XML-Encryption structure (`<EncryptedData>`, `<EncryptedKey>`, `<CipherValue>`, `<X509Certificate>`), 3333 bytes against 888 for a plaintext key, and the plaintext `<masterKey>` element was absent |
| Does an expired certificate decrypt? | **Yes.** The spike ring stayed at length 1 across both processes -- the second read the first's encrypted key rather than minting a replacement, which is what it would have done had decryption failed                                             |
| End-to-end protect and unprotect     | **Yes.** A full login succeeded and authenticated under the expired certificate: 200 with the session cookie, 302 without it                                                                                                                        |
| Real ring throughout                 | Byte-identical; the spike ring was deleted afterwards                                                                                                                                                                                               |

**Conclusion.** Data Protection uses the certificate as a key pair and does not enforce its validity
dates. So the validity period can be chosen for operational hygiene -- to force a rotation cadence --
rather than as a cliff edge that would sign everyone out.

**Do not over-read this.** It means expiry is not a data-loss event. It does not mean expiry is
irrelevant: an expired certificate is still a governance and audit finding, and this was tested with
a self-signed certificate where no chain or revocation checking is involved. A CA-issued certificate
under a policy that checks revocation may not behave the same way. Rotation still needs scheduling;
it just is not an emergency.

**Bonus observation, which makes 6.4 measured rather than reasoned.** Pointing the application at a
different ring is functionally the reset procedure, and it behaved exactly as 6.4 predicts: the
pre-existing session returned 302 (signed out), while both processes stayed healthy, login worked,
and nothing else broke. The reset really does cost only re-logins.

---

## 7. Ordering constraint -- read before moving the key store

Recorded during the hardening epic as **REQ-APP-01**, and it applies to any work in this area:

> If the key store is ever moved out of Redis, that move must **precede** any change to the cache
> eviction policy. Otherwise the keys are destroyed weeks before anyone notices.

The failure mode is quiet: an eviction policy that discards the key ring does not error, it just
means that at some later point sessions and tokens stop validating with no obvious cause.

---

## 8. Re-checking this document

Every claim above can be verified without redoing the investigation:

```bash
# The two configuration methods
sed -n '1103,1131p' src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs
sed -n '382,401p'   src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs

# No key protection configured anywhere
grep -rn "ProtectKeysWith\|UnprotectKeysWithAnyCertificate\|XmlEncryptor" --include=*.cs src/

# Redis posture in production
sed -n "$(grep -n '^  redis:' docker-compose.prod.yml | cut -d: -f1),+18p" docker-compose.prod.yml

# The blast-radius claim in 5 and 6.4: nothing in application code uses these keys.
# Expect ZERO matches. If this ever returns a hit, 6.4 must be re-derived, because
# something durable would then depend on the ring.
grep -rn "IDataProtector\|IDataProtectionProvider\|CreateProtector" --include=*.cs src/

# OpenIddict uses its OWN certificate, not this key ring -- which is why rotating the
# signing certificate and rotating the key-ring certificate have different blast radii (6.5).
grep -rn "AddProductionEncryptionAndSigningCertificate" --include=*.cs src/

# Only the authserver gets a certificate mount today (6.2, 6.7). The api service has none.
grep -n "pfx" docker-compose.prod.yml
```

To re-run the section 4 measurement, the shape was: capture the ring
(`redis-cli LRANGE CaseEvaluation-Protection-Keys 0 -1`), add `ProtectKeysWithCertificate` to both
modules against a throwaway self-signed `.pfx` mounted at a path both containers can see, restart
both, then confirm the ring is byte-identical and an authenticated request still returns 200 while
the same request without the cookie returns 302. **Keep the no-cookie control**: two earlier test
designs passed against a deliberately corrupted cookie and were worthless (4.2).

Microsoft references:

- Key storage providers in ASP.NET Core -- the deregistration warning
- Key encryption at rest in Windows and Azure using ASP.NET Core -- the mechanism list
- Data Protection key management and lifetime in ASP.NET Core -- the same warning, and key states
- Configure ASP.NET Core Data Protection -- `ProtectKeysWith*` and `UnprotectKeysWithAnyCertificate`

Related in this repository: `docs/security/SECRETS-MANAGEMENT.md`,
`docs/security/SESSION-AND-TOKENS.md`.
