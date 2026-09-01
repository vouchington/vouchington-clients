import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { cp, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import test from 'node:test'

const execFileAsync = promisify(execFile)
const repositoryRoot = fileURLToPath(new URL('..', import.meta.url))

test('dev/clean removes native output and preserves dependencies and source', async () => {
  const fixture = await mkdtemp(join(tmpdir(), 'vouchington-clients-clean-'))
  try {
    await mkdir(join(fixture, 'dev'), { recursive: true })
    await cp(join(repositoryRoot, 'dev/clean'), join(fixture, 'dev/clean'))
    await mkdir(join(fixture, 'dev/lib'), { recursive: true })
    await cp(
      join(repositoryRoot, 'dev/lib/clean-build-output.sh'),
      join(fixture, 'dev/lib/clean-build-output.sh'),
    )
    const removedDirectories = [
      'coverage',
      'swift-clients/core/.build',
      'swift-clients/apps/iOS/Voucha.xcodeproj',
      'swift-clients/apps/iOS/Voucha.xcworkspace',
      'swift-clients/apps/android/Android/app/build',
      'dotnet-clients/src/Voucha.Client.Core/bin',
      'dotnet-clients/src/Voucha.Client.Core/obj',
      'dotnet-clients/TestResults',
      'scripts/.cache',
    ]
    for (const directory of removedDirectories) {
      await mkdir(join(fixture, directory), { recursive: true })
      await writeFile(join(fixture, directory, 'artifact'), 'generated')
    }
    await mkdir(join(fixture, 'node_modules/.cache'), { recursive: true })
    await writeFile(join(fixture, 'node_modules/.cache/preserved'), 'dependency cache')
    await mkdir(join(fixture, 'dotnet-clients/src/Voucha.Client.Core'), { recursive: true })
    await writeFile(join(fixture, 'dotnet-clients/src/Voucha.Client.Core/source.cs'), 'source')

    await execFileAsync('bash', [join(fixture, 'dev/clean')])

    for (const directory of removedDirectories) {
      await assert.rejects(readFile(join(fixture, directory, 'artifact')))
    }
    assert.equal(
      await readFile(join(fixture, 'node_modules/.cache/preserved'), 'utf8'),
      'dependency cache',
    )
    assert.equal(
      await readFile(join(fixture, 'dotnet-clients/src/Voucha.Client.Core/source.cs'), 'utf8'),
      'source',
    )
  } finally {
    await rm(fixture, { recursive: true, force: true })
  }
})

test('dev commands expose strict help and reset safety boundaries', async () => {
  const cleanHelp = await execFileAsync('bash', [join(repositoryRoot, 'dev/clean'), '--help'])
  assert.match(cleanHelp.stdout, /Usage: \.\/dev\/clean/u)

  const resetHelp = await execFileAsync('bash', [
    join(repositoryRoot, 'dev/reset-worktree'),
    '--help',
  ])
  assert.match(resetHelp.stdout, /disposable Git worktree/u)

  const resetSource = await readFile(join(repositoryRoot, 'dev/reset-worktree'), 'utf8')
  assert.match(resetSource, /git_dir.*common_dir/su)
  assert.match(resetSource, /status --porcelain --untracked-files=normal/u)
  assert.match(resetSource, /switch --discard-changes.*origin\/main/u)
  assert.match(resetSource, /pnpm --dir.*install --frozen-lockfile/u)
})
