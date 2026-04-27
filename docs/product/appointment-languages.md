[Home](../INDEX.md) > [Product Intent](./) > Appointment Languages

# Appointment Languages -- Intended Behavior

**Status:** draft -- Phase 2 T8, lookup cluster
**Last updated:** 2026-04-24
**Primary stakeholder:** Host admin (global list) + tenant admin (per-tenant hide/show) + the doctor's office / booker for translator coordination [Source: Adrian-confirmed 2026-04-24]

> Captures INTENDED behaviour for the `AppointmentLanguage` lookup entity. This is NOT a storage-only preference field -- a non-English language on the patient record is a workflow trigger that obligates the portal to schedule a translator and notify all parties. Every claim source-tagged.

## Purpose

`AppointmentLanguage` records the patient's primary language. The list is a common global catalogue (maintained by Gesco's host admin), with per-tenant hide/show customisation so each practice can show only the languages it actually supports. The field is load-bearing: a non-English value triggers a legally-required translator arrangement and an all-parties notification so a translator can be scheduled. [Source: Adrian-confirmed 2026-04-24, Q-A + Q-A2 + Q-K]

## Personas and goals

Cross-reference `00-BUSINESS-CONTEXT.md` for persona definitions.

- **Host admin (Gesco).** Manages the global language list (English, Spanish, Mandarin, Vietnamese, Korean, Cantonese, Tagalog, and whatever the client base requires). [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION]
- **Tenant admin (doctor's admin).** Hides languages the practice does not support from its own users' dropdowns. Cannot add languages. [Source: Adrian-confirmed 2026-04-24, Q-A2]
- **Patient / booker.** Picks the patient's primary language on the booking form. A non-English choice automatically signals "translator required" (patient is not asked to tick a separate translator-needed checkbox). [Source: Adrian-confirmed 2026-04-24, Q-K -- "The language field is so that there can be a translator made available"]
- **Doctor's office staff.** Reads the patient's language on the appointment; when non-English, must see the translator-required signal and coordinate scheduling with a translator.
- **All case parties.** Receive notification when a translator is required so they can plan for translator scheduling and attendance at the appointment. [Source: Adrian-confirmed 2026-04-24, Q-K]

## Intended workflow

### At install

Host admin seeds the initial language list manually (no automatic `DataSeedContributor`). [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION]

### Per-tenant hide/show

Tenant admin hides languages not supported by that practice from its dropdowns. [Source: Adrian-confirmed 2026-04-24, Q-A2]

### On booking

Patient (or booker) picks the patient's primary language. English is a valid value; any other language triggers the translator obligation. [Source: Adrian-confirmed 2026-04-24, Q-K]

### Translator obligation (non-English)

When the patient's language is recorded as anything other than English, Gesco is legally required to arrange a translator. The portal's notifications to case parties must surface the translator requirement so everyone can plan (attorneys, doctor's office, claim examiner, insurance / adjustor). [Source: Adrian-confirmed 2026-04-24, Q-K]

### English exclusion

A patient whose primary language is English (or who enters English in the field) CANNOT request a translator -- Gesco does not provide English-to-English translation. [Source: Adrian-confirmed 2026-04-24, Q-K]

## Business rules and invariants

- **Common global base.** All tenants share one master language list; only Gesco's host admin edits it. [Source: Adrian-confirmed 2026-04-24, Q-A]
- **Per-tenant hide/show.** Each tenant hides languages it does not support. Cannot add languages. [Source: Adrian-confirmed 2026-04-24, Q-A2]
- **Translator trigger rule.** Patient language != English -> translator required. Derivation is automatic on the appointment; no separate "translator needed" checkbox on the form. [Source: Adrian-confirmed 2026-04-24, Q-K]
- **English exclusion rule.** Patient language = English -> no translator is provided. Explicit legal rule: Gesco cannot provide English-to-English translation. [Source: Adrian-confirmed 2026-04-24, Q-K]
- **All-parties notification on translator-required.** When translator is required, every case party must be notified so they can plan accordingly. [Source: Adrian-confirmed 2026-04-24, Q-K]
- **Language is required on the Patient record.** Because the field drives legal-compliance logic, a patient cannot be saved without an explicit language (no silent null). Default at MVP: English auto-selected on the patient form, which the patient can change. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION; derived from Q-K's "we have to legally provide translators for whatever language is required" -- if null was allowed, the trigger would be undefined]

## Integration points

- **Patient record.** `Patient.AppointmentLanguageId` (SetNull FK) -- stores the patient's primary language.
- **Appointment notifications (future, FEAT-05).** When translator is required, every party receives a notification that surfaces the language + translator requirement.
- **Downstream (doctor's office).** The patient's language is part of the data handed off to the doctor's office (the Packet that pre-fills the intake form) so the office can plan its translator arrangements.
- **Legal compliance.** California workers'-comp rules on translator provision drive this workflow. The exact rule reference is [UNKNOWN -- queued for legal/compliance manager as part of the broader Q7 legal-rules question].

## Edge cases and error behaviors

- **Patient with no recorded language.** Invalid at MVP; the form should not allow save without a language. [Source: Adrian best-guess -- NEEDS CONFIRMATION]
- **Patient changes language post-submit.** Per the T7 universal post-submit lock rule, the language is locked at request-submit; changes require a Gesco-side admin running the proper process. Changing language post-submit changes the translator requirement -- if it flips English to non-English or vice versa, notifications have to re-fire. [Source: inferred from T7 + Q-K]
- **Host admin deletes a language that patients use.** Current FK is SetNull -- patients lose their language and the translator-trigger logic breaks. Intent: block deletion when in use; at minimum add a warning. [Source: Adrian best-guess -- NEEDS CONFIRMATION]
- **Tenant hides a language after patients with it exist.** Existing records retain the reference; only new bookings at that tenant cannot pick the hidden language. [Source: Adrian best-guess -- NEEDS CONFIRMATION]
- **Duplicate Name on create.** No uniqueness constraint at DB level. [Source: observed, not authoritative]

## Success criteria

- Patient language field is required on every patient record.
- Non-English patient language triggers the translator-required indicator on the appointment and on every case-party notification.
- English patient language does not trigger any translator path.
- Tenant admin can hide languages not supported by their practice from their users' dropdowns.
- Host admin can add a new language to the global list; every tenant sees it (subject to their own hide/show choices).

## Known discrepancies with implementation

- `[observed, not authoritative]` `Patient.AppointmentLanguageId` is currently SetNull and nullable; a patient without a language can exist. Intent requires it non-null (or enforced by the AppService).
- `[observed, not authoritative]` No translator-trigger logic exists in code; the FK is stored but never read. Intent (automatic translator-required derivation + all-parties notification) is entirely unbuilt.
- `[observed, not authoritative]` The notification system itself (FEAT-05) is pending; the all-parties-notification-on-translator-required behavior cannot ship before the base notification system is built.
- `[observed, not authoritative]` No per-tenant visibility flag exists -- per-tenant hide/show is intent-only; a new mechanism is required.
- `[observed, not authoritative]` No uniqueness constraint on `Name`; duplicates are permitted.
- `[observed, not authoritative]` No `DataSeedContributor` for `AppointmentLanguage`; host admin enters languages manually.
- `[observed, not authoritative]` The `AppointmentLanguage` entity and its `Patient` FK live under `TenantMigrations` in the EF Core project, despite the host-scope configuration in `CaseEvaluationDbContext`. Host-scope intent is authoritative per root CLAUDE.md.

## Outstanding questions

- MVP default for patient language field ([UNKNOWN -- queued for Adrian]): English-auto-selected vs require-explicit-pick.
- Exact legal rule citation for translator-provision requirement (rolls up to Q7 legal/compliance, which is actively being researched per OUTSTANDING-QUESTIONS.md).
- Deletion-of-in-use-language behavior ([UNKNOWN -- queued for Adrian]): block vs SetNull.
