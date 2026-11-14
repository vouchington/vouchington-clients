import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { jobBlock as readJobBlock } from './workflow-job-block.mjs'

const jobBlock = (workflow, job) => readJobBlock(workflow, job, `missing workflow job ${job}`)

test('unsigned macOS archive stays arm64 and keeps the packaging checks', () => {
  const workflow = readFileSync(
    new URL('../.github/workflows/release-macos-unsigned.yml', import.meta.url),
    'utf8',
  )
  const job = jobBlock(workflow, 'archive-and-assemble')
  const archive = job.slice(
    job.indexOf('- name: Archive without signing'),
    job.indexOf('- name: Export unsigned Copy App'),
  )
  const exported = job.slice(
    job.indexOf('- name: Export unsigned Copy App'),
    job.indexOf('- name: Assemble and verify unsigned DMG'),
  )
  const image = job.slice(job.indexOf('- name: Assemble and verify unsigned DMG'))

  assert.match(job, /^    timeout-minutes: 30$/mu)
  assert.match(archive, /timeout-minutes: 24/u)
  assert.match(exported, /timeout-minutes: 3/u)
  assert.match(image, /timeout-minutes: 3/u)
  assert.match(archive, /-destination 'generic\/platform=macOS'/u)
  assert.match(archive, /archive ARCHS=arm64 EXCLUDED_ARCHS=x86_64 /u)
  assert.match(archive, /CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY=""/u)
  assert.match(archive, /lipo -archs "\$app\/Contents\/MacOS\/Voucha"\)" = arm64/u)
  assert.match(exported, /UnsignedExportOptions\.plist/u)
  assert.match(
    exported,
    /CFBundleIdentifier' "\$app\/Contents\/Info\.plist"\)" = ai\.voucha\.macos/u,
  )
  assert.match(exported, /lipo -archs "\$app\/Contents\/MacOS\/Voucha"\)" = arm64/u)
  assert.match(exported, /Signature=adhoc/u)
  assert.match(exported, /TeamIdentifier=not set/u)
  assert.match(exported, /code object is not signed at all/u)
  assert.match(image, /hdiutil create/u)
  assert.match(image, /hdiutil verify/u)
  assert.match(image, /shasum -a 256/u)
  assert.match(job, /retention-days: 7/u)
})
