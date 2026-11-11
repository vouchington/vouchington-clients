import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import { prefixedFailure } from './prefixed-failure.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const fail = prefixedFailure('Contract check failed')

export const contractConfigPath = resolve(repositoryRoot, 'contracts/vouchington.json')
export const contractRepository = 'vouchington/vouchington'
export const localizationPaths = [
  'swift-clients/ui/Sources/VouchaLocalization/Generated',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated',
]
export const contractDataPaths = ['api-fixtures/v1', 'api-contracts/v1']
export const declaredContractPaths = [...contractDataPaths, ...localizationPaths]

/**
 * Reads the narrow, pinned Vouchington contract declaration. The pin is a full commit SHA so
 * every consumer resolves one immutable producer revision; there is no branch or ref to track.
 */
export async function readContractConfig(config = contractConfigPath) {
  let parsed
  try {
    parsed = JSON.parse(await readFile(config, 'utf8'))
  } catch (error) {
    fail(`cannot read contract configuration at ${config}: ${error.message}`)
  }
  if (
    JSON.stringify(Object.keys(parsed ?? {}).sort()) !==
    JSON.stringify(['commit', 'paths', 'repository', 'schemaVersion'])
  )
    fail('contract configuration must contain exactly schemaVersion, repository, commit, paths')
  if (
    parsed.schemaVersion !== 1 ||
    parsed.repository !== contractRepository ||
    typeof parsed.commit !== 'string' ||
    !/^[0-9a-f]{40}$/u.test(parsed.commit) ||
    JSON.stringify(parsed.paths) !== JSON.stringify(declaredContractPaths)
  )
    fail('contract configuration does not match the client contract (commit must be a full SHA)')
  return parsed
}

/**
 * A paired change may pin a different producer revision than trusted main (it is evaluated
 * against an open producer pull request), so declarations match when everything except the
 * pin is identical.
 */
export function sameDeclarationIgnoringPin(left, right) {
  return JSON.stringify({ ...left, commit: null }) === JSON.stringify({ ...right, commit: null })
}
