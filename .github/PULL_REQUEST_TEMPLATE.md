## Summary

<!-- What does this PR do? Keep it to 1-3 bullet points. -->

-

## Changes

<!-- List the key changes made. -->

-

## Test Plan

- [ ] Tests pass locally (`dotnet test`)
- [ ] Angular build succeeds (`npx ng build --configuration development`)
- [ ] Manual testing performed (describe what was tested)

## Screenshots

<!--
Default (no UI change): N/A.
If UI files changed (angular/**, *.component.*, *.html, *.scss):
 - If Playwright MCP is installed in the session, Claude MUST capture before/after screenshots via the MCP, commit them to .github/pr-media/, and embed them here via relative markdown image links, e.g. ![intake form -- after](./.github/pr-media/patient-intake-after.png).
 - If no Playwright MCP is available, leave "TODO: attach before/after" and attach via the GitHub web UI before merge.
Synthetic data only -- never capture real patient data.
-->

N/A (no UI change)

## Documentation

- [ ] Feature CLAUDE.md updated (if applicable)
- [ ] docs/ updated (if applicable)
- [ ] No new docs needed

## HIPAA Checklist

- [ ] No real patient data (names, SSNs, DOBs, medical records) in code, tests, or logs
- [ ] All test data uses synthetic/dummy values
- [ ] No PHI exposed in API responses beyond what consumers need
- [ ] No new logging of PHI fields
- [ ] `appsettings.secrets.json` not modified or committed

## Additional Notes

<!-- Anything else reviewers should know? -->
