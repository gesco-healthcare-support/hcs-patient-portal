// ES module wrapper for wagoid/commitlint-github-action@v6, which rejects
// .js configFile paths (commitlint v19 requires ESM for explicit configFile).
// The CommonJS .js sibling at ./commitlint.config.js is kept for the Husky
// commit-msg hook; keep both files in sync if rules change.
export default {
  extends: ['@commitlint/config-conventional'],
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
  },
};
