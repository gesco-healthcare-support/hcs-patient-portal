[Home](../INDEX.md) > [Issues](./) > Questions for Previous Developer

# Questions for the Previous Developer

These questions represent knowledge that **exists only in the previous developer's memory** (or in private communications, signed agreements, or external accounts we have no access to). Unlike code-level ambiguities that can be resolved by examining the codebase or researching industry standards, the answers to these questions **cannot be reconstructed from any artifact in this repository**.

> For code-level unknowns that are resolvable through codebase analysis or industry research, see [Technical Open Questions](TECHNICAL-OPEN-QUESTIONS.md).

**If contact with the previous developer is lost, these questions cannot be answered.** Prioritise obtaining answers before that window closes.

---

## How to Use This Document

Each question is accompanied by:
- **Why this is irreplaceable**: What makes this impossible to recover without the developer
- **Consequence of not knowing**: What we would be forced to assume, and the risk if that assumption is wrong
- **Suggested action**: The fastest way to get a definitive answer

Questions are ordered from highest-risk (legal/compliance exposure) to operational risk.

---

## P1: Is there an active client engagement, contract, or Statement of Work?

**Why this is irreplaceable**: If this system was built under a contract or SOW, there are legally binding obligations -- feature commitments, delivery dates, ownership of IP, liability clauses -- that exist entirely outside this repository. No contract document was found in the codebase.

**Consequence of not knowing**: We may inadvertently breach a contract by changing scope, reprioritising features, or delaying delivery. Alternatively, we may invest effort on features that were already contractually descoped, or miss a delivery deadline we didn't know existed.

**Suggested action**: Ask directly: "Is there a signed contract, SOW, or engagement letter with a client for this system? If yes, can you share the document?"

---

## P2: Has this application ever been deployed to a production or staging environment with real patient data?

**Why this is irreplaceable**: If real Protected Health Information (PHI) -- patient names, dates of injury, claim numbers, employer details -- was ever loaded into any environment, HIPAA's Breach Notification Rule may apply to how we handle the transition. There is no production deployment configuration in the repository, but that does not mean one was never created.

**Consequence of not knowing**: If real patient data exists in an environment we don't know about, it is currently unsecured and unmonitored. If a breach occurred before or during the handover, notification obligations may already be running.

**Suggested action**: Ask directly: "Was this ever deployed outside of localhost -- on any server, cloud instance, or client's infrastructure? Were any real patients, doctors, or case records ever entered?"

---

## P3: Were any HIPAA compliance decisions made, and was legal counsel involved?

**Why this is irreplaceable**: A workers' compensation IME platform that handles patient health information is likely subject to HIPAA. Any formal compliance decisions -- whether a Business Associate Agreement (BAA) was required, which safeguards were deemed sufficient, whether the system was classified as a covered entity or business associate -- were made by people, not written into code.

**Consequence of not knowing**: We cannot determine our current HIPAA posture. We may be operating under a compliance framework that was negotiated and is now invisible to us, or we may have no framework at all and be exposed. HIPAA violations carry civil and criminal penalties.

**Specific sub-questions**:
1. Was a HIPAA attorney or compliance consultant engaged?
2. Were Business Associate Agreements (BAAs) signed with any cloud providers, third-party services, or the client?
3. Was a formal risk assessment conducted under the HIPAA Security Rule?
4. Were any technical safeguards (audit controls, encryption standards) selected based on a compliance review rather than developer preference?

**Suggested action**: Ask directly: "Did anyone review this system for HIPAA compliance? Were any BAAs signed? Is there a compliance report or attorney opinion anywhere?"

---

## P4: Who is the actual end client, and what is their contact information?

**Why this is irreplaceable**: The name "Healthcare Support" appears in the solution name, but the identity of the organisation that commissioned this system, their business contact, and their technical contact cannot be determined from the code. If we need to make product decisions, understand the user base, or communicate about the handover, we need to know who to call.

**Consequence of not knowing**: We may make product decisions based on incorrect assumptions about who the end users are (e.g., assuming a national carrier when this is a single regional TPA). We have no one to notify if a security issue is discovered.

**Suggested action**: Ask directly: "Who is the client -- the business name, the main contact person, and their email/phone? Is there a product owner on the client side we should be communicating with?"

---

## P5: Are there any external service accounts we need to take ownership of?

**Why this is irreplaceable**: Running a web application typically requires accounts with third-party services that are not reflected in the code: SMTP relay (SendGrid, Mailgun, SES), error monitoring (Sentry, Application Insights), cloud hosting, domain registrar, SSL certificate authority, CDN. These accounts are under the previous developer's email and will become inaccessible if that relationship ends.

**Consequence of not knowing**: Critical services can silently stop working (emails stop sending, certificates expire, monitoring goes dark) without us receiving any notification, because all notifications go to an email address we don't control.

**Specific sub-questions**:
1. What SMTP service is used for transactional email (appointment confirmations, password resets)? Who owns that account?
2. Is there an error monitoring or APM tool configured? (Sentry, New Relic, Datadog, Application Insights?)
3. Was the domain name for this application registered anywhere? Who owns that registration?
4. Is there an SSL/TLS certificate provisioned under anyone's account?
5. Is there a cloud account (Azure, AWS, GCP) that was used for any environment, even temporary?

**Suggested action**: Ask directly for a list of every third-party service account created for this project, with account email and access transfer instructions.

---

## P6: Were any features, behaviours, or constraints communicated verbally to the client that are not in the code?

**Why this is irreplaceable**: Software projects routinely involve verbal commitments made in demos, calls, or emails that are never written into specifications or tickets. These commitments shape client expectations. The previous developer is the only person who knows what was promised.

**Consequence of not knowing**: We may build something the client explicitly said they didn't want, or fail to build something they were told to expect. In a healthcare context, an undocumented verbal commitment about data retention, access control, or audit trails could be legally significant.

**Specific sub-questions**:
1. Were any features demoed to the client that are not yet implemented in the codebase? (Promises made about a roadmap?)
2. Were any specific behaviours promised that differ from standard IME scheduling practice -- for example, a custom cancellation policy, a specific billing code integration, or a particular report format?
3. Were any features explicitly ruled out in conversation -- things the client said they definitely did not want?
4. Is there a design document, wireframe set, or Figma file that represents the intended final state of the UI that was shared with the client?

**Suggested action**: Ask for any email threads, meeting notes, Slack/Teams channels, or design files that contain client-facing product discussions.

---

## P7: Why was this project handed over, and what is the nature of the handover?

**Why this is irreplaceable**: The circumstances of the handover -- contract completion, scope change, relationship breakdown, developer capacity -- affect how we interpret the state of the codebase. An intentional handover after completed work is different from an abrupt exit. This context affects how we handle client communication, what we assume is "done enough" versus abandoned, and how we handle the transition.

**Consequence of not knowing**: We may treat abandoned work as complete, or spend time completing features that were explicitly descoped.

**Specific sub-questions**:
1. Was this handover planned, or was it abrupt?
2. Are there features the developer considers "done" that were never tested by the client?
3. Are there known issues the developer chose not to raise with the client?
4. Is the developer bound by any non-disclosure or non-compete agreement that limits what they can tell us?

**Suggested action**: Have a direct conversation about the handover context before asking technical questions.

---

## P8: Were any real users (doctors, administrators, patients, adjusters) involved in design or testing?

**Why this is irreplaceable**: If actual end users participated in requirements gathering, user testing, or feedback sessions, their input shaped product decisions that are invisible in the code. No user research artefacts were found in the repository -- but they may exist elsewhere (Notion, Confluence, Google Docs, the developer's laptop).

**Consequence of not knowing**: We may "fix" UI or workflow decisions that were deliberately shaped by user research, inadvertently making the product worse for its intended audience.

**Specific sub-questions**:
1. Were any actual doctors, claim adjusters, or attorneys shown the system? What was their feedback?
2. Are there user personas, journey maps, or UX research notes anywhere?
3. Were there any usability problems raised by early users that were not resolved?
4. Is there a test account or demo dataset that was shown to the client, separate from the seeded data?

**Suggested action**: Ask whether any user research or stakeholder feedback exists, and request any documents or recordings.

---

## P9: Were discussions held about integration with California DWC systems or insurance carrier systems?

**Why this is irreplaceable**: Workers' compensation IME scheduling in California operates within a regulated ecosystem -- the Division of Workers' Compensation (DWC), insurance carriers, third-party administrators, and utilisation review organisations. If the previous developer had conversations about integrating with any of these systems, those discussions represent product direction that is invisible in the codebase.

**Consequence of not knowing**: We may build a self-contained system when the client expected it to exchange data with a carrier's system. Or we may attempt integrations that were already investigated and ruled out.

**Specific sub-questions**:
1. Were discussions held about importing or exporting to any external system? (Insurance carrier portals, WCAB case management systems, practice management software?)
2. Were any API credentials or sandbox access tokens obtained for third-party systems?
3. Did the client mention specific software their staff already uses that this system should connect to?
4. Was EDI or HL7/FHIR ever discussed for medical record exchange?

**Suggested action**: Ask specifically: "Were any external system integrations discussed with the client or investigated, even if they were never started?"

---

## P10: Are there security vulnerabilities or incidents that were discovered but not disclosed?

**Why this is irreplaceable**: A developer who found a security issue and did not fix it has information that cannot be recovered from code analysis alone. Our audit found committed secrets (SEC-01), PII logging (SEC-02), and open authorization gaps (SEC-03) -- but we cannot know whether these were known, whether they were exploited, or whether additional vulnerabilities were found and silently left.

**Consequence of not knowing**: If there was a prior intrusion or data exposure, we may be inheriting a compromised system. If vulnerabilities were found and suppressed, we need to know their scope before we can give any security assurance to a client.

**Specific sub-questions**:
1. Were any security reviews or penetration tests conducted?
2. Were any of the issues identified in our audit (committed secrets, PII logging, open endpoints) known to the developer? Were they disclosed to the client?
3. Has any unauthorised access to the system ever been detected or suspected?
4. Were the API keys committed to source control (ABP NuGet key, any OAuth secrets) ever rotated or revoked?

**Suggested action**: Ask directly and without judgement: "Were there any security concerns about this system that you're aware of that we should know about?"

---

## Summary

| # | Question | Risk if Unanswered |
|---|----------|--------------------|
| P1 | Active contract / SOW | Legal: breach of contract, IP ownership dispute |
| P2 | Real patient data in any environment | Legal: HIPAA breach notification obligations |
| P3 | HIPAA compliance decisions and legal counsel | Legal: HIPAA civil/criminal exposure |
| P4 | Client identity and contact | Operational: no stakeholder to align with |
| P5 | Third-party service accounts | Operational: services expire/fail silently |
| P6 | Verbal commitments to client | Product: building the wrong thing |
| P7 | Why and how the handover occurred | Context: misreading what is "done" vs abandoned |
| P8 | User research and stakeholder feedback | Product: undoing deliberate design decisions |
| P9 | Integration discussions with external systems | Product: self-contained vs connected architecture |
| P10 | Known security vulnerabilities or prior incidents | Legal + Security: inheriting a compromised system |

---

> **Recommended approach**: Request a one-hour structured handover call with the previous developer before raising any code-level questions. The questions above require narrative answers, not code references. Record the call if permitted.
>
> For code-level unknowns that can be resolved through codebase analysis, see [Technical Open Questions](TECHNICAL-OPEN-QUESTIONS.md).
