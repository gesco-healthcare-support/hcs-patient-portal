# Session key encryption at rest

**Status:** finding recorded, no fix applied. Design work not yet started.
**Raised:** 2026-09-01, during the production-hardening epic (phase 1 item 1.6).
**Moved out of that epic by Adrian's decision on 2026-09-01**, to be designed and handled separately
on `main`. The epic's phase-1 file now points here.

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

Relevant precedent already in this system: `docs/security/SECRETS-MANAGEMENT.md` records an existing
OpenIddict signing certificate whose password lives in configuration, and an outstanding rotation
task for it. Whatever is designed here should probably answer for both certificates rather than
creating a second, separate lifecycle.

---

## 4. Would turning it on break existing sessions?

**Best current answer: no.** Enabling a `ProtectKeysWith*` mechanism should not invalidate the keys
already in the ring, should not sign anyone out, and should not break outstanding confirmation
links. New keys would be written encrypted; existing plaintext keys stay readable.

**Confidence: medium-high, not certain.** This is assembled from several documented behaviours
rather than a single statement, and it is labelled that way deliberately:

- "Created, active, and expired keys may all be used to unprotect incoming payloads."
- Keys are stored individually as XML, each with its own descriptor; an encrypted key carries a
  reference to the decryptor that opens it. A ring can therefore hold keys in different states.
- `UnprotectKeysWithAnyCertificate` exists specifically to "configure certificates which can be used
  to decrypt keys loaded from storage", for certificate rotation. It only makes sense if a ring can
  legitimately contain keys protected under different settings.
- On default-key selection: "new keys may have been configured with different algorithms or
  **encryption-at-rest mechanisms** than old keys, and the system should prefer the current
  configuration" -- a mechanism change rolls forward; it does not invalidate what came before.

**What would settle it:** enable the mechanism against a copy of a real key ring and confirm an
existing session survives. That needs a running stack and was not done. Until it is, treat "no
disruption" as expected rather than proven, and plan the rollout accordingly.

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

So the change converts a **confidentiality** gap into an **availability** dependency. That is
usually the right trade, but only if the certificate is looked after at least as carefully as the
key ring it protects -- backed up, its location known, and its loss recoverable by a planned route
rather than an emergency.

The framework does provide a rotation path: `UnprotectKeysWithAnyCertificate` accepts several
certificates for decryption while `ProtectKeysWithCertificate` sets the one used for new keys. That
covers _planned_ rotation. It does not answer what happens when a certificate is lost unexpectedly,
which is the scenario that needs a deliberate answer.

---

## 6. Open design questions

These are recorded as open on purpose. They are the questions Adrian asked to think through, and
nothing here proposes an answer to them.

1. **How should it work?** Which mechanism, and why that one for this deployment.
2. **How is access to the certificate designed?** Where it lives, how both containers obtain it at
   start-up, and how it is kept out of version control and images.
3. **Who holds it?** Which people or systems can retrieve it, and how that is controlled and
   reviewed.
4. **How does the override work?** What the recovery route is when the certificate is lost, changed
   or compromised -- including whether an intentional, controlled "we accept losing every existing
   session" reset is an acceptable last resort, and who is allowed to invoke it.
5. **Does it cover the existing signing certificate too?** See `SECRETS-MANAGEMENT.md`; there is
   already one certificate with an unresolved lifecycle.

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
```

Microsoft references:

- Key storage providers in ASP.NET Core -- the deregistration warning
- Key encryption at rest in Windows and Azure using ASP.NET Core -- the mechanism list
- Data Protection key management and lifetime in ASP.NET Core -- the same warning, and key states
- Configure ASP.NET Core Data Protection -- `ProtectKeysWith*` and `UnprotectKeysWithAnyCertificate`

Related in this repository: `docs/security/SECRETS-MANAGEMENT.md`,
`docs/security/SESSION-AND-TOKENS.md`.
