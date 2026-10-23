import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import { describe, it } from 'node:test'

const root = resolve(import.meta.dirname, '..')

describe('dotnet Sentry configuration', () => {
  it('scrubs performance transactions before send', async () => {
    const options = await readFile(
      join(root, 'dotnet-clients/src/Voucha.Client.App/MauiProgram.Sentry.cs'),
      'utf8',
    )
    const projection = await readFile(
      join(root, 'dotnet-clients/src/Voucha.Client.App/SentryTransactionProjection.cs'),
      'utf8',
    )
    assert.match(options, /SetBeforeSendTransaction\(SentryTransactionProjection\.Scrub\)/u)
    assert.match(projection, /ScrubAddress\(span\.Description\) \?\? ""/u)
    assert.match(projection, /QueryString = null/u)
    assert.match(projection, /KeepsNumber\(key\)/u)
    assert.match(options, /extra\[key\] = value/u)
  })
})
