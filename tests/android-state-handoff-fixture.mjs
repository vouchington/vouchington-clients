import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmod, mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

export async function androidStateFixture(t) {
  const root = await mkdtemp(join(tmpdir(), 'android-state-handoff-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const packageRoot = join(root, 'checkout/swift-clients/apps/android')
  const checkoutRoot = join(root, 'checkout')
  const cacheRoot = join(root, 'cache')
  const artifactRoot = join(root, 'artifact')
  await mkdir(join(packageRoot, '.build/plugins/outputs'), { recursive: true })
  await mkdir(join(packageRoot, 'Android/.gradle'), { recursive: true })
  await mkdir(cacheRoot)
  await writeFile(join(packageRoot, '.build/plugins/outputs/native.o'), 'native')
  await chmod(join(packageRoot, '.build/plugins/outputs/native.o'), 0o755)
  await writeFile(join(packageRoot, 'Android/.gradle/task-state'), 'task-state')
  await writeFile(join(cacheRoot, 'cache-entry'), 'cache')
  await writeFile(join(checkoutRoot, 'swift-clients/Package.swift'), '// tracked Swift input\n')
  const gitInit = spawnSync('git', ['init', '-q', checkoutRoot], { encoding: 'utf8' })
  assert.equal(gitInit.status, 0, gitInit.stderr)
  const gitAdd = spawnSync('git', ['-C', checkoutRoot, 'add', 'swift-clients/Package.swift'], {
    encoding: 'utf8',
  })
  assert.equal(gitAdd.status, 0, gitAdd.stderr)
  const gitCommit = spawnSync(
    'git',
    [
      '-C',
      checkoutRoot,
      '-c',
      'user.name=Android State Test',
      '-c',
      'user.email=android-state@example.invalid',
      'commit',
      '-qm',
      'fixed test candidate',
    ],
    {
      encoding: 'utf8',
      env: {
        ...process.env,
        GIT_AUTHOR_DATE: '2020-01-01T00:00:00Z',
        GIT_COMMITTER_DATE: '2020-01-01T00:00:00Z',
      },
    },
  )
  assert.equal(gitCommit.status, 0, gitCommit.stderr)
  const gitHead = spawnSync('git', ['-C', checkoutRoot, 'rev-parse', 'HEAD'], { encoding: 'utf8' })
  assert.equal(gitHead.status, 0, gitHead.stderr)
  const identity = { revision: gitHead.stdout.trim(), run: '123', attempt: '2', gradle: '9.4.1' }
  return { root, packageRoot, checkoutRoot, cacheRoot, artifactRoot, identity }
}
