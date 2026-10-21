import { execFileSync } from 'node:child_process'
import { existsSync, lstatSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { basename, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const packageRoot = resolve(fileURLToPath(new URL('..', import.meta.url)))
const profileRoot = join(packageRoot, '.build/Android')
const maxProfileBytes = 4 * 1024 * 1024
const maxEntries = 25_000
const maxScanMs = 2_000

export function profileRows(html, tab) {
  const section = html.match(new RegExp(`<div class="tab" id="tab${tab}">([\\s\\S]*?)<\\/div>`))?.[1]
  if (!section) throw new Error(`Gradle profile tab ${tab} is missing`)
  return [...section.matchAll(/<tr>\s*<td(?: class="[^"]*")?>([^<]*)<\/td>\s*<td class="numeric">([^<]*)<\/td>(?:\s*<td>[^<]*<\/td>)?\s*<\/tr>/gu)]
    .map(([, name, duration]) => ({ name: name.trim(), duration: duration.trim() }))
}

export function durationSeconds(value) {
  const parts = [...value.matchAll(/(\d+(?:\.\d+)?)(ms|s|m|h)/gu)]
  if (!parts.length || parts.map(part => part[0]).join('') !== value.replaceAll(' ', '')) {
    throw new Error(`Unsupported Gradle profile duration: ${value}`)
  }
  return parts.reduce((sum, [, amount, unit]) => sum + Number(amount) * { ms: 0.001, s: 1, m: 60, h: 3600 }[unit], 0)
}

export function profileFiles(root, sourceRoot = packageRoot) {
  const roots = [join(sourceRoot, 'Android/build/reports/profile'), join(root, 'reports/profile')]
  if (existsSync(root)) {
    roots.push(...readdirSync(root, { withFileTypes: true })
      .filter(entry => entry.isDirectory())
      .slice(0, 100)
      .map(entry => join(root, entry.name, 'reports/profile')))
  }
  return roots.flatMap(path => existsSync(path)
    ? readdirSync(path, { withFileTypes: true })
      .filter(entry => entry.isFile() && /^profile-.*\.html$/u.test(entry.name))
      .map(entry => join(path, entry.name))
    : [])
}

export function measuredSize(root) {
  if (!root || !existsSync(root)) return { bytes: 0, entries: 0, complete: true, present: false }
  const started = Date.now()
  const pending = [root]
  let bytes = 0
  let entries = 0
  while (pending.length && entries < maxEntries && Date.now() - started < maxScanMs) {
    const path = pending.pop()
    const stat = lstatSync(path)
    entries += 1
    if (stat.isDirectory()) {
      for (const entry of readdirSync(path)) pending.push(join(path, entry))
    } else if (stat.isFile()) bytes += stat.size
  }
  return { bytes, entries, complete: pending.length === 0, present: true }
}

export function renderReport({ html, profilePath, paths, identity }) {
  const summary = profileRows(html, 0)
  const tasks = profileRows(html, 4)
    .filter(row => row.name.startsWith(':') && row.name !== ':')
    .map(row => ({ ...row, seconds: durationSeconds(row.duration) }))
    .sort((a, b) => b.seconds - a.seconds)
  const groups = new Map()
  for (const task of tasks) {
    const name = /buildAndroidSwiftPackage/u.test(task.name) ? 'Native Swift package'
      : task.name.startsWith(':skip-plugins:') ? 'Skip Gradle plugin'
      : /compile.*(?:Kotlin|Java)/u.test(task.name) ? 'Kotlin and Java compile'
      : /^:app:(?:package|assemble|merge.*Dex|dexBuilder)/u.test(task.name) ? 'APK assembly'
      : 'Other Gradle tasks'
    groups.set(name, (groups.get(name) ?? 0) + task.seconds)
  }
  const lines = [
    '### Android Gradle profile and generated state',
    '',
    `Candidate: \`${identity.revision}\`; run: \`${identity.run}\`; attempt: \`${identity.attempt}\`; Gradle: \`${identity.gradle}\`.`,
    `Profile: \`${profilePath}\`. Task durations are cumulative and may overlap; they are not critical-path wall time.`,
    '',
    '| Build category | Duration |',
    '| --- | ---: |',
    ...summary.map(row => `| ${row.name} | ${row.duration} |`),
    '',
    '| Longest task | Duration |',
    '| --- | ---: |',
    ...tasks.slice(0, 15).map(row => `| \`${row.name}\` | ${row.duration} |`),
    '',
    '| Task-time group (may overlap) | Sum |',
    '| --- | ---: |',
    ...[...groups].map(([name, seconds]) => `| ${name} | ${seconds.toFixed(3)}s |`),
    '',
    '| Generated state | Observed regular-file bytes | Entries | Scan |',
    '| --- | ---: | ---: | --- |',
    ...paths.map(({ name, path }) => {
      const size = measuredSize(path)
      const state = !size.present ? 'missing' : size.complete ? 'complete' : 'partial (bounded)'
      return `| ${name} | ${size.bytes} | ${size.entries} | ${state} |`
    }),
    '',
  ]
  return lines.join('\n')
}

function main() {
  const files = profileFiles(profileRoot).sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs)
  if (files.length !== 1) throw new Error(`Expected one Android Gradle profile; found ${files.length}`)
  const profilePath = files[0]
  if (statSync(profilePath).size > maxProfileBytes) throw new Error('Gradle profile exceeds 4 MiB')
  const revision = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: packageRoot, encoding: 'utf8' }).trim()
  const gradle = readFileSync(join(packageRoot, 'Android/gradle/wrapper/gradle-wrapper.properties'), 'utf8')
    .match(/gradle-(\d+\.\d+\.\d+)-bin\.zip/u)?.[1]
  if (!/^[0-9a-f]{40}$/u.test(revision) || !gradle) throw new Error('Missing candidate or Gradle identity')
  const paths = [
    { name: 'Skip generated plugins', path: join(packageRoot, '.build/plugins/outputs') },
    { name: 'Gradle project outputs', path: join(packageRoot, '.build/Android') },
    { name: 'SwiftPM checkouts', path: join(packageRoot, '.build/checkouts') },
    { name: 'SwiftPM artifacts', path: join(packageRoot, '.build/artifacts') },
    { name: 'Gradle user home', path: process.env.GRADLE_USER_HOME },
    { name: 'Gradle build cache', path: process.env.GRADLE_BUILD_CACHE_DIR },
    { name: 'Job-scoped Skip home', path: process.env.VOUCHA_SKIP_SWIFT_HOME },
    { name: 'Job-scoped mise data', path: process.env.MISE_DATA_DIR },
    { name: 'Job-scoped Android SDK', path: process.env.ANDROID_HOME },
  ]
  process.stdout.write(renderReport({
    html: readFileSync(profilePath, 'utf8'),
    profilePath: basename(profilePath),
    paths,
    identity: {
      revision,
      run: process.env.GITHUB_RUN_ID || 'local',
      attempt: process.env.GITHUB_RUN_ATTEMPT || 'local',
      gradle,
    },
  }))
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main()
