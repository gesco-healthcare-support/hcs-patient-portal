module.exports = {
  extends: ['@commitlint/config-conventional'],
  // Skip "merge: ..." integration commits. config-conventional's defaultIgnores
  // catches "Merge ..." / "Revert ..." but not this repo's lowercase "merge:"
  // prefix; merge commits are not conventional commits and must not be linted.
  //
  // Also skip Dependabot's commits (2026-09-29). Their titles are generated, often run past
  // the 100-character header limit (the squash of PR #1154 was 106), and cannot be edited
  // before they land. Matched on Dependabot's sign-off trailer, so a human commit that only
  // mentions Dependabot is still linted.
  ignores: [
    (message) => /^merge:/i.test(message),
    (message) => /^Signed-off-by: dependabot\[bot\] <support@github\.com>$/m.test(message),
  ],
  rules: {
    'type-enum': [
      2,
      'always',
      [
        'feat',
        'fix',
        'docs',
        'style',
        'refactor',
        'test',
        'chore',
        'ci',
        'perf',
        'build',
        'revert',
      ],
    ],
    'subject-max-length': [2, 'always', 100],
    'body-max-line-length': [1, 'always', 200],
    // subject-case inherited from config-conventional disallows
    // sentence-case / start-case / pascal-case / upper-case at the
    // start of the subject. Disabled because the team's domain
    // vocabulary regularly leads with proper nouns and acronyms
    // (e.g. "IT Admin", "OLD app", "MinIO", "MailKit", "MRR AI").
    // The 100-char subject cap above is the binding hygiene check.
    'subject-case': [0],
  },
};
