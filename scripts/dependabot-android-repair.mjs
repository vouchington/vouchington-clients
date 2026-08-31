import { readFile, writeFile } from 'node:fs/promises'

import { isSwiftCodeOffset, parseUniqueSwiftBinaryTargetChecksum } from 'vouchington-tooling/swift-source-offset'
import { validateResolvedPinDelta } from 'vouchington-tooling/swift-resolved-pin-delta'

const versionPattern = /\.package\(url:\s*"https:\/\/source\.skip\.tools\/skip\.git",\s*exact:\s*"(?<version>\d+\.\d+\.\d+)"\)/gu
const sha = /^[a-f0-9]{40}$/u
const materializerUrl = 'SKIP_MACOS_GITHUB_ZIP_URL="https://github.com/skiptools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"'

export function parseSkipUpdate(packageSource, resolvedSource) {
  const declarations = [...packageSource.matchAll(versionPattern)].filter(
    declaration => declaration.index !== undefined && isSwiftCodeOffset(packageSource, declaration.index),
  )
  const version = declarations[0]?.groups?.version
  if (declarations.length !== 1 || !version) {
    throw new Error('Android Package.swift must contain exactly one exact Skip semantic version')
  }
  const pins = JSON.parse(resolvedSource)?.pins?.filter(pin => pin?.identity === 'skip') ?? []
  const pin = pins[0]
  if (pins.length !== 1 || pin?.state?.version !== version || !sha.test(pin?.state?.revision ?? '')) {
    throw new Error('Android Package.resolved Skip pin must match Package.swift exactly')
  }
  return { revision: pin.state.revision, version }
}

function skipDeclaration(packageSource) {
  const declarations = [...packageSource.matchAll(versionPattern)].filter(
    candidate => candidate.index !== undefined && isSwiftCodeOffset(packageSource, candidate.index),
  )
  const declaration = declarations[0]
  const version = declaration?.groups?.version
  if (declarations.length !== 1 || !declaration || !version) {
    throw new Error('Android Package.swift must contain one executable exact Skip declaration')
  }
  return { literal: declaration[0], version }
}

export function validateTrustedBaseDelta(trustedPackageSource, trustedResolvedSource, packageSource, resolvedSource) {
  const trusted = skipDeclaration(trustedPackageSource)
  const candidate = skipDeclaration(packageSource)
  const expectedDeclaration = trusted.literal.replace(`"${trusted.version}"`, `"${candidate.version}"`)
  if (packageSource !== trustedPackageSource.replace(trusted.literal, expectedDeclaration)) {
    throw new Error('Android Package.swift may change only the exact Skip version')
  }
  if (candidate.literal !== expectedDeclaration) {
    throw new Error('Android Package.swift Skip declaration formatting must remain unchanged')
  }
  const update = parseSkipUpdate(packageSource, resolvedSource)
  validateResolvedPinDelta(JSON.parse(trustedResolvedSource), JSON.parse(resolvedSource), { requiredIdentity: 'skip' })
  return update
}

function trustedChecksum(upstreamPackage, version) {
  const checksum = parseUniqueSwiftBinaryTargetChecksum(
    upstreamPackage,
    'skip',
    `https://github.com/skiptools/skip/releases/download/${version}/skip-macos.zip`,
  )
  if (!checksum) throw new Error('Trusted Skip package metadata has no matching binary checksum')
  return checksum
}

export function validateMaterializerSource(materializer) {
  const urls = materializer.match(/^SKIP_MACOS_GITHUB_ZIP_URL=.*$/gmu) ?? []
  if (urls.length !== 1 || urls[0] !== materializerUrl) {
    throw new Error('Trusted materializer lacks the exact version-templated GitHub archive URL')
  }
}

export function materializeSkipRepair(materializer, upstreamPackage, update) {
  validateMaterializerSource(materializer)
  const checksum = trustedChecksum(upstreamPackage, update.version)
  const next = materializer
    .replace(/^SKIP_VERSION="[^"]+"$/mu, `SKIP_VERSION="${update.version}"`)
    .replace(/^SKIP_MACOS_ZIP_SHA256="[a-f0-9]{64}"$/mu, `SKIP_MACOS_ZIP_SHA256="${checksum}"`)
  if (next === materializer) throw new Error('Trusted materializer lacks expected repair fields')
  return next
}

async function main() {
  const [materializerPath, candidatePackagePath, candidateResolvedPath, upstreamPackagePath, outputPath] = process.argv.slice(2)
  if (!materializerPath || !candidatePackagePath || !candidateResolvedPath || !upstreamPackagePath || !outputPath) {
    throw new Error('Usage: dependabot-android-repair.mjs <trusted-materializer> <candidate-package> <candidate-resolved> <upstream-package> <output>')
  }
  const [materializer, packageSource, resolvedSource, upstreamPackage] = await Promise.all([
    readFile(materializerPath, 'utf8'),
    readFile(candidatePackagePath, 'utf8'),
    readFile(candidateResolvedPath, 'utf8'),
    readFile(upstreamPackagePath, 'utf8'),
  ])
  await writeFile(outputPath, materializeSkipRepair(materializer, upstreamPackage, parseSkipUpdate(packageSource, resolvedSource)))
}

if (import.meta.url === `file://${process.argv[1]}`) await main()
