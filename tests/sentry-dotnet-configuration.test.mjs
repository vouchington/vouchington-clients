import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import { describe, it } from 'node:test'

const root = resolve(import.meta.dirname, '..')

describe('dotnet Sentry configuration', () => {
  it('packages release and environment build inputs for the installed SDK', async () => {
    const appRoot = join(root, 'dotnet-clients/src/Voucha.Client.App')
    const project = await readFile(join(appRoot, 'Voucha.Client.App.csproj'), 'utf8')
    const targets = await readFile(join(appRoot, 'SentryDsn.targets'), 'utf8')
    const startup = await readFile(join(appRoot, 'MauiProgram.Sentry.cs'), 'utf8')
    for (const [key, property] of [
      ['VOUCHA_SENTRY_RELEASE', 'VouchaSentryRelease'],
      ['VOUCHA_SENTRY_ENVIRONMENT', 'VouchaSentryEnvironment'],
    ]) {
      assert.ok(project.includes(`<AssemblyMetadata Include="${key}" Value="$(${property})" />`))
      assert.ok(targets.includes(`>$(${key})</${property}>`))
      assert.ok(startup.includes(`"${key}"`))
    }
  })

  it('scrubs performance transactions before send', async () => {
    const options = await readFile(
      join(root, 'dotnet-clients/src/Voucha.Client.App/MauiProgram.Sentry.cs'),
      'utf8',
    )
    const projection = await readFile(
      join(root, 'dotnet-clients/src/Voucha.Client.App/SentryTransactionProjection.cs'),
      'utf8',
    )
    const eventProjection = await readFile(
      join(root, 'dotnet-clients/src/Voucha.Client.App/SentryEventProjection.cs'),
      'utf8',
    )
    assert.match(options, /SetBeforeSend\(SentryEventProjection\.Scrub\)/u)
    assert.match(options, /SetBeforeSendTransaction\(SentryTransactionProjection\.Scrub\)/u)
    assert.match(projection, /ScrubAddress\(span\.Description\) \?\? ""/u)
    assert.match(projection, /QueryString = null/u)
    assert.match(projection, /KeepsNumber\(key\)/u)
    assert.match(projection, /RetainAccountMarker\(user, account\)/u)
    assert.match(eventProjection, /extra\[key\] = value/u)
    assert.match(eventProjection, /extra\.Remove\(key\)/u)
  })
})
