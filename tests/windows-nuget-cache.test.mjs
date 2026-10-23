import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const jobs = [
  [
    'image-preview',
    'Test the Windows image decoder',
    'candidate-clients/dotnet-clients/tests/Voucha.Client.WindowsImagePreview.Tests/packages.lock.json',
  ],
  [
    'maui',
    'Compile the actual Windows MAUI app',
    'candidate-clients/dotnet-clients/src/**/*.lock.json',
  ],
  [
    'language-model-surface',
    'Probe the pinned Windows language-model surface',
    'candidate-clients/dotnet-clients/tests/Voucha.Client.WindowsLanguageModelSurface.Tests/packages.lock.json',
  ],
]

for (const [suffix, finalCheck, lock] of jobs) {
  test(`Windows ${suffix} restores existing keys and saves only a successful main miss`, async () => {
    const workflow = await readFile(
      new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
      'utf8',
    )
    const job = workflow.split(`  dotnet-windows-${suffix}:\n`)[1].split(/^  [a-z][^\n]*:\n/mu)[0]
    const steps = job.split('      - name: ')
    const restore = steps.find(step => step.startsWith('Restore NuGet packages\n'))
    const save = steps.find(step => step.startsWith('Save NuGet packages from main\n'))
    assert.ok(restore && save)
    assert.doesNotMatch(
      job,
      /uses: actions\/cache@/u,
      'combined cache action would save PR copies in its post action',
    )
    assert.doesNotMatch(restore, /\n\s+if:/u)
    const id = `windows-${suffix}-nuget-cache`
    assert.ok(restore.includes(`id: ${id}\n`))
    assert.match(restore, /uses: actions\/cache\/restore@[0-9a-f]{40} # v[\d.]+/u)
    assert.match(save, /uses: actions\/cache\/save@[0-9a-f]{40} # v[\d.]+/u)
    for (const path of [
      '${{ runner.temp }}/voucha-windows-nuget-packages',
      '${{ runner.temp }}/voucha-windows-nuget-http-cache',
    ]) {
      assert.ok(restore.includes(path) && save.includes(path))
    }
    const key =
      '${{ runner.os }}-nuget-windows-' +
      suffix +
      "-${{ hashFiles('" +
      lock +
      "', 'candidate-clients/dotnet-clients/Directory.Packages.props', 'candidate-clients/global.json') }}"
    assert.ok(restore.includes(`key: ${key}\n`), 'exact key inputs changed')
    assert.ok(
      restore.includes(`restore-keys: |\n            \${{ runner.os }}-nuget-windows-${suffix}-\n`),
    )
    assert.ok(save.includes(`key: \${{ steps.${id}.outputs.cache-primary-key }}`))
    assert.match(save, /timeout-minutes: 3/u)
    assert.match(save, /continue-on-error: true/u)
    assert.ok(job.indexOf(finalCheck) < job.indexOf('Save NuGet'))
    assert.equal(steps.at(-1), save, 'save must follow all validation steps')

    const condition = save.match(/if: \$\{\{ (.+) \}\}/u)[1]
    assert.ok(condition.includes('success()'), 'failures and cancellations must never save')
    const expression = condition.replace(`steps.${id}.outputs.cache-hit`, 'cacheHit')
    const permitsSave = new Function('success', 'github', 'cacheHit', `return (${expression})`)
    for (const event of ['push', 'pull_request', 'workflow_dispatch']) {
      for (const ref of ['refs/heads/main', 'refs/heads/feature', 'refs/pull/484/merge']) {
        for (const hit of ['true', 'false', '']) {
          for (const outcome of ['success', 'failure', 'cancelled', 'skipped']) {
            assert.equal(
              permitsSave(() => outcome === 'success', { event_name: event, ref }, hit),
              event === 'push' &&
                ref === 'refs/heads/main' &&
                hit !== 'true' &&
                outcome === 'success',
              `${event} ${ref} hit=${hit} ${outcome}`,
            )
          }
        }
      }
    }
  })
}
