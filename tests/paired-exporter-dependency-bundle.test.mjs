import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { readFile } from 'node:fs/promises'
import { it } from 'node:test'
import {
  readPinnedRegistryBytes,
  verifyPairedExporterLock,
} from '../scripts/fetch-paired-exporter-dependencies.mjs'

const producerRoot = process.env.PAIRED_TEST_PRODUCER_ROOT

it('accepts the exact reviewed exporter lock closure', { skip: !producerRoot }, async () => {
  const [lock, packageJson] = await Promise.all([
    readFile(`${producerRoot}/pnpm-lock.yaml`, 'utf8'),
    readFile(`${producerRoot}/ts-shared/ui-messages/package.json`, 'utf8').then(JSON.parse),
  ])
  assert.equal(verifyPairedExporterLock(lock, packageJson), undefined)
})

it(
  'fails closed when a transitive package integrity changes',
  { skip: !producerRoot },
  async () => {
    const [lock, packageJson] = await Promise.all([
      readFile(`${producerRoot}/pnpm-lock.yaml`, 'utf8'),
      readFile(`${producerRoot}/ts-shared/ui-messages/package.json`, 'utf8').then(JSON.parse),
    ])
    const changed = lock.replace(
      'sha512-N6//FLET/tXYNM/F6ABca1oH6fWB+KlTt909Le28WMDBk8oaT4vY17DCrwg2MvmuqUKt3Ni4N5dGJ/EoBgcO6A==',
      'sha512-N6//FLET/tXYNM/F6ABca1oH6fWB+KlTt909Le28WMDBk8oaT4vY17DCrwg2MvmuqUKt3Ni4N5dGJ/EoBgcO6B==',
    )
    assert.notEqual(changed, lock)
    assert.throws(
      () => verifyPairedExporterLock(changed, packageJson),
      /reviewed lock entry changed/u,
    )
  },
)

it(
  'fails closed when the exporter adds an unreviewed dependency',
  { skip: !producerRoot },
  async () => {
    const [lock, packageJson] = await Promise.all([
      readFile(`${producerRoot}/pnpm-lock.yaml`, 'utf8'),
      readFile(`${producerRoot}/ts-shared/ui-messages/package.json`, 'utf8').then(JSON.parse),
    ])
    assert.throws(
      () =>
        verifyPairedExporterLock(lock, {
          ...packageJson,
          dependencies: { ...packageJson.dependencies, yaml: '^2.9.1' },
        }),
      /dependency declaration changed/u,
    )
  },
)

it('fetches only the constructed registry URL without following redirects and verifies SRI', async () => {
  const contents = Buffer.from('pinned package bytes')
  const integrity = `sha512-${createHash('sha512').update(contents).digest('base64')}`
  let requestedURL
  const result = await readPinnedRegistryBytes(
    { name: 'sample-pkg', version: '1.2.3', integrity },
    async (url, options) => {
      requestedURL = url
      assert.equal(options.redirect, 'error')
      const response = new Response(contents)
      Object.defineProperty(response, 'url', {
        value: 'https://registry.npmjs.org/sample-pkg/-/sample-pkg-1.2.3.tgz',
      })
      return response
    },
  )
  assert.equal(requestedURL, 'https://registry.npmjs.org/sample-pkg/-/sample-pkg-1.2.3.tgz')
  assert.deepEqual(result, contents)
})

it('rejects unexpected registry origins, redirect responses, and integrity mismatches', async () => {
  const content = Buffer.from('package')
  const expected = `sha512-${createHash('sha512').update(content).digest('base64')}`
  const wrongOrigin = async () => {
    const response = new Response(content)
    Object.defineProperty(response, 'url', { value: 'https://registry.attacker.example/pkg.tgz' })
    return response
  }
  await assert.rejects(
    readPinnedRegistryBytes({ name: 'pkg', version: '1.0.0', integrity: expected }, wrongOrigin),
    /unexpected origin/u,
  )
  const wrongSRI = async () => {
    const response = new Response(content)
    Object.defineProperty(response, 'url', {
      value: 'https://registry.npmjs.org/pkg/-/pkg-1.0.0.tgz',
    })
    return response
  }
  await assert.rejects(
    readPinnedRegistryBytes(
      { name: 'pkg', version: '1.0.0', integrity: `sha512-${'A'.repeat(88)}` },
      wrongSRI,
    ),
    /integrity/u,
  )
})
