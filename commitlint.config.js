module.exports = {
  extends: ['@commitlint/config-conventional'],
  rules: {
    'type-enum': [2, 'always', [
      'feat', 'fix', 'docs', 'style', 'refactor',
      'test', 'chore', 'ci', 'perf', 'build', 'revert'
    ]],
    'subject-max-length': [2, 'always', 100],
    'body-max-line-length': [1, 'always', 200],
  }
};
