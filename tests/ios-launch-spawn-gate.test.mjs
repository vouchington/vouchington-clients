import assert from 'node:assert/strict'
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { describe, it } from 'node:test'

function run(terminateBehavior, uninstallExit, launchBehavior = 'true', spawnBehavior = 'exit 0') {
  const dir = mkdtempSync(join(tmpdir(), 'spawn-gate-'))
  const bin = join(dir, 'bin')
  mkdirSync(bin)
  const app = join(dir, 'Voucha.app')
  mkdirSync(app)
  writeFileSync(join(app, 'Info.plist'), '')
  const log = join(dir, 'calls.log')
  writeFileSync(
    join(bin, 'xcrun'),
    `#!/bin/sh
echo "$2" >> "${log}"
case "$2" in
  launch) ${launchBehavior}; echo "ai.voucha.ios: 4242" ;;
  terminate) ${terminateBehavior} ;;
  spawn) ${spawnBehavior} ;;
  uninstall) echo 'uninstall boom' >&2; exit ${uninstallExit} ;;
esac
exit 0
`,
  )
  writeFileSync(join(bin, 'PlistBuddy'), '#!/bin/sh\necho ai.voucha.ios\n')
  chmodSync(join(bin, 'xcrun'), 0o755)
  chmodSync(join(bin, 'PlistBuddy'), 0o755)
  const source = new URL('../scripts/ios-launch-spawn-gate.py', import.meta.url).pathname
  // Shrink the budgets and redirect PlistBuddy so the gate runs without Xcode.
  const patched = join(dir, 'gate.py')
  writeFileSync(
    patched,
    `import runpy, sys, re
text = open(${JSON.stringify(source)}).read()
text = text.replace("BUDGET_SECONDS = 150", "BUDGET_SECONDS = 3")
text = text.replace("TERMINATE_SECONDS = 60", "TERMINATE_SECONDS = 3")
text = text.replace("RECOVERY_SECONDS = 120", "RECOVERY_SECONDS = 3")
text = text.replace("LIVENESS_SECONDS = 15", "LIVENESS_SECONDS = 1")
text = text.replace("/usr/libexec/PlistBuddy", ${JSON.stringify(join(bin, 'PlistBuddy'))})
exec(compile(text, "gate", "exec"))
`,
  )
  const result = spawnSync('python3', [patched, 'SIM', app], {
    encoding: 'utf8',
    env: { ...process.env, PATH: `${bin}:${process.env.PATH}` },
  })
  return { ...result, calls: readFileSync(log, 'utf8').trim().split('\n') }
}

const alive = 'echo "123\t0\tUIKitApplication:ai.voucha.ios[abcd][rb-legacy]"'

describe('ios launch spawn gate', () => {
  it('stops the app when terminate succeeds', () => {
    const result = run('exit 0', 0)
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /stopped/u)
  })

  it('resets the app when terminate hangs and the app is still alive', () => {
    const result = run('sleep 30', 0, 'true', alive)
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /terminate-budget-exhausted/u)
    assert.match(result.stdout, /reset/u)
    assert.deepEqual(result.calls, [
      'install',
      'launch',
      'terminate',
      'spawn',
      'uninstall',
      'install',
    ])
  })

  it('continues when terminate hangs but the app is gone', () => {
    const result = run('sleep 30', 1, 'true', 'echo "-\t0\tcom.apple.other"')
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /stopped-late/u)
    assert.deepEqual(result.calls, ['install', 'launch', 'terminate', 'spawn'])
  })

  it('fails closed when the liveness check stalls', () => {
    const result = run('sleep 30', 1, 'true', 'sleep 30')
    assert.equal(result.status, 1)
    assert.match(result.stdout, /liveness-timeout/u)
    assert.doesNotMatch(result.stdout, /stopped-late/u)
    assert.deepEqual(
      result.calls,
      ['install', 'launch', 'terminate', 'spawn', 'uninstall'].slice(0, 6),
    )
  })

  it('fails closed without erasing the device when terminate hangs and the app reset fails', () => {
    const result = run('sleep 30', 1, 'true', alive)
    assert.equal(result.status, 1)
    assert.match(result.stderr, /Could not stop the pre-launched app/u)
    assert.match(
      result.stdout,
      /::warning::spawn-gate reset uninstall failed exit=1 after \ds: uninstall boom/u,
    )
    assert.deepEqual(result.calls, ['install', 'launch', 'terminate', 'spawn', 'uninstall'])
    assert.doesNotMatch(result.stdout, /erase/u)
  })

  it('gives terminate its own budget when install and launch used the shared one', () => {
    const result = run('sleep 2', 0, 'sleep 2')
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /stopped/u)
    assert.doesNotMatch(result.stdout, /terminate-budget-exhausted/u)
  })
})
