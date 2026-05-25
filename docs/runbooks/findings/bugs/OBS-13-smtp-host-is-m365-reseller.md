---
id: OBS-13
title: SMTP host is SiteGround shared hosting fronted by SpamExperts, not a Microsoft 365 reseller (corrects original finding)
severity: observation
status: documented
found: 2026-05-14
corrected: 2026-05-22
flow: notification-emails
---

# OBS-13 — `securemailprotocol.com` is SiteGround shared hosting (not M365)

> **Correction 2026-05-22 (confidence 97%).** The original analysis below misidentified the provider. Live DNS + SMTP probe shows `securemailprotocol.com` is **SiteGround shared web hosting fronted by SpamExperts (N-able Mail Assure)**, not a Microsoft 365 reseller. The downstream conclusions about M365 SMTP AUTH limits (30/min, 10K/day, 3 concurrent connections) do not apply to us. The cited rejection error came from a *recipient* M365 mailbox throttling our sender reputation, not from the sender host. Migration recommendation is upgraded from "long-term consideration" to "do this before scale-up." Original analysis preserved below for audit trail.

## Corrected analysis (2026-05-22)

### Live evidence: what `securemailprotocol.com` actually is

**MX records** point to SpamExperts (N-able Mail Assure), a generic outbound anti-spam frontend used by many shared hosts:

```
mx10.antispam.mailspamprotection.com   (preference 10)  -> 34.149.79.66    (Google Cloud)
mx20.antispam.mailspamprotection.com   (preference 20)  -> 34.120.156.61   (Google Cloud)
mx30.antispam.mailspamprotection.com   (preference 30)  -> 34.111.121.216  (Google Cloud)
```

Microsoft 365 MX records always point to `*.mail.protection.outlook.com`. These don't.

**SMTP banner** on `mail.securemailprotocol.com:587`:

```
220-gcam1225.siteground.biz ESMTP #2
```

`siteground.biz` = **SiteGround**, a shared web-hosting + email-hosting provider. M365 submission banners would identify as `outlook.com` or `protection.outlook.com`.

**SPF record:**

```
v=spf1 +a +mx include:securemailprotocol.com.spf.auto.dnssmarthost.net ~all
```

References `dnssmarthost.net` (an outbound-relay anti-spam service). M365 SPF would include `spf.protection.outlook.com`.

**Microsoft partner registry:** searching Microsoft's CSP / reseller listings for "securemailprotocol.com" returns nothing. The domain is not a recognized Microsoft 365 CSP.

### Why the original analysis was misled

The original rejection error this doc cited:

```
4.5.127 Message rejected. Excessive message rate from sender.
For more information see https://aka.ms/EXOSmtpErrors.
```

That `aka.ms/EXOSmtpErrors` URL is in the error because the **recipient mailbox** runs on Microsoft 365, not the sender host. SMTP code `4.5.127` is M365's recipient-side sender-reputation throttle: when we send to an M365-hosted recipient, M365 evaluates our sender reputation and may reject with `4.5.127`, embedding the EXO docs link in the response. The link says "the recipient is M365 and rejected you," not "the sender is M365."

So the original conclusion ("the sender host is a M365 reseller") was a misread of where the rejection came from. We would see the same `aka.ms` link sending from *any* SMTP host to an M365-hosted recipient.

### Actual provider chain and real limits

```
Our app (HttpApi.Host / AuthServer)
  -> mail.securemailprotocol.com:587      (SiteGround shared host, submission port)
  -> antispam.mailspamprotection.com      (SpamExperts outbound filter)
  -> internet
  -> recipient MX (varies; many recipients are M365 mailboxes)
```

| Layer | Limit profile |
|---|---|
| **Sender side (SiteGround shared hosting)** | SiteGround's published shared-hosting TOS allows roughly **100-300 outbound emails per hour, per account**. Shared web hosting is not designed for transactional volume; this is *lower* than M365 SMTP AUTH's per-mailbox cap, not higher. |
| **Outbound filter (SpamExperts)** | Adds its own per-account throttling tuned for shared-host customers. |
| **Recipient side (M365 mailboxes)** | M365 enforces sender-reputation throttling on inbound. The `4.5.127` rejection comes from here. Independent of our outbound host. |

### Implications

1. **Original limit numbers in this doc do not apply.** The 30/min, 10K/day, 3 concurrent connections are M365 SMTP AUTH limits ([Microsoft Learn: SMTP submission limits](https://learn.microsoft.com/en-us/troubleshoot/exchange/send-emails/smtp-submission-improvements)) -- they apply to *M365 SMTP AUTH customers*, which we are not. Our actual sender-side limits are SiteGround's, typically lower.
2. **Recipient-side throttling still bites.** When we send to M365-hosted recipients we still trigger `4.5.127` rejections based on sender reputation. The [[BUG-018]] throttle implementation work mitigates this and remains valid.
3. **The migration recommendation is stronger, not weaker.** SiteGround shared hosting is the wrong tool for transactional email volume. Real transactional providers (SendGrid, Mailgun, Postmark, AWS SES) maintain dedicated sender-reputation IP pools tuned for transactional deliverability and have predictable rate limits documented in their pricing tiers. This should be a near-term plan, not a long-term consideration.
4. **Even M365 SMTP AUTH has a deadline.** Per Microsoft, [Basic Auth for SMTP Client Submission is being removed in March 2026](https://learn.microsoft.com/en-us/exchange/clients-and-mobile-in-exchange-online/deprecation-of-basic-authentication-exchange-online). So even if we ever did migrate to direct M365 SMTP, there's a near-term clock on it.

### Recommended operational responses (corrected)

1. **Near-term:** keep the [[BUG-018]] throttle work going -- still relevant for recipient-side throttling regardless of our outbound host.
2. **Before Phase 1A scales:** migrate transactional email off `securemailprotocol.com` (SiteGround) to a transactional provider. Top candidates with established deliverability:
   - **AWS SES** -- cheapest at scale, requires us to manage IP warmup and DMARC alignment.
   - **SendGrid / Twilio** -- managed reputation, more expensive per email, less ops burden.
   - **Postmark** -- transactional-only, no marketing path; strongest for our shape (account / appointment / notification emails).
3. **Avoid:** routing through any other shared web host. Same class of provider, same outcome.

---

## Original analysis (preserved for audit trail, superseded by 2026-05-22 correction above)

### Symptom

The app's `appsettings.secrets.json` points `Abp.Mailing.Smtp.Host` at `mail.securemailprotocol.com:587`. Despite the branding suggesting a self-contained "secure mail" provider, the SMTP rejection error from this host explicitly references Microsoft's documentation:

```
4.5.127 Message rejected. Excessive message rate from sender.
For more information see https://aka.ms/EXOSmtpErrors.
```

`aka.ms/EXOSmtpErrors` is Microsoft's own URL for Exchange Online SMTP error codes — a genuinely independent SMTP server (Postfix, Exim, SendGrid, Mailgun, Mailpit, etc.) would never reference `aka.ms`. So `securemailprotocol.com` is a reseller / wrapper on top of Microsoft 365 Exchange Online.

> **Correction:** the `aka.ms` link comes from the recipient-side M365 mailbox, not from the sender host. See corrected analysis above.

### Implications

The `patientportal@securemailprotocol.com` mailbox is subject to M365 SMTP AUTH per-mailbox limits:

- 30 messages/minute rolling cap
- 10,000 recipients/day ceiling
- Stricter burst protection that rejects ~7 emails sent in <3 seconds (the symptom we hit at 20:17)

> **Correction:** these are M365 SMTP AUTH customer limits and do not apply to us; we're a SiteGround customer with different (likely lower) sender-side limits. The burst rejection observed at 20:17 was recipient-side, not sender-side.

### Operational impact

This is **not** a problem with our code — it's a deliberate Microsoft throttle on shared infrastructure. Two responses are appropriate:

1. **Throttle proactively in the app** (Adrian's choice 2026-05-14; see [[BUG-018]] for the implementation spec).
2. **Long-term consideration**: move transactional email off `securemailprotocol.com` to a true transactional-email service (SendGrid / Mailgun / Postmark / Amazon SES) once Phase 1A wraps. Those services are designed for higher transactional throughput and have no surprise burst protection.

> **Correction:** still not a code problem, but the throttle is on the sender (SiteGround) + recipient (M365 reputation) sides, not on a single deliberate Microsoft throttle. Migration recommendation upgraded from "long-term" to "near-term."

## Related

- [[BUG-018]] — implementation ticket: throttle to 2-3 emails/sec + retry on rate-limit + relabel ACS-era log strings
- [[BUG-010]] — synthetic-user SMTP delivery (separate; `.test` domains never reach a valid MX)
