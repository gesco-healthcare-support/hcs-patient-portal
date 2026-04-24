---
purpose: Round-2 manager email -- three questions after T3 DoctorAvailabilities
date: 2026-04-24
covers: OUTSTANDING-QUESTIONS.md Q17, Q18, Q19 (renumbered 1-3 for the email; internal cross-refs preserved in the spec)
note: reconstructed 2026-04-24 from conversation history; Adrian sent after round-1, before round-3
---

**Subject:** Patient Portal MVP -- 3 more questions from the schedule / booking work

Hi [Manager],

Three follow-on questions from this week's scheduling work. Separate from the five I sent earlier -- only the new ones are here, so please ignore these if you've already forwarded the prior batch.

**1. What should "Reserved" mean on a time slot?**

Every time slot on a doctor's schedule has a label. Two labels are clear: "Available" (open for booking) and "Booked" (already taken by an approved appointment). The third label is "Reserved", and nothing in the current system actually sets a slot to Reserved -- it's defined but unused.

My best guess is that Reserved should apply to a slot where someone has submitted a booking request but the doctor's office has not yet approved it. That would fit the two-step booking flow: the booker submits, the slot becomes Reserved while the office reviews, and then it moves to Booked (if approved) or back to Available (if rejected, or if the send-back-for-info goes unanswered). Two other interpretations I considered -- "the doctor is busy" and "an approved appointment" -- both seemed self-contradictory to me, so I ruled them out.

Please confirm whether the pending-review interpretation is right, or correct me if Reserved should mean something else.

**2. How long is each time slot, and where does the length come from?**

Three possible models:

- The length comes from the type of exam. Every QME is 2 hours, every AME is 1 hour, and so on. When the office publishes a slot of a given type, the length is automatic.
- The office picks the length each time they publish. No standard length per exam type; the office decides for each slot.
- A default length per exam type, which the office can override on specific slots.

This changes how the office creates the schedule and what the booker sees when they pick a time. I don't know the answer personally -- it sits with whoever owns that decision.

**3. Can the office directly edit a slot after it's been booked, or does any change have to go through the cancel / reschedule process?**

My intuition is that booked slots should be locked. If the office needs to shift a patient's appointment by 30 minutes, they should go through the formal cancel / reschedule flow rather than silently changing the time on the booked slot. The reason is the same ex-parte point I raised in the earlier batch: any change to a committed appointment should produce a record and notify every party. Direct edits would bypass that.

I want to confirm with you rather than assume. If direct edits ARE allowed in some cases, I need to know (a) who's allowed to do them, (b) what counts as small enough to skip the formal process, and (c) what notifications go out when it happens.

---

## What each answer unblocks on my side

- **1** -- whether the schedule needs an extra "awaiting review" stage in the slot labelling, and when slots move between labels during a booking.
- **2** -- how the publishing screen is laid out and where the length of a slot is stored.
- **3** -- the boundary between schedule management and appointment management in the office's day-to-day.

Once these come back I can finalize the scheduling spec and move to the next feature.

Thanks,
Adrian
