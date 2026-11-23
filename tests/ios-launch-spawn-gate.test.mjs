import assert from 'node:assert/strict'
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawn, spawnSync } from 'node:child_process'
import { describe, it } from 'node:test'

function run(terminateBehavior, uninstallBehavior, launchBehavior = 'true', pid = 4242) {
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
  launch) ${launchBehavior}; echo "ai.voucha.ios: ${pid}" ;;
  terminate) ${terminateBehavior} ;;
  uninstall) echo 'uninstall boom' >&2; ${uninstallBehavior} ;;
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

function startChild() {
  return spawn('sleep', ['60'], { stdio: 'ignore' })
}

function deadPid() {
  const child = spawnSync('true')
  return child.pid
}

describe('ios launch spawn gate', () => {
  it('stops the app when terminate succeeds', () => {
    const result = run('exit 0', 'exit 0')
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /stopped/u)
  })

  it('resets the app when terminate hangs and the host process is still alive', () => {
    const child = startChild()
    try {
      const result = run('sleep 30', 'exit 0', 'true', child.pid)
      assert.equal(result.status, 0, result.stderr)
      assert.match(result.stdout, /terminate-budget-exhausted/u)
      assert.match(result.stdout, new RegExp(`alive pid=${child.pid}`, 'u'))
      assert.match(result.stdout, /reset-install/u)
      assert.deepEqual(result.calls, ['install', 'launch', 'terminate', 'uninstall', 'install'])
    } finally {
      child.kill()
    }
  })

  it('continues when terminate hangs but the host process is gone', () => {
    const result = run('sleep 30', 'exit 1', 'true', deadPid())
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /stopped-late/u)
    assert.deepEqual(result.calls, ['install', 'launch', 'terminate'])
  })

  it('fails closed when the process cannot be signalled', { skip: process.getuid() === 0 }, () => {
    // pid 1 exists but an unprivileged caller gets EPERM, which must count as alive.
    const result = run('sleep 30', 'exit 1', 'true', 1)
    assert.equal(result.status, 1)
    assert.doesNotMatch(result.stdout, /stopped-late/u)
    assert.deepEqual(result.calls, ['install', 'launch', 'terminate', 'uninstall'])
  })

  it('fails closed and logs the failing command when recovery fails', () => {
    const child = startChild()
    try {
      const result = run('sleep 30', 'exit 1', 'true', child.pid)
      assert.equal(result.status, 1)
      assert.match(result.stderr, /Could not stop the pre-launched app/u)
      assert.match(
        result.stdout,
        /::warning::spawn-gate reset uninstall failed exit=1 after \ds: uninstall boom/u,
      )
      assert.deepEqual(result.calls, ['install', 'launch', 'terminate', 'uninstall'])
      assert.doesNotMatch(result.stdout, /erase/u)
    } finally {
      child.kill()
    }
  })

  it('keeps the stderr a reset command wrote before it hung', () => {
    const child = startChild()
    try {
      const result = run('sleep 30', 'sleep 30', 'true', child.pid)
      assert.equal(result.status, 1)
      assert.match(
        result.stdout,
        /::warning::spawn-gate reset uninstall timed out after \ds of the 3s budget: uninstall boom/u,
      )
      assert.deepEqual(result.calls, ['install', 'launch', 'terminate', 'uninstall'])
    } finally {
      child.kill()
    }
  })

  it('gives terminate its own budget when install and launch used the shared one', () => {
    const result = run('sleep 2', 'exit 0', 'sleep 2')
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /stopped/u)
    assert.doesNotMatch(result.stdout, /terminate-budget-exhausted/u)
  })
})
