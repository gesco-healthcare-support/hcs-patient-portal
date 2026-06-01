---
id: G-05-INTENT-01
title: All SMS legs dropped across every reminder job
source_file: 05-jobs-scheduler.md
category: Intent deviation
decision: defer
depth_tier: medium
effort: M
root_cause_confidence: high
repro_status: not-reproduced-static-confirmed
dependencies: [G-04-01, "SMS provider integration (Twilio/ACS-SMS creds, master-plan 18.3)"]
duplicate_of: []
defer_trigger: SMS provider integrated (Twilio or Azure Communication Services SMS creds land + Volo.Abp.Sms ISmsSender wired into the host) -- the same dependency G-04-01 gates on.
---

## 1. ID & Summary
OLD reminder jobs sent a Twilio SMS leg in addition to email for reminders 1, 2, 4,
5, 6 (`SchedulerDomain.cs:105, 137, 191, 218, 249`). NEW delivers email only -- no
SMS is sent anywhere in the job/notification pipeline. Classed as an intent deviation
(deliberate Phase 1 scope cut, not an oversight) because the outcome changes: fewer
channels reach the recipient. Deferred until the SMS provider rollout lands; this is
the reminder-job-side slice of the same SMS work tracked by G-04-01.

## 2. Docs Reviewed
- docs/parity-v2/05-jobs-scheduler.md (section "### G-05-INTENT-01", lines 228-256).
- docs/parity-review-log.csv lines 23 (G-04-01), 36, 37 (this item) -- confirms
  cross-ref and the ~3-month (by 2026-08-29) plan.
- NEW: NotificationDispatcher.cs, INotificationDispatcher.cs (Application + Contracts).
- OLD: SchedulerDomain.cs, TwilioSmsService.cs (read-only).

## 3. Affected Code (brief: where the gap is + gating dependency)
Gap location (NEW):
- `src/HealthcareSupport.CaseEvaluation.Application/Notifications/NotificationDispatcher.cs:61-126`
  -- `DispatchAsync` renders `BodySms` but only calls `EnqueueEmailAsync`; no SMS
  sender is invoked. Lines 19-28 document the deliberate deferral (Volo.Abp.Sms +
  Twilio modules "not yet referenced ... belongs with the Twilio creds rollout").
- DOC/IMPL MISMATCH (note, not a separate gap): the interface XML doc claims SMS
  "dispatches synchronously through ABP's ISmsSender"
  (`INotificationDispatcher.cs:13-16, 30-31`), but the impl does no such thing. The
  contract comment overstates current behavior; worth aligning when SMS lands.

Gating dependency:
- `Volo.Abp.Sms` is present only as a TRANSITIVE package in lock files
  (HttpApi.Host/Application/AuthServer packages.lock.json) -- NOT a referenced module,
  NO `ISmsSender` registration, NO provider (Twilio/ACS) wired. Grep under `src\` for
  `Twilio|SendSms|SmsService` returns only comments and the `BodySms` template field.

OLD behavior (ground truth):
- `PatientAppointment.Domain/Core/SchedulerDomain.cs:24,27` inject `ITwilioSmsService`;
  `:105` and `:137` call `TwilioSmsService.SendSms(item.PhoneList.Replace("-",""), smsBody)`
  (doc cites all five legs: 105, 137, 191, 218, 249). Reminders 3 and 9 were
  email-only; 7 and 8 sent nothing (commented out).
- `PatientAppointment.Infrastructure/Utilities/TwilioSmsService.cs:12-47` -- working
  Twilio provider (`SendSms(toPhoneNo, msgData)`). DO NOT PORT the in-house package /
  AWS infra; this is the behavioral spec, not the implementation to copy.

## 4. Expected vs Actual
- Expected (parity with OLD): reminders 1, 2, 4, 5, 6 each emit an SMS (phone number
  normalized by stripping dashes) in addition to the email; reminders 3 and 9 stay
  email-only.
- Actual (NEW): every reminder is email-only. `BodySms` is rendered (template carries
  the text) but never delivered.

## 5. Repro Status
not-reproduced-static-confirmed. Confirmed statically: NEW dispatcher only enqueues
email (NotificationDispatcher.cs:85-88 -> EnqueueEmailAsync), no `ISmsSender` exists in
`src\`, and `Volo.Abp.Sms` is transitive-only. A true runtime repro (triggering a
reminder cron and observing no SMS) would require seeding appointment/recipient state
and firing a Hangfire job -- state mutation, out of scope for read-only research. Static
confirmation is sufficient and unambiguous here.

## 6. Search Strings
- NEW: `Twilio|SendSms|SmsService|BodySms|ISmsSender` under `src\`.
- NEW: `DispatchAsync` / `EnqueueEmailAsync` in NotificationDispatcher.cs.
- OLD: `TwilioSmsService.SendSms` / `ITwilioSmsService` in SchedulerDomain.cs.
- CSV: `G-04-01`, `G-05-INTENT-01` in docs/parity-review-log.csv.

## 7. Candidate Solution (high-level; deep sourcing deferred)
HOLD deep external sourcing until the upstream SMS provider lands -- stated explicitly.
High-level approach once unblocked (do this with G-04-01, not standalone):
1. Add an SMS provider module to the host (ABP supports `Volo.Abp.Sms.ISmsSender`;
   provider = Twilio or Azure Communication Services per master-plan 18.3). Register
   creds via settings/secrets, not hardcoded.
2. In `NotificationDispatcher.DispatchAsync`, after the email enqueue loop, send SMS
   when `rendered.BodySms` is non-empty AND the recipient has a phone (mirrors OLD's
   per-recipient guard). Normalize the number (OLD stripped dashes via
   `Replace("-","")`); prefer E.164 normalization for a modern provider.
3. Restrict the SMS legs to the reminders that had them in OLD (1, 2, 4, 5, 6); leave
   3 and 9 email-only.
4. Add an enable flag (per G-04-01 note: "+ enable flag") so SMS can be toggled per
   tenant/environment.
5. Align the `INotificationDispatcher` XML doc with reality at that time.
Cite the live OLD spec at delivery time: SchedulerDomain.cs:105/137/191/218/249 and
TwilioSmsService.cs:12-47. ABP `ISmsSender` / provider SDK docs to be sourced when the
provider choice is finalized (deferred).

## 8. Root-Cause Hypothesis
Not a defect -- a documented, deliberate Phase 1 scope cut. SMS delivery was intentionally
left unwired because no SMS provider (Twilio/ACS) credentials or module exist in the NEW
solution yet (`Volo.Abp.Sms` is transitive-only). The dispatcher was built email-first
with `BodySms` already rendered so the SMS leg is a localized, additive change once the
provider lands. Confidence: high (the deferral is explicitly stated in code comments and
the CSV decision log, and OLD behavior is directly cited).

## 9. Open Questions (incl. the defer trigger / what Adrian must confirm)
- Defer trigger: SMS provider integrated (G-04-01's dependency) -- planned ~3 months out
  (by 2026-08-29). When that lands, un-defer and implement the reminder-job SMS legs in
  the same slice.
- Provider choice: Twilio (OLD) vs Azure Communication Services SMS (master-plan 18.3
  hint mentions "Twilio/ACS-SMS")? Adrian to confirm; affects SDK + creds.
- Recipient phone source: which NEW field holds the recipient phone, and is it populated
  for the same recipient set OLD targeted? OLD used `item.PhoneList`. Needs confirmation
  when wiring.
- Enable-flag scope: per-tenant setting vs global app setting? CSV says "+ enable flag";
  granularity unspecified.
- Confirm reminders 7 and 8 stay no-op (commented out in OLD) -- not part of this gap.
