# Parity flags

Tracks behaviors where NEW deviates from OLD (or where OLD's behavior is
ambiguous and we have replicated it pending test verification). Per
project CLAUDE.md, every flag here pairs with a `// PARITY-FLAG:`
comment on the relevant C#/TypeScript line.

Status legend:
- `needs-test` — replicated OLD; manual test pending to confirm intent
- `parity-plus` — NEW deliberately exceeds OLD; OLD did not have this
- `bug-fix` — silently corrected an OLD bug; not preserved
- `resolved` — flag closed (manual test passed or replaced with verbatim port)

| Flag | Feature | OLD source citation | Description | Status |
|------|---------|---------------------|-------------|--------|
| PF-001 | Reminder jobs / JDF | OLD has no dedicated JDF reminder template | NEW's `PackageDocumentReminderEmailHandler` fires for both package-doc and JDF reminders. For JDF, the closest OLD-verbatim template code is `AppointmentDueDateUploadDocumentLeft` (TemplateCode 15, "due-date + pending docs left"). OLD's actual JDF flow is auto-cancel via `AppointmentCancelledDueDate` without a separate reminder. Mapping NEW's JDF reminder to `AppointmentDueDateUploadDocumentLeft` is best-effort semantic parity until N1 (Stage 7) revisits the JDF reminder job and confirms intent. | needs-test |
| PF-002 | CustomField rendering | OLD `PatientAppointment.DbEntities\Enums\CustomFieldType.cs` declares all 7 values; OLD Angular templates render only Alphanumeric / Numeric / Date | NEW carries all 7 enum values verbatim (Alphanumeric=12, Numeric=13, Picklist=14, Tickbox=15, Date=16, Radio=17, Time=18) for schema parity. Stage 2 task **B1** is parity-plus: the booking form will render all 7 input types (Picklist / Radio / Tickbox / Time + the original 3) so IT-Admin-defined custom fields work with every declared type. OLD's enum-vs-render mismatch was a latent bug; NEW closes it. | parity-plus |
