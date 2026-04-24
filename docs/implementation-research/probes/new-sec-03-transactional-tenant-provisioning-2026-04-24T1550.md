# Probe log: new-sec-03-transactional-tenant-provisioning

**Timestamp (local):** 2026-04-24T15:50:00
**Purpose:** Document the static-analysis proof of NEW-SEC-03 and record why no mutating tenant-create probe was executed.

## Live Verification Protocol constraint

`docs/implementation-research/README.md:247-261` bans state-mutating probes for SaaS tenant creation ("Never probe SaaS tenant creation ... persistent state manual cleanup might miss"). The brief therefore relies on static code analysis.

## Evidence 1 -- transactional flag is wrong (static)

**Source:** `W:/patient-portal/implementation-research/src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs:57`

````text
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
