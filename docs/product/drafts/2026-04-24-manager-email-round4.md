---
purpose: Round-4 manager email -- two net-new questions from T8 lookup cluster and T10 auth-and-roles work
date: 2026-04-24
covers: OUTSTANDING-QUESTIONS.md Q26, Q27
prior-rounds: round-1 covered Q1, Q7, Q9, Q11, Q16; round-2 covered Q17, Q18, Q19; round-3 covered Q20, Q21, Q22, Q23, Q24, Q25
note: Q21 from round-3 has since been resolved on a follow-up call with Adrian; the manager does not need to answer that one anymore. Status mention included up front so the manager can disregard.
---

**Subject:** Patient Portal MVP -- 2 more questions from this week's work

Hi [Manager],

Two follow-on questions from this week's work on the lookup catalogues (the reference lists the portal uses for office locations, appointment types, and similar) and on the role hierarchy. Both are net new -- no overlap with prior batches.

Quick status update first: Q21 from round-3 (about doctor portal login + the "doctor's manager" role on our side) is no longer waiting on you. We worked through it on a follow-up call and confirmed (a) doctors don't get a special role -- they get a regular Practice Admin login if they want one, and (b) "doctor's manager at our side" is a separate role from host admin -- a Gesco-side mid-tier we're calling "Supervisor Admin", scoped to an explicit portfolio of practices. So you can disregard that one. Q20, Q22, Q23, Q24, Q25 from round-3 are still open on your side; this email does not re-ask them.

## For your business / product manager

**1. What is the intended MVP role of the WCAB Office catalogue in the portal?**

The portal's code has a complete WCAB office entity (California Workers' Comp Appeals Board district-office records -- name, abbreviation, address, active / inactive flag) with admin CRUD, an Angular admin screen, and an Excel export. But no other feature references this list -- no appointment, no case record, no notification reads it. We don't know what the catalogue is supposed to do inside the Patient Portal at MVP.

Candidates we'd consider:

- **Data directory only.** Staff reference the list for case context (e.g., to know which WCAB handles a given case). No live business linkage; no FK from any appointment or case record.
- **Planned future linkage.** The catalogue is seeded in advance because an upcoming feature will link an appointment or case record to a WCAB office.
- **Downstream consumption.** Case Tracking, MRR AI, or another Gesco product consumes this list and the Patient Portal is the source of truth.
- **Not needed at MVP.** The feature was over-built; retire it or deprioritise until a concrete need arises.

Please tell us what the catalogue is actually for -- or whether we should drop it -- and we'll scope the admin UI and any downstream linkage accordingly.

Related but smaller: the Excel export on this list is currently reachable without logging in. No patient data is at risk (WCAB offices are public DWC data), but the pattern is inconsistent with every other export in the portal. We can close that gap as part of answering the main question.

**2. When a booker picks an Appointment Type on the booking form, which specific fields on the appointment does the type pre-fill?**

Adrian confirmed in a recent session that picking an Appointment Type drives multiple other fields on the appointment (it is not just a classification label). He did not know which specific fields; this one is for you.

Candidate fields we'd consider:

- **Default slot duration** -- e.g., QME Orthopedic defaults to 45 minutes, AME Psychiatric to 60 minutes.
- **Default billing rate / pricing** -- each type carries its own rate.
- **Required prep documents** -- certain types require specific medical records handed over in advance.
- **Default prep instructions for the patient** -- e.g., fasting, wearing loose clothing, bringing current imaging.
- **Default time-of-day alignment** -- some types only happen at specific times.

Please tell us which of these the type should actually drive, plus anything we have missed. For each field you confirm, we also need to know whether the type's default is editable by the booker on a specific appointment or locked.

---

## What each answer unblocks on my side

- **1** -- WCAB Office admin UI scope; whether to plan the future linkage now or defer; whether to fix the unauthenticated export endpoint.
- **2** -- Appointment Type form schema (which fields each Type row carries) and the booking form's pre-fill behavior.

Thanks,
Adrian
