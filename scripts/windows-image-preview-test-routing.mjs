import { appendFileSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
import { resolve } from 'node:path'

const windowsImagePreviewPaths = [
  '.github/actions/prepare-native-contract/action.yml',
  '.github/actions/setup-mise-toolchain/action.yml',
  '.github/workflows/native-contract-tests.yml',
  '.mise.toml',
  'docs/development/ci-runners.md',
  'docs/development/native-ci-test-placement.md',
  'dotnet-clients/AGENTS.md',
  'dotnet-clients/Directory.Build.props',
  'dotnet-clients/Directory.Build.targets',
  'dotnet-clients/Directory.Packages.props',
  'dotnet-clients/NuGet.config',
  'dotnet-clients/src/Voucha.Client.App/Support/LocalImagePreview.cs',
  'dotnet-clients/src/Voucha.Client.App/Support/WindowsLocalImagePreviewDecoder.cs',
  'dotnet-clients/tests/Voucha.Client.WindowsImagePreview.Tests/',
  'dotnet-clients/tooling/restore-locks.sh',
  'global.json',
  'scripts/windows-image-preview-test-routing.mjs',
  'tests/runner-policy.test.mjs',
  'tests/windows-image-preview-test-routing.test.mjs',
]

export function shouldRunWindowsImagePreviewTests(paths) {
  return paths.some(path =>
    windowsImagePreviewPaths.some(prefix =>
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
  const selected = shouldRunWindowsImagePreviewTests(changed)
  console.log(`Windows image preview changed-file route: ${selected ? 'run' : 'skip'}`)
  if (process.env.GITHUB_OUTPUT) {
    appendFileSync(process.env.GITHUB_OUTPUT, `windows-image-preview=${selected}\n`)
  }
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) main()
