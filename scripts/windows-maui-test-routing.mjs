import { appendFileSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
import { resolve } from 'node:path'

const windowsMauiPaths = [
  '.github/actions/setup-mise-toolchain/action.yml',
  '.github/workflows/native-contract-tests.yml',
  '.mise.toml',
  'global.json',
  'dotnet-clients/Directory.Build.props',
  'dotnet-clients/Directory.Build.targets',
  'dotnet-clients/Directory.Packages.props',
  'dotnet-clients/NuGet.config',
  'dotnet-clients/src/',
  'dotnet-clients/tooling/restore-locks.sh',
  'scripts/windows-maui-test-routing.mjs',
]

export function shouldRunWindowsMauiBuild(paths) {
  return paths.some(path =>
    windowsMauiPaths.some(prefix =>
      prefix.endsWith('/') ? path.startsWith(prefix) : path === prefix,
    ),
  )
}

function requireSha(value, label) {
  if (!/^[a-f0-9]{40}$/u.test(value ?? '')) throw new Error(`${label} must be a full commit SHA`)
  return value
}

function main() {
  const [, , baseArgument, revisionArgument] = process.argv
  const base = requireSha(baseArgument, 'base SHA')
  const revision = requireSha(revisionArgument, 'revision SHA')
  const repositoryRoot = resolve(import.meta.dirname, '..')
  execFileSync('git', ['fetch', '--no-tags', '--depth=1', 'origin', base], {
    cwd: repositoryRoot,
    stdio: 'inherit',
  })
  const changed = execFileSync(
    'git',
    ['diff', '--no-renames', '--name-only', '-z', base, revision],
    {
      cwd: repositoryRoot,
      encoding: 'utf8',
    },
  )
    .split('\0')
    .filter(Boolean)
  const selected = shouldRunWindowsMauiBuild(changed)
  console.log(`Windows MAUI build changed-file route: ${selected ? 'run' : 'skip'}`)
  if (process.env.GITHUB_OUTPUT) {
    appendFileSync(process.env.GITHUB_OUTPUT, `windows-maui=${selected}\n`)
  }
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) main()
