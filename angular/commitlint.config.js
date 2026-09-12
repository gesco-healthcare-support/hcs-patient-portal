module.exports = {
  extends: ['@commitlint/config-conventional'],
  // Skip "merge: ..." integration commits. config-conventional's defaultIgnores
  // catches "Merge ..." / "Revert ..." but not this repo's lowercase "merge:"
  // prefix; merge commits are not conventional commits and must not be linted.
  ignores: [(message) => /^merge:/i.test(message)],
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
