import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

test('Mac Catalyst restores NuGet on every run and saves a completed main-only miss', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const maui = workflow.split('  dotnet-maui:\n')[1].split('  dotnet-windows-image-preview:\n')[0]
  const restore = maui.split('      - name: Restore NuGet packages\n')[1].split('      - name: ')[0]
  const save = maui.split('      - name: Save NuGet packages from main\n')[1]
  const paths = [
    '${{ runner.temp }}/voucha-dotnet-cache',
    '${{ github.workspace }}/candidate-clients/.nuget/packages',
    '${{ github.workspace }}/candidate-clients/.nuget/http-cache',
    '${{ github.workspace }}/candidate-clients/.nuget/plugins-cache',
  ]

  assert.match(restore, /id: maui-nuget-cache\n\s+uses: actions\/cache\/restore@[0-9a-f]{40}/u)
  assert.match(save, /uses: actions\/cache\/save@[0-9a-f]{40}/u)
  for (const path of paths) {
    assert.ok(restore.includes(path), `restore omits ${path}`)
    assert.ok(save.includes(path), `save omits ${path}`)
  }
  assert.match(restore, /key: \$\{\{ runner\.os \}\}-nuget-maui-\$\{\{ hashFiles\(/u)
  assert.match(save, /key: \$\{\{ steps\.maui-nuget-cache\.outputs\.cache-primary-key \}\}/u)
  assert.match(
    save,
    /if: \$\{\{ github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.maui-nuget-cache\.outputs\.cache-hit != 'true' \}\}/u,
  )
  assert.match(save, /timeout-minutes: 3/u)
  assert.ok(maui.indexOf('Verify all eleven committed NuGet locks') < maui.indexOf('Save NuGet'))
  assert.ok(maui.indexOf('Test rendered MAUI pages') < maui.indexOf('Save NuGet'))
  assert.ok(maui.indexOf('Build MAUI Mac Catalyst app') < maui.indexOf('Save NuGet'))
})
