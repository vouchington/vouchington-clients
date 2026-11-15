import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'

const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8')

// Dependabot evaluates manifests on its own runtime without running contracts:pull.
describe('manifests Dependabot evaluates', () => {
  it('keeps Swift package manifests evaluable without generated localization', () => {
    for (const path of [
      'swift-clients/core/Package.swift',
      'swift-clients/ui/Package.swift',
      'swift-clients/apps/android/Package.swift',
    ]) {
      assert.doesNotMatch(read(path), /fatalError\(|FileManager\.default/u, path)
    }
  })

  it('accepts the Node 24 runtime Dependabot installs with', () => {
    const { engines } = JSON.parse(read('package.json'))
    assert.equal(engines.node, '>=24.0.0')
  })
})
