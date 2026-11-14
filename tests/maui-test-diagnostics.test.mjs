import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'
import { jobBlock as readJobBlock, stepBlock as readStepBlock } from './workflow-job-block.mjs'

const jobBlock = (workflow, job) => readJobBlock(workflow, job, `missing workflow job ${job}`)
const stepBlock = (job, name) => readStepBlock(job, name, `missing workflow step ${name}`)

describe('MAUI App test diagnostics', () => {
  it('keeps the full Catalyst App suite and saves bounded hang diagnostics on failure', () => {
    const workflow = readFileSync(
      new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
      'utf8',
    )
    const maui = jobBlock(workflow, 'dotnet-maui-tests')
    const test = stepBlock(maui, 'Test rendered MAUI pages')
    const upload = stepBlock(maui, 'Upload failed MAUI test diagnostics')

    assert.match(maui, /timeout-minutes: 10/u)
    assert.match(test, /timeout-minutes: 18/u)
    const testCommand = test.replace(/\\\n\s*/gu, ' ').replace(/\s+/gu, ' ')
    assert.match(
      testCommand,
      /dotnet test dotnet-clients\/tests\/Voucha\.Client\.App\.Tests\/Voucha\.Client\.App\.Tests\.csproj --configuration Release --no-build --logger "console;verbosity=detailed" --blame-hang --blame-hang-timeout 2m --blame-hang-dump-type mini/u,
    )
    assert.doesNotMatch(testCommand, /--filter(?:\s|=)/u)
    assert.match(
      test,
      /VSTEST_DUMP_PATH: \$\{\{ runner\.temp \}\}\/voucha-maui-test-results\/dumps/u,
    )
    assert.match(
      test,
      /VSTEST_DUMP_TEMP_PATH: \$\{\{ runner\.temp \}\}\/voucha-maui-test-results\/tmp/u,
    )
    assert.match(
      test,
      /VSTEST_DIAG: \$\{\{ runner\.temp \}\}\/voucha-maui-test-results\/vstest\.log/u,
    )
    assert.match(test, /mkdir -p "\$VSTEST_DUMP_PATH" "\$VSTEST_DUMP_TEMP_PATH"/u)
    assert.match(test, /--results-directory "\$RUNNER_TEMP\/voucha-maui-test-results"/u)
    assert.match(
      upload,
      /if: \$\{\{ failure\(\) \|\| cancelled\(\) \}\}[\s\S]*?timeout-minutes: 2[\s\S]*?actions\/upload-artifact@[a-f0-9]{40}[\s\S]*?voucha-maui-test-results/u,
    )
    assert.match(upload, /path: \$\{\{ runner\.temp \}\}\/voucha-maui-test-results/u)
    assert.match(upload, /retention-days: 1/u)
    assert.ok(
      maui.indexOf('      - name: Test rendered MAUI pages') <
        maui.indexOf('      - name: Upload failed MAUI test diagnostics'),
    )
    assert.doesNotMatch(maui, /name: Select compatible Xcode|name: Build MAUI Mac Catalyst app/u)
  })
})
