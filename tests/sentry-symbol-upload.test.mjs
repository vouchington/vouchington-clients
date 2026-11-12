import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { test } from 'node:test'

const workflow = readFileSync(
  new URL('../.github/workflows/sentry-symbols.yml', import.meta.url),
  'utf8',
)
const uploadScript = workflow
  .split('      - name: Upload debug files when configured')[1]
  .split('        run: |\n')[1]
  .split('\n')
  .map(line => line.slice(10))
  .join('\n')

test('symbol upload skips absent credentials and propagates configured upload failures', () => {
  const root = mkdtempSync(join(tmpdir(), 'sentry-upload-'))
  try {
    const cli = join(root, 'sentry-cli')
    writeFileSync(cli, '#!/bin/bash\nprintf "%s\\n" "$@" > "$RUNNER_TEMP/arguments"\nexit 19\n', {
      mode: 0o700,
    })
    const env = {
      PATH: process.env.PATH,
      RUNNER_TEMP: root,
      SENTRY_AUTH_TOKEN: 'secret-token-fixture',
      SENTRY_ORG: 'fixture',
      SENTRY_PROJECT: 'fixture',
    }
    for (const missing of ['SENTRY_AUTH_TOKEN', 'SENTRY_ORG', 'SENTRY_PROJECT']) {
      const result = spawnSync('bash', ['-e', '-c', uploadScript], {
        env: { ...env, [missing]: '' },
        encoding: 'utf8',
      })
      assert.equal(result.status, 0)
      assert.doesNotMatch(result.stdout + result.stderr, /secret-token-fixture/)
      assert.throws(() => readFileSync(join(root, 'arguments')), { code: 'ENOENT' })
    }
    const failed = spawnSync('bash', ['-e', '-c', uploadScript], { env, encoding: 'utf8' })
    assert.equal(failed.status, 19)
    assert.doesNotMatch(failed.stdout + failed.stderr, /secret-token-fixture/)
    writeFileSync(cli, '#!/bin/bash\nexit 0\n', { mode: 0o700 })
    assert.equal(spawnSync('bash', ['-e', '-c', uploadScript], { env }).status, 0)
    assert.equal(
      readFileSync(join(root, 'arguments'), 'utf8'),
      `debug-files\nupload\n${root}/sentry-symbols\n`,
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('only the protected upload step receives credentials and downloads same-run artifacts', () => {
  assert.match(workflow, /environment: sentry-release/)
  assert.equal((workflow.match(/secrets\.SENTRY_AUTH_TOKEN/g) || []).length, 1)
  assert.doesNotMatch(
    workflow,
    /pull_request_target|workflow_run|run-id:|repository:|include-sources|continue-on-error/,
  )
  assert.match(workflow, /github\.event_name == 'push'.*github\.ref == 'refs\/heads\/main'/)
  assert.match(
    workflow,
    /github\.event_name == 'workflow_dispatch' && github\.ref == 'refs\/heads\/main'/,
  )
  assert.match(workflow, /sha256sum --check --status/)
})

test('upload gate accepts main and release events and rejects PR or branch dispatches', () => {
  const gate = workflow.split('    if: >-\n')[1].split('    runs-on:')[0].trim()
  const accepts = (eventName, ref) =>
    Function(
      'github',
      'startsWith',
      `return ${gate}`,
    )({ event_name: eventName, ref }, (value, prefix) => value.startsWith(prefix))
  for (const [event, ref, expected] of [
    ['pull_request', 'refs/heads/main', false],
    ['pull_request', 'refs/tags/ios-v1.0.0', false],
    ['push', 'refs/heads/feature', false],
    ['workflow_dispatch', 'refs/heads/feature', false],
    ['workflow_dispatch', 'refs/tags/ios-v1.0.0', false],
    ['push', 'refs/heads/main', true],
    ['push', 'refs/tags/ios-v1.0.0', true],
    ['workflow_dispatch', 'refs/heads/main', true],
  ]) {
    assert.equal(accepts(event, ref), expected, `${event} ${ref}`)
  }
})

test('every caller isolates symbol collection and upload from pull requests', () => {
  for (const [filename, platform, job] of [
    ['release-ios-testflight.yml', 'ios', 'sentry-symbols'],
    ['release-macos-unsigned.yml', 'macos', 'sentry-symbols'],
    ['native-contract-tests.yml', 'dotnet', 'sentry-dotnet-symbols'],
  ]) {
    const source = readFileSync(
      new URL(`../.github/workflows/${filename}`, import.meta.url),
      'utf8',
    )
    assert.doesNotMatch(source, /secrets\.SENTRY_AUTH_TOKEN/)
    const caller = source.split(`  ${job}:\n`)[1].split(/\n  [\w-]+:\n/)[0]
    assert.match(caller, /permissions:\n      contents: read\n      actions: read/)
    const artifact = `sentry-${platform}-\${{ github.run_id }}-\${{ github.run_attempt }}`
    assert.ok(caller.includes(`artifact: ${artifact}`))
    assert.ok(source.includes(`name: ${artifact}`))
    const gateExpressions = [
      ...source.matchAll(
        /      - name: (?:Retain trusted[^\n]*|Collect trusted[^\n]*|Validate trusted[^\n]*)\n        if: ([^\n]+)/g,
      ),
    ].map(match => match[1])
    gateExpressions.push(caller.match(/    if: ([^\n]+)/)[1])
    assert.ok(gateExpressions.length >= 2)
    for (const gate of gateExpressions) {
      const expression = gate.replace(/^\$\{\{\s*|\s*\}\}$/g, '')
      const evaluate = (event, ref) =>
        Function(
          'github',
          'startsWith',
          `return ${expression}`,
        )({ event_name: event, ref }, (value, prefix) => value.startsWith(prefix))
      assert.equal(evaluate('pull_request', 'refs/heads/main'), false)
      assert.equal(evaluate('pull_request', 'refs/tags/ios-v1.0.0'), false)
      assert.equal(evaluate('push', 'refs/heads/feature'), false)
      assert.equal(evaluate('workflow_dispatch', 'refs/heads/feature'), false)
      assert.equal(
        evaluate(platform === 'dotnet' ? 'push' : 'workflow_dispatch', 'refs/heads/main'),
        true,
      )
    }
    const dsnExpression = source.match(/VOUCHA_SENTRY_DSN: \$\{\{ (.*?) \}\}/)[1]
    const evaluateDsn = (event, ref) =>
      Function(
        'github',
        'vars',
        'startsWith',
        `return ${dsnExpression}`,
      )(
        { event_name: event, ref },
        { [`VOUCHA_SENTRY_DSN_${platform.toUpperCase()}`]: 'public-dsn' },
        (value, prefix) => value.startsWith(prefix),
      )
    assert.equal(evaluateDsn('pull_request', 'refs/heads/main'), '')
    assert.equal(evaluateDsn('workflow_dispatch', 'refs/heads/topic'), '')
    assert.equal(evaluateDsn('push', 'refs/heads/topic'), '')
    assert.equal(
      evaluateDsn(platform === 'dotnet' ? 'push' : 'workflow_dispatch', 'refs/heads/main'),
      'public-dsn',
    )
    if (platform !== 'dotnet') {
      const ancestry = source.indexOf('git merge-base --is-ancestor HEAD origin/main')
      assert.ok(ancestry > 0 && ancestry < source.indexOf('xcodebuild -project'))
      assert.match(source, /fetch-depth: 0/)
    }
  }
})
