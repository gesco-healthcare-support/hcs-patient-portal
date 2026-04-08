---
paths:
  - "src/**/Domain/**/*.cs"
  - "src/**/Application.Contracts/**/*.cs"
  - "src/**/Application/**/*.cs"
  - "src/**/HttpApi/Controllers/**/*.cs"
---
# HIPAA Data Model Rules

When working with entity, DTO, or API files in this project:

- Review every entity and DTO for PHI exposure risk
- PHI fields in this project: patient names, dates of birth, phone numbers, addresses, email, medical appointment details
- Flag any new property that stores or transmits PHI
- Ensure DTOs do not expose more PHI than the consumer needs
- Verify `[Authorize]` with appropriate permissions on PHI-containing endpoints
- Never use real patient data in code examples, test data, or comments
- If adding new fields that could contain PHI: document them in the feature CLAUDE.md
- Patient, Appointment, and AppointmentEmployerDetail are the primary PHI-touching entities
