import { readFile, writeFile } from 'node:fs/promises'

const versionPattern = /\.package\(url:\s*"https:\/\/source\.skip\.tools\/skip\.git",\s*exact:\s*"(?<version>\d+\.\d+\.\d+)"\)/gu
const sha = /^[a-f0-9]{40}$/u

export function parseSkipUpdate(packageSource, resolvedSource) {
  const declarations = [...packageSource.matchAll(versionPattern)]
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

function trustedChecksum(upstreamPackage, version) {
  const escapedVersion = version.replaceAll('.', '\\.')
  const expression = new RegExp(
    `\\.binaryTarget\\(name:\\s*"skip",\\s*url:\\s*"https://github\\.com/skiptools/skip/releases/download/${escapedVersion}/skip-macos\\.zip",\\s*checksum:\\s*"(?<checksum>[a-f0-9]{64})"\\)`,
    'u',
  )
  const checksum = upstreamPackage.match(expression)?.groups?.checksum
  if (!checksum) throw new Error('Trusted Skip package metadata has no matching binary checksum')
  return checksum
}

export function materializeSkipRepair(materializer, upstreamPackage, update) {
  const url = 'SKIP_MACOS_GITHUB_ZIP_URL="https://github.com/skiptools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"'
  if (!materializer.includes(url)) throw new Error('Trusted materializer lacks the version-templated GitHub archive URL')
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
