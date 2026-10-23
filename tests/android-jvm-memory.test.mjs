import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const helper = new URL(
  '../swift-clients/apps/android/tooling/measure-jvm-memory.sh',
  import.meta.url,
)

test('temporary JVM sampler bounds rows and samples without changing build status', async t => {
  const root = await mkdtemp(join(tmpdir(), 'android-jvm-memory-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const bin = join(root, 'bin')
  await mkdir(bin)
  const commands = {
    ps: `test "$*" = '-axo pid=,ppid=,rss=,ucomm=' || exit 91
for i in {1..100}; do echo '123 12 456 /tools/java'; done
echo '1 0 20 secret-non-java'
`,
    vm_stat: 'echo "Pages free: 42"',
    sysctl: 'echo "$*: 2048"',
    sleep: 'exit 0',
  }
  for (const [name, body] of Object.entries(commands)) {
    await writeFile(join(bin, name), `#!/bin/bash\n${body}\n`)
    await chmod(join(bin, name), 0o755)
  }
  const env = { ...process.env, RUNNER_TEMP: root, PATH: `${bin}:${process.env.PATH}` }
  const run = command =>
    spawnSync('bash', [helper.pathname, 'bash', '-c', command], {
      env,
      encoding: 'utf8',
      timeout: 15_000,
    })
  const untilBounded = `for ((i=0; i<1000; i++)); do
  grep -q '^sample=180 ' "$RUNNER_TEMP/android-jvm-memory.log" && exit 23
  /bin/sleep 0.01
done
exit 92`
  const result = run(untilBounded)
  assert.equal(result.status, 23, result.stderr)
  const log = await readFile(join(root, 'android-jvm-memory.log'), 'utf8')
  assert.equal((log.match(/^sample=/gmu) ?? []).length, 180)
  const rows = (log.match(/^123 12 456 java$/gmu) ?? []).length
  assert.ok(rows >= 64 && rows <= 180 * 64)
  assert.doesNotMatch(log, /secret-non-java|sample=181/u)
  assert.match(log, /Pages free: 42/u)

  await writeFile(
    join(bin, 'sleep'),
    '#!/bin/bash\necho $$ > "$RUNNER_TEMP/sleeper"\nexec /bin/sleep 30\n',
  )
  const failure = run(`while [[ ! -s "$RUNNER_TEMP/sleeper" ]]; do /bin/sleep 0.01; done; exit 19`)
  assert.equal(failure.status, 19, failure.stderr)
  const sleeper = Number((await readFile(join(root, 'sleeper'), 'utf8')).trim())
  assert.throws(() => process.kill(sleeper, 0), { code: 'ESRCH' })

  for (const name of ['ps', 'vm_stat', 'sysctl']) {
    await writeFile(
      join(bin, name),
      `#!/bin/bash\ntouch "$RUNNER_TEMP/missing-${name}"\nexit 127\n`,
    )
  }
  const missingTools = `while [[ ! -e "$RUNNER_TEMP/missing-vm_stat" ]]; do /bin/sleep 0.01; done`
  assert.equal(run(`${missingTools}; exit 0`).status, 0)
  assert.equal(run(`${missingTools}; exit 29`).status, 29)
})
