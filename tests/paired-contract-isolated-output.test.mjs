import assert from 'node:assert/strict'
import { it } from 'node:test'
import {
  assertUnprivilegedHostIdentity,
  decodeCandidateOutput,
} from '../scripts/run-isolated-native-exporter.mjs'
import { pairedExporterOutputPaths } from '../scripts/paired-exporter-runtime-evidence.mjs'

const expectedPaths = [
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageDescriptor.g.cs',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.es.resx',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.fr.resx',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.pt.resx',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.resx',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/Resources/en.lproj/Localizable.strings',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/Resources/es.lproj/Localizable.strings',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/Resources/fr.lproj/Localizable.strings',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/Resources/pt.lproj/Localizable.strings',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageDescriptor.swift',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift',
]
assert.deepEqual(pairedExporterOutputPaths, expectedPaths)
const swiftPath = expectedPaths.at(-1)
const dotnetPath = expectedPaths[0]
const baseFiles = expectedPaths.map((path, index) => [path, `contents-${index}\n`])

function payload(files = baseFiles) {
  return Buffer.from(
    JSON.stringify({
      schemaVersion: 1,
      files: files.map(([path, text]) => ({
        path,
        contentBase64: Buffer.from(text).toString('base64'),
      })),
    }),
  )
}

it('accepts only bounded regular-file data below both localization roots', () => {
  const decoded = decodeCandidateOutput(payload())
  assert.equal(decoded.length, expectedPaths.length)
  assert.equal(decoded.at(-1).contents.toString(), 'contents-11\n')
})

it('rejects traversal, undeclared paths, duplicates, malformed base64, and unexpected response fields', () => {
  assert.throws(
    () => decodeCandidateOutput(payload([['../escape', 'x'], ...baseFiles.slice(1)])),
    /noncanonical output path/u,
  )
  assert.throws(
    () =>
      decodeCandidateOutput(payload([['api-fixtures/v1/probe.json', 'x'], ...baseFiles.slice(1)])),
    /exact generated localization file set/u,
  )
  assert.throws(
    () =>
      decodeCandidateOutput(
        payload([
          ['swift-clients/ui/Sources/VouchaLocalization/Generated/Probe.swift', 'x'],
          ...baseFiles.slice(1),
        ]),
      ),
    /exact generated localization file set/u,
  )
  assert.throws(
    () => decodeCandidateOutput(payload([baseFiles[0], baseFiles[0], ...baseFiles.slice(1)])),
    /duplicate path/u,
  )
  const invalidBase64 = Buffer.from(
    JSON.stringify({
      schemaVersion: 1,
      files: [
        { path: swiftPath, contentBase64: 'not-base64!' },
        { path: dotnetPath, contentBase64: '' },
      ],
    }),
  )
  assert.throws(() => decodeCandidateOutput(invalidBase64), /invalid base64/u)
  const extraField = Buffer.from(
    JSON.stringify({
      schemaVersion: 1,
      runtime: { node: 'untrusted' },
      files: baseFiles.map(([path, text]) => ({
        path,
        contentBase64: Buffer.from(text).toString('base64'),
      })),
    }),
  )
  assert.throws(() => decodeCandidateOutput(extraField), /invalid shape/u)
})

it('rejects evidence missing either required localization tree', () => {
  assert.throws(
    () => decodeCandidateOutput(payload([[swiftPath, 'only Swift']])),
    /exact generated localization file set/u,
  )
})

it('refuses privileged host identities before candidate execution', () => {
  assert.doesNotThrow(() => assertUnprivilegedHostIdentity(501, 20))
  assert.throws(() => assertUnprivilegedHostIdentity(0, 0), /non-root host UID and GID/u)
  assert.throws(() => assertUnprivilegedHostIdentity(undefined, 20), /non-root host UID and GID/u)
})
