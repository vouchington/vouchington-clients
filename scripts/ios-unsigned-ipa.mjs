#!/usr/bin/env node
import { spawnSync } from 'node:child_process'

const fail = message => {
  console.error(message)
  process.exit(1)
}

const [ipa, version, build, bundleId] = process.argv.slice(2)
if (!ipa || !version || !build || !bundleId)
  fail('Usage: ios-unsigned-ipa.mjs <ipa> <version> <build> <bundle-id>')

const entries = unzip(['-Z1', ipa], 'utf8')
  .split('\n')
  .filter(entry => entry.length > 0)
const apps = new Set()
for (const entry of entries) {
  if (entry.includes('\\') || entry.startsWith('/'))
    fail(`IPA entry is not under Payload: ${entry}`)
  const segments = entry.split('/')
  if (segments.at(-1) === '') segments.pop()
  if (segments.length === 0 || segments[0] !== 'Payload' || segments.includes('..'))
    fail(`IPA entry is not under Payload: ${entry}`)
  if (segments.length === 1) continue
  if (!segments[1].endsWith('.app')) fail(`IPA entry is outside the app: ${entry}`)
  apps.add(segments[1])
  if (segments.at(-1) === 'embedded.mobileprovision')
    fail('Unsigned inspection IPA must not contain a provisioning profile')
}
if (apps.size !== 1) fail(`Expected one Payload app, found ${apps.size}`)
const app = [...apps][0]
const plistEntry = `Payload/${app}/Info.plist`
if (!entries.includes(plistEntry)) fail(`IPA is missing ${plistEntry}`)

const raw = unzip(['-p', ipa, plistEntry])
if (raw.subarray(0, 8).toString('utf8') === 'bplist00')
  fail('Info.plist in the inspection IPA must be XML')
const xml = raw.toString('utf8')
const actual = {
  version: plistString(xml, 'CFBundleShortVersionString'),
  build: plistString(xml, 'CFBundleVersion'),
  bundleId: plistString(xml, 'CFBundleIdentifier'),
}
if (actual.version !== version || actual.build !== build || actual.bundleId !== bundleId) {
  fail(
    `IPA identity mismatch: expected ${version} ${build} ${bundleId}, observed ${actual.version} ${actual.build} ${actual.bundleId}`,
  )
}

function unzip(args, encoding) {
  const result = spawnSync('unzip', args, { encoding })
  if (result.status !== 0) {
    const detail = `${result.stderr ?? result.error?.message ?? ''}`.trim() || 'unzip failed'
    fail(`Could not read IPA: ${detail}`)
  }
  return result.stdout
}

function plistString(xmlText, key) {
  const match = new RegExp(`<key>${key}</key>\\s*<string>([^<]*)</string>`).exec(xmlText)
  if (!match) fail(`Info.plist is missing ${key}`)
  return match[1]
}
