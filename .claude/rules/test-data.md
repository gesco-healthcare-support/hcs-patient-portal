---
paths:
  - "test/**/*.cs"
---
# Test Data Rules (HIPAA)

When writing or modifying test files:

- ALL test data MUST be synthetic — never use data that could resemble real patients
- Use approved synthetic patterns from `.claude/discovery/test-patterns.md`
- String values: use random hex strings matching max length constraints (existing project convention)
- Email values: `"{randomhex}@{randomhex}.com"` format
- Phone numbers: use 555 prefix reserved range only
- Date values: obviously fake dates like `1990-01-01`
- Identifier values: must start with TEST- or FAKE- prefix
- Patient names in tests: random hex strings (NOT realistic names)
- NEVER copy data from production, staging, or demo environments
- NEVER use patterns resembling real government-issued ID numbers
- The PHI scanner hook will block real-looking patterns in tool input
