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

describe('MAUI App test diagnostics', () => {
  it('keeps the full Catalyst App suite and saves bounded hang diagnostics on failure', () => {
    const workflow = readFileSync(
      new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
      'utf8',
    )
    const maui = jobBlock(workflow, 'dotnet-maui')

    assert.match(maui, /timeout-minutes: 30/u)
    assert.match(maui, /name: Test rendered MAUI pages\n\s+timeout-minutes: 18/u)
    assert.match(
      maui,
      /dotnet test dotnet-clients\/tests\/Voucha\.Client\.App\.Tests\/Voucha\.Client\.App\.Tests\.csproj\n\s+--configuration Release --no-build[\s\S]*--logger "console;verbosity=detailed"[\s\S]*--blame-hang --blame-hang-timeout 2m --blame-hang-dump-type mini/u,
    )
    assert.match(maui, /--results-directory "\$RUNNER_TEMP\/voucha-maui-test-results"/u)
    assert.match(
      maui,
      /name: Upload failed MAUI test diagnostics\n\s+if: \$\{\{ failure\(\) \|\| cancelled\(\) \}\}[\s\S]*?timeout-minutes: 2[\s\S]*?actions\/upload-artifact@[a-f0-9]{40}[\s\S]*?voucha-maui-test-results/u,
    )
    assert.match(maui, /retention-days: 1/u)
    assert.ok(
      maui.indexOf('name: Test rendered MAUI pages') <
        maui.indexOf('name: Upload failed MAUI test diagnostics'),
    )
    assert.ok(
      maui.indexOf('name: Upload failed MAUI test diagnostics') <
        maui.indexOf('name: Select compatible Xcode'),
    )
  })
})
