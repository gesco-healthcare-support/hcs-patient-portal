[Home](../../INDEX.md) > [Product Intent](../) > Escalations

# Open Escalation Items -- Patient Portal MVP

**Status:** live; items are removed from this file once answered and folded back into the spec.

These are open product-intent questions that the direct manager has flagged for further consultation. Each item below carries the original question, the context for why the answer matters to MVP, and what the answer unblocks on the build side. When it is time to draft a message covering these, the text will be neutrally framed -- a substantive ask, not a list of complaints.

---

## Item 1 -- Cancel / reschedule in MVP

**Open question:** Should the portal let anyone cancel or reschedule an approved appointment in MVP? If yes, who (staff at the doctor's office, the central admin on our side, or both) and what notifications go out when it happens?

**Why it matters:** This is the biggest remaining MVP-scope lever on the appointments workflow. If cancel / reschedule is NOT in MVP, the portal ships book-review-approve only and cancellations happen off-portal via phone / email until a later phase. If IN MVP, the build adds: admin-side cancel / reschedule UI, slot-release logic, modification notifications, a possible new Packet version, and the legal-record handling that goes with every data change on an already-notified request.

**What the answer unblocks:** modification-flow spec, sizing of the MVP cancel / reschedule work, and the slot-lifecycle state machine on `DoctorAvailability`.

**Spec cross-reference:** `docs/product/appointments.md` modification-flow subsection (currently flagged provisional, awaiting this answer); OUTSTANDING-QUESTIONS.md Q1.

---

## Item 2 -- Exact format of each required notification email

**Open question:** What exact format does each required notification email need to follow so it counts as defensible evidence of communication? We need the actual format per event (request submit, approve, reject, send-back-for-info, modifications) and per party-type (patient, applicant attorney, defense attorney, insurance company, claim adjustor, doctor's office, sometimes employer). If the firm or client already has legal-comms templates we can adopt, those would unblock the build immediately. Also: what audit-trail, retry-on-bounce, delivery-receipt, or logging expectations come with the legal-evidence standard?

**Why it matters:** FEAT-05 (email system wiring) is the single biggest MVP-blocking build item on the appointments-notifications flow. The strict format requirement (confirmed earlier because notifications are legal communications on the ex-parte principle) means we cannot ship templates by developer intuition -- we need the actual format.

**What the answer unblocks:** the MVP email-template build per event and per party-type; any retry / logging / bounce-handling infrastructure we need alongside the sender.

**Related partial answer (direct manager):** Regardless of the exact format, the system should show the booker a clear confirmation after each notification event ("A confirmation email has been sent successfully"). That success-UX expectation is captured in `appointments.md`; the format specifics remain open.

**Spec cross-reference:** `docs/product/appointments.md` integration-points email-notifications subsection; OUTSTANDING-QUESTIONS.md Q9.

---

## Item 3 -- Direct edits on booked slots

**Open question:** Can the doctor's office directly edit a slot's time or location after a booking has been approved on it, or does every change have to go through the formal cancel / reschedule process?

**Why it matters:** This is the boundary between "slot management" and "appointment management" in the office's day-to-day. If direct edits are allowed, the office can shift a patient's time 30 minutes later without the ceremony of cancel-and-rebook -- but any change has to produce a notification under the ex-parte rule, and we need to know who's allowed to do what.

**Developer's intuition (not yet confirmed):** Booked slots should be locked; all changes go through cancel / reschedule so the audit trail stays clean and every committed appointment change produces a notification record. This intuition is captured as best-guess in `doctor-availabilities.md` pending this confirmation. If direct edits ARE allowed in some cases, we also need: (a) who is allowed to do them, (b) what counts as small-enough to skip the formal process, and (c) what notifications go out when it happens.

**What the answer unblocks:** the `DoctorAvailability` entity's edit-authority model at MVP, and the relationship between slot-edit authority and the T7 universal post-submit lock rule.

**Spec cross-reference:** `docs/product/doctor-availabilities.md` business-rules subsection; OUTSTANDING-QUESTIONS.md Q19.

---

## Index

| Item | Topic | Spec file | Outstanding-questions cross-ref |
|------|-------|-----------|----------------------------------|
| 1 | Cancel / reschedule MVP scope | `appointments.md` | Q1 |
| 2 | Required notification email formats | `appointments.md` | Q9 |
| 3 | Direct edits on booked slots | `doctor-availabilities.md` | Q19 |

## Change log

- 2026-04-24 -- Initial escalation set: Q1, Q9 (format specifics), Q19. All three were flagged for further consultation during the 2026-04-24 response round.
