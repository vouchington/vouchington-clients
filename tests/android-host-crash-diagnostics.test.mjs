import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtemp, mkdir, readFile, rm, symlink, utimes, writeFile } from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'

const workflow = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
const host = workflow
  .split('  test-swift-android-host:\n')[1]
  .split('  test-swift-android-cookie:\n')[0]
const collector = host.split("python3 - <<'PYTHON'\n")[1].split('          PYTHON')[0]
const python = collector.replaceAll(/^ {10}/gmu, '')
const bundle = 'voucha-androidPackageTests'
const imagePath = `/tmp/${bundle}.xctest/Contents/MacOS/${bundle}`
const ips = (process = 'xctest', name = bundle) =>
  `${JSON.stringify({ app_name: process })}\n${JSON.stringify({ procName: process, usedImages: [{ name, path: imagePath }] })}`
const crash = `Process: xctest [42]\nBinary Images:\n0x1 - 0x2 ${bundle} arm64 ${imagePath}\n`

async function fixture(t) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'android-host-crash-test-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const reports = path.join(root, 'Library/Logs/DiagnosticReports')
  await mkdir(reports, { recursive: true })
  const marker = path.join(root, 'android-host-test-start-marker')
  await writeFile(marker, '')
  await utimes(marker, 100, 100)
  return {
    root,
    reports,
    async report(name, content, old = false) {
      const file = path.join(reports, name)
      await writeFile(file, content)
      await utimes(file, old ? 99 : 101, old ? 99 : 101)
      return file
    },
    async collect() {
      // Only the test harness replaces waiting; production remains six bounded local scans.
      const stdout = execFileSync(
        'python3',
        ['-c', `import time\ntime.sleep = lambda _: None\n${python}`],
        {
          env: {
            ...process.env,
            HOME: root,
            RUNNER_TEMP: root,
            CANDIDATE_REVISION: 'candidate-sha',
            WORKFLOW_RUN_ID: '123',
            WORKFLOW_RUN_ATTEMPT: '2',
          },
          encoding: 'utf8',
        },
      )
      const output = path.join(root, 'android-host-crash-reports')
      return {
        stdout,
        output,
        provenance: JSON.parse(await readFile(path.join(output, 'provenance.json'))),
      }
    },
  }
}

test('host diagnostics preserve the original fatal test invocation and bounded failure-only steps', () => {
  const original = host
    .split('      - name: Test Android Swift package\n')[1]
    .split('      - name: ')[0]
  assert.match(host, /timeout-minutes: 20\n    steps:/u)
  assert.ok(host.includes('select-xcode.sh 26.6'))
  assert.ok(host.includes('MISE_EXEC_AUTO_INSTALL=false mise exec swift@6.3.3'))
  assert.match(original, /id: android-host-tests/u)
  assert.match(original, /SWIFT_BACKTRACE: enable=yes,interactive=no/u)
  assert.ok(
    original.includes('bash swift-clients/apps/android/tooling/run-android-host-swift-tests.sh'),
  )
  assert.ok(
    original.includes(
      '--package-path swift-clients/apps/android\n          --filter VouchaAndroidTests',
    ),
  )
  assert.doesNotMatch(original, /continue-on-error|\|\| true|retry|timeout-minutes/u)
  assert.ok(
    host.indexOf('Record Android host test start marker') <
      host.indexOf('Test Android Swift package'),
  )
  assert.match(host, /touch "\$RUNNER_TEMP\/android-host-test-start-marker"/u)
  for (const [name, minutes] of [
    ['Collect', 2],
    ['Upload', 1],
  ]) {
    const step = host
      .split(`      - name: ${name} Android host xctest crash reports\n`)[1]
      .split('      - name: ')[0]
    assert.ok(
      step.includes("if: ${{ failure() && steps.android-host-tests.outcome == 'failure' }}"),
    )
    assert.ok(step.includes(`timeout-minutes: ${minutes}`))
  }
  assert.ok(
    host.includes(
      'name: android-host-crash-reports-${{ needs.verify.outputs.revision-sha }}-${{ github.run_id }}-${{ github.run_attempt }}',
    ),
  )
  assert.match(python, /range\(6\)/u)
  assert.match(python, /time\.sleep\(5\)/u)
})

test('collector copies only new matching xctest bundle reports and binds artifact provenance', async t => {
  const f = await fixture(t)
  await f.report('target.ips', ips())
  await f.report('target.crash', crash)
  await f.report('old.ips', ips(), true)
  await f.report('foreign.ips', ips('xctest', 'ForeignTests'))
  await f.report('other-process.ips', ips('other-process'))
  await f.report('foreign.crash', crash.replaceAll(bundle, 'ForeignTests'))
  await f.report('invalid.ips', '{invalid')
  await f.report('not-report.txt', ips())
  await symlink(path.join(f.reports, 'target.ips'), path.join(f.reports, 'symlink.ips'))
  const result = await f.collect()
  assert.deepEqual(result.provenance.reports.sort(), ['target.crash', 'target.ips'])
  assert.equal(result.provenance.candidate_revision, 'candidate-sha')
  assert.equal(result.provenance.run_id, '123')
  assert.equal(result.provenance.run_attempt, '2')
  assert.equal(result.provenance.process, 'xctest')
  assert.equal(result.provenance.bundle, `${bundle}.xctest`)
  assert.equal(await readFile(path.join(result.output, 'target.crash'), 'utf8'), crash)
  assert.equal(await readFile(path.join(result.output, 'target.ips'), 'utf8'), ips())
})

test('missing and oversized crash reports produce diagnostics without changing the original failure', async t => {
  const f = await fixture(t)
  await f.report('oversized.ips', `${ips()}${' '.repeat(8 * 1024 * 1024)}`)
  const result = await f.collect()
  assert.deepEqual(result.provenance.reports, [])
  assert.match(result.stdout, /::warning::No new Android host xctest crash report/u)
  assert.ok(result.provenance.warnings.includes('Diagnostic report byte limit reached'))
})
