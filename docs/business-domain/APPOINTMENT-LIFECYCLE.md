# Appointment Lifecycle

[Home](../INDEX.md) > [Business Domain](./) > Appointment Lifecycle

## Overview

Every appointment in the HCS Case Evaluation Portal moves through a defined set of statuses. The `AppointmentStatusType` enum (defined in `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs`) contains **13 statuses**, each representing a distinct phase in the appointment lifecycle.

---

## Appointment Statuses

| Value | Status                  | Description                                                                 |
|-------|-------------------------|-----------------------------------------------------------------------------|
| 1     | **Pending**             | Initial state when an appointment is created or requested                   |
| 2     | **Approved**            | Appointment confirmed by admin or doctor                                    |
| 3     | **Rejected**            | Appointment denied                                                          |
| 4     | **NoShow**              | Patient did not appear for the scheduled examination                        |
| 5     | **CancelledNoBill**     | Cancelled with sufficient notice; no billing applies                        |
| 6     | **CancelledLate**       | Cancelled late; may incur a late-cancellation fee                           |
| 7     | **RescheduledNoBill**   | Rescheduled with sufficient notice; no billing for the original slot        |
| 8     | **RescheduledLate**     | Rescheduled late; may incur billing for the original slot                   |
| 9     | **CheckedIn**           | Patient has arrived and checked in at the office                            |
| 10    | **CheckedOut**          | Examination completed; patient has left                                     |
| 11    | **Billed**              | Final state; examination report and billing have been processed             |
| 12    | **RescheduleRequested** | An external user requested a reschedule (awaiting admin action)             |
| 13    | **CancellationRequested** | An external user requested cancellation (awaiting admin action)           |

---

## Full State Machine Diagram

This is the **centerpiece** diagram showing all 13 states and their valid transitions.

```mermaid
stateDiagram-v2
    [*] --> Pending : Appointment created

    Pending --> Approved : Admin/doctor confirms
    Pending --> Rejected : Admin/doctor denies
    Pending --> CancelledNoBill : Cancelled early
    Pending --> CancelledLate : Cancelled late
    Pending --> RescheduledNoBill : Rescheduled early
    Pending --> RescheduledLate : Rescheduled late
    Pending --> RescheduleRequested : External user requests reschedule
    Pending --> CancellationRequested : External user requests cancellation

    Approved --> CheckedIn : Patient arrives
    Approved --> NoShow : Patient does not appear
    Approved --> CancelledNoBill : Cancelled early
    Approved --> CancelledLate : Cancelled late
    Approved --> RescheduledNoBill : Rescheduled early
    Approved --> RescheduledLate : Rescheduled late
    Approved --> RescheduleRequested : External user requests reschedule
    Approved --> CancellationRequested : External user requests cancellation

    CheckedIn --> CheckedOut : Examination completed

    CheckedOut --> Billed : Billing processed

    RescheduleRequested --> Approved : Admin approves reschedule
    RescheduleRequested --> RescheduledNoBill : Admin reschedules (no bill)
    RescheduleRequested --> RescheduledLate : Admin reschedules (late)
    RescheduleRequested --> Rejected : Admin rejects request

    CancellationRequested --> CancelledNoBill : Admin cancels (no bill)
    CancellationRequested --> CancelledLate : Admin cancels (late)
    CancellationRequested --> Approved : Admin denies cancellation

    Billed --> [*]
    Rejected --> [*]
    CancelledNoBill --> [*]
    CancelledLate --> [*]
    RescheduledNoBill --> [*]
    RescheduledLate --> [*]
    NoShow --> [*]
```

---

## Happy Path

The ideal appointment flow from creation to billing.

```mermaid
flowchart LR
    A[Pending] -->|Admin confirms| B[Approved]
    B -->|Patient arrives| C[CheckedIn]
    C -->|Exam completed| D[CheckedOut]
    D -->|Billing processed| E[Billed]

    style A fill:#ffd966,stroke:#333
    style B fill:#93c47d,stroke:#333
    style C fill:#6fa8dc,stroke:#333
    style D fill:#8e7cc3,stroke:#333
    style E fill:#76a5af,stroke:#333
```

**Sequence:** `Pending (1)` -> `Approved (2)` -> `CheckedIn (9)` -> `CheckedOut (10)` -> `Billed (11)`

---

## Cancellation and Reschedule Paths

```mermaid
flowchart TD
    subgraph Direct Admin Actions
        ANY1[Any Active Status] -->|Early cancellation| CNB[CancelledNoBill]
        ANY1 -->|Late cancellation| CL[CancelledLate]
        ANY2[Any Active Status] -->|Early reschedule| RNB[RescheduledNoBill]
        ANY2 -->|Late reschedule| RL[RescheduledLate]
    end

    subgraph External User Requests
        ANY3[Any Active Status] -->|User requests reschedule| RR[RescheduleRequested]
        ANY3 -->|User requests cancellation| CR[CancellationRequested]
        RR -->|Admin acts| RNB2[RescheduledNoBill / RescheduledLate]
        CR -->|Admin acts| CNB2[CancelledNoBill / CancelledLate]
        RR -->|Admin denies| APR[Approved / Rejected]
        CR -->|Admin denies| APR2[Approved]
    end

    style CNB fill:#ea9999,stroke:#333
    style CL fill:#e06666,stroke:#333
    style RNB fill:#f9cb9c,stroke:#333
    style RL fill:#e69138,stroke:#333
    style RR fill:#ffe599,stroke:#333
    style CR fill:#ffe599,stroke:#333
```

### External User Request Flow

When an external user (Patient, Attorney, etc.) wants to cancel or reschedule, they do not directly change the appointment status. Instead:

1. The appointment moves to **RescheduleRequested (12)** or **CancellationRequested (13)** -- these are "pending admin action" states.
2. An admin reviews the request and transitions to the appropriate terminal status.

---

## Terminal States

These statuses represent the end of an appointment's lifecycle. No further transitions occur from these states.

| Status              | Description                                         |
|---------------------|-----------------------------------------------------|
| **Billed (11)**           | Successfully completed and billed                 |
| **Rejected (3)**          | Denied before it could proceed                    |
| **CancelledNoBill (5)**   | Cancelled early, no charge                        |
| **CancelledLate (6)**     | Cancelled late, possible fee                      |
| **RescheduledNoBill (7)** | Rescheduled early, no charge for original         |
| **RescheduledLate (8)**   | Rescheduled late, possible fee for original       |
| **NoShow (4)**            | Patient failed to appear                          |

---

## Billing Implications

| Variant            | Billing Impact                                                   |
|--------------------|------------------------------------------------------------------|
| **NoBill** variants (CancelledNoBill, RescheduledNoBill) | No charge for the cancelled/rescheduled appointment |
| **Late** variants (CancelledLate, RescheduledLate)       | Possible late-cancellation or late-reschedule fee   |
| **Billed**         | Full examination billing has been processed                      |
| **NoShow**         | May incur a no-show fee depending on business rules              |

---

## Source Reference

- **Enum definition:** `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs`

```csharp
public enum AppointmentStatusType
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    NoShow = 4,
    CancelledNoBill = 5,
    CancelledLate = 6,
    RescheduledNoBill = 7,
    RescheduledLate = 8,
    CheckedIn = 9,
    CheckedOut = 10,
    Billed = 11,
    RescheduleRequested = 12,
    CancellationRequested = 13,
}
```

---

## Related Documentation

- [Domain Overview](DOMAIN-OVERVIEW.md)
- [Doctor Availability](DOCTOR-AVAILABILITY.md)
- [Enums and Constants](../backend/ENUMS-AND-CONSTANTS.md)
- [Application Services](../backend/APPLICATION-SERVICES.md)
