import assert from 'node:assert/strict'
import { mkdir, mkdtemp, readFile, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  durationSeconds,
  measuredSize,
  profileFiles,
  profileRows,
  renderReport,
  selectProfilePath,
} from '../swift-clients/apps/android/tooling/report-gradle-profile.mjs'

const fixture = new URL('./fixtures/gradle-9.4.1-profile.html', import.meta.url)

test('reads the pinned Gradle 9.4.1 profile categories and task durations', async () => {
  const html = await readFile(fixture, 'utf8')
  assert.deepEqual(
    profileRows(html, 0).find(row => row.name === 'Total Build Time'),
    {
      name: 'Total Build Time',
      duration: '11.347s',
    },
  )
  assert.deepEqual(
    profileRows(html, 4).find(row => row.name === ':measure'),
    {
      name: ':measure',
      duration: '0.009s',
    },
  )
  assert.deepEqual(profileRows(html, 2), [{ name: 'All dependencies', duration: '0.127s' }])
  assert.deepEqual(
    profileRows(html, 4).map(row => row.name),
    [':measure', ':app:compileDebugKotlin', ':app:packageDebug'],
  )
  assert.equal(durationSeconds('1.5m'), 90)
  assert.equal(durationSeconds('1m 23.5s'), 83.5)
  assert.throws(() => profileRows('<html></html>', 4), /tab 4 is missing/u)
  assert.throws(() => durationSeconds('unknown'), /Unsupported/u)
})

test('reports exact identity, bounded generated sizes, and longest tasks', async t => {
  const root = await mkdtemp(join(tmpdir(), 'android-gradle-profile-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  await mkdir(join(root, 'generated'))
  await writeFile(join(root, 'generated/output'), '1234')
  await symlink('/dev/null', join(root, 'generated/external'))
  const profileDirectory = join(root, '.build/Android/VouchaAndroid/reports/profile')
  await mkdir(profileDirectory, { recursive: true })
  await writeFile(join(profileDirectory, 'profile-pinned.html'), 'profile')
  assert.deepEqual(profileFiles(join(root, '.build/Android'), root), [
    join(profileDirectory, 'profile-pinned.html'),
  ])
  const producerProfile = join(profileDirectory, 'profile-pinned.html')
  const consumerProfile = join(profileDirectory, 'profile-consumer.html')
  await writeFile(consumerProfile, 'consumer')
  const bothProfiles = profileFiles(join(root, '.build/Android'), root)
  assert.equal(selectProfilePath(bothProfiles, consumerProfile), consumerProfile)
  assert.equal(selectProfilePath(bothProfiles, producerProfile), producerProfile)
  assert.throws(() => selectProfilePath(bothProfiles), /Expected one Android Gradle profile/u)
  assert.throws(
    () => selectProfilePath(bothProfiles, join(root, 'outside-profile.html')),
    /outside the expected generated reports/u,
  )
  assert.deepEqual(measuredSize(join(root, 'missing')), {
    bytes: 0,
    entries: 0,
    complete: true,
    present: false,
  })
  assert.equal(measuredSize(join(root, 'generated')).bytes, 4)
  const wide = join(root, 'wide')
  await mkdir(wide)
  await Promise.all(
    Array.from({ length: 40 }, (_, index) => writeFile(join(wide, `file-${index}`), 'x')),
  )
  const bounded = measuredSize(wide, { entryLimit: 10, timeLimitMs: 2_000 })
  assert.equal(bounded.complete, false)
  assert.ok(bounded.entries <= 10)
  assert.ok(bounded.bytes <= 9)
  const report = renderReport({
    html: await readFile(fixture, 'utf8'),
    profilePath: 'profile-pinned.html',
    paths: [{ name: 'Generated', path: join(root, 'generated') }],
    identity: { revision: 'a'.repeat(40), run: '123', attempt: '2', gradle: '9.4.1' },
  })
  assert.match(report, /Candidate: `a{40}`; run: `123`; attempt: `2`; Gradle: `9\.4\.1`/u)
  assert.match(report, /\| `:measure` \| 0\.009s \|/u)
  assert.match(report, /\| All dependencies \| 0\.127s \|/u)
  assert.match(report, /\| Kotlin and Java compile \| 1\.200s \|/u)
  assert.match(report, /\| APK assembly \| 0\.100s \|/u)
  assert.doesNotMatch(report, /\| `:app` \|/u)
  assert.match(report, /\| Generated \| 4 \| 3 \| complete \|/u)
  assert.match(report, /may overlap; they are not critical-path wall time/u)
})
