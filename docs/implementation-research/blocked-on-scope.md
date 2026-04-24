# Capabilities blocked on open scope questions

**Status:** placeholder. Populated by Phase 4.

Phase 4 collects every `Blocked by open question: Q#N` value from the briefs in
`solutions/`, groups capabilities by question ID, quotes each question verbatim
from `docs/gap-analysis/README.md` (Consolidated open questions section, README
lines 227-271), and for each group proposes a default assumption:

- If Adrian answers `yes`: these M capabilities enter wave W (Phase 3 output).
- If Adrian answers `no`: these capabilities drop from MVP scope.

The goal is that Adrian can batch-answer all open questions in one pass with the
downstream consequences visible for each answer.

## Known open questions (reference)

From `docs/gap-analysis/README.md` lines 227-271, the 32 consolidated open questions
are organized into 4 groups:

- Feature-scope decisions (Q1 through Q16) -- affect whether capabilities are in
  or out of MVP.
- Architecture decisions (Q17 through Q24) -- affect how a capability is built.
- Security / compliance (Q25 through Q27) -- already partly acknowledged by Adrian.
- Process / confirmation (Q28 through Q32) -- verification or confirmation items.

Phase 4 emits verbatim quotes with precise capability-to-question mappings.
