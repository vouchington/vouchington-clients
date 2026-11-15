import assert from 'node:assert/strict'
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { describe, it } from 'node:test'

function run(terminateBehavior, uninstallExit) {
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
  launch) echo "ai.voucha.ios: 4242" ;;
  terminate) ${terminateBehavior} ;;
  uninstall) exit ${uninstallExit} ;;
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
text = text.replace("RECOVERY_SECONDS = 60", "RECOVERY_SECONDS = 3")
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

describe('ios launch spawn gate', () => {
  it('stops the app when terminate succeeds', () => {
    const result = run('exit 0', 0)
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /stopped/u)
  })

  it('resets the app when terminate hangs', () => {
    const result = run('sleep 30', 0)
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /terminate-budget-exhausted/u)
    assert.match(result.stdout, /reset/u)
    assert.deepEqual(result.calls, ['install', 'launch', 'terminate', 'uninstall', 'install'])
  })

  it('fails closed when terminate hangs and the reset fails', () => {
    const result = run('sleep 30', 1)
    assert.equal(result.status, 1)
    assert.match(result.stderr, /Could not stop the pre-launched app/u)
  })
})
