/** @type {import('dependency-cruiser').IConfiguration} */
const config = {
  forbidden: [
    {
      name: 'no-circular-dependencies',
      severity: 'error',
      from: { path: '^(scripts|tests)/' },
      to: { circular: true },
    },
    {
      name: 'scripts-do-not-import-tests',
      severity: 'error',
      from: { path: '^scripts/' },
      to: { path: '^tests/' },
    },
    {
      name: 'no-unresolved-local-imports',
      severity: 'error',
      from: { path: '^(scripts|tests)/' },
      to: { couldNotResolve: true, dependencyTypesNot: ['core'] },
    },
  ],
  options: {
    doNotFollow: { path: 'node_modules' },
    includeOnly: '^(scripts|tests)/',
  },
}

export default config
