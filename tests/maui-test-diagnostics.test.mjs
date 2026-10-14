import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'

const jobBlock = (workflow, job) => {
  const start = workflow.indexOf(`  ${job}:`)
  assert.notEqual(start, -1, `missing workflow job ${job}`)
  const remainder = workflow.slice(start)
  const nextJob = remainder.search(/\n {2}[A-Za-z0-9_-]+:/u)
  return remainder.slice(0, nextJob === -1 ? remainder.length : nextJob)
}

const stepBlock = (job, name) => {
  const marker = `      - name: ${name}\n`
  const start = job.indexOf(marker)
  assert.notEqual(start, -1, `missing workflow step ${name}`)
  const remainder = job.slice(start)
  const nextStep = remainder.search(/\n      - name:/u)
  return remainder.slice(0, nextStep === -1 ? remainder.length : nextStep)
}

describe('MAUI App test diagnostics', () => {
  it('keeps the full Catalyst App suite and saves bounded hang diagnostics on failure', () => {
    const workflow = readFileSync(
      new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
      'utf8',
    )
    const maui = jobBlock(workflow, 'dotnet-maui')
    const test = stepBlock(maui, 'Test rendered MAUI pages')
    const upload = stepBlock(maui, 'Upload failed MAUI test diagnostics')

    assert.match(maui, /timeout-minutes: 30/u)
    assert.match(test, /timeout-minutes: 18/u)
    assert.match(
      test,
      /dotnet test dotnet-clients\/tests\/Voucha\.Client\.App\.Tests\/Voucha\.Client\.App\.Tests\.csproj\n\s+--configuration Release --no-build[\s\S]*--logger "console;verbosity=detailed"[\s\S]*--blame-hang --blame-hang-timeout 2m --blame-hang-dump-type mini/u,
    )
    assert.match(test, /--results-directory "\$RUNNER_TEMP\/voucha-maui-test-results"/u)
    assert.match(
      upload,
      /if: \$\{\{ failure\(\) \|\| cancelled\(\) \}\}[\s\S]*?timeout-minutes: 2[\s\S]*?actions\/upload-artifact@[a-f0-9]{40}[\s\S]*?voucha-maui-test-results/u,
    )
    assert.match(upload, /retention-days: 1/u)
    assert.ok(
      maui.indexOf('      - name: Test rendered MAUI pages') <
        maui.indexOf('      - name: Upload failed MAUI test diagnostics'),
    )
    assert.ok(
      maui.indexOf('      - name: Upload failed MAUI test diagnostics') <
        maui.indexOf('      - name: Select compatible Xcode'),
    )
  })
})
