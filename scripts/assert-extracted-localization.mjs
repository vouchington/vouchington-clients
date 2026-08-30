import { readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'

import { declaredFilamentsContractPaths } from './contracts.mjs'

const [fixturesPath, swiftPath, dotnetPath] = declaredFilamentsContractPaths

export async function assertExtractedLocalizationRepresentatives(contractRoot) {
  const root = resolve(contractRoot)
  const [swift, dotnet] = await Promise.all([
    readFile(join(root, swiftPath, 'UiMessageKey.swift'), 'utf8'),
    readFile(join(root, dotnetPath, 'UiMessageKey.g.cs'), 'utf8'),
  ])
  if (!swift.includes('nativeSwiftTopHashtagsTopHashtags'))
    throw new Error('staged Swift extracted localization representative is missing')
  if (!dotnet.includes('NativeDotnetTopHashtagsTopHashtags'))
    throw new Error('staged .NET extracted localization representative is missing')
}

if (process.argv[1] === new URL(import.meta.url).pathname) {
  const [flag, contractRoot] = process.argv.slice(2)
  if (flag !== '--contract-root' || contractRoot === undefined || process.argv.length !== 4)
    throw new Error('Usage: node scripts/assert-extracted-localization.mjs --contract-root <path>')
  await assertExtractedLocalizationRepresentatives(contractRoot)
}
