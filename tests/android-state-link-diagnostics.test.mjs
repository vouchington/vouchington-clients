import assert from 'node:assert/strict'
import test from 'node:test'
import { assertDebugLink } from '../swift-clients/apps/android/tooling/android-state-links.mjs'

function failure(target) {
  let error
  try {
    assertDebugLink('.build/debug', target)
  } catch (caught) {
    error = caught
  }
  assert.match(error?.message ?? '', /^Unsafe Android state symlink: \.build\/debug; target=/u)
  return error.message.split('; target=')[1]
}

test('unsafe debug-link target is JSON-escaped without changing admission', () => {
  const target = 'unexpected/"target"\n\r\u001b[31m'
  assert.equal(failure(target), JSON.stringify(target))
  for (const control of ['\n', '\r', '\u001b']) assert.ok(!failure(target).includes(control))
  assert.doesNotThrow(() => assertDebugLink('.build/debug', 'arm64-apple-macosx/debug'))
  assert.throws(
    () => assertDebugLink('.build/debug', 'out/Products/Debug-android-aarch64'),
    /Unsafe Android state symlink/u,
  )
})

test('unsafe debug-link target diagnostics are capped at 1024 UTF-8 bytes', () => {
  for (const target of ['x'.repeat(3000), '😀'.repeat(2000)]) {
    const diagnostic = failure(target)
    assert.ok(Buffer.byteLength(diagnostic) <= 1024)
    assert.ok(diagnostic.endsWith('...'))
    assert.ok(!diagnostic.includes('\ufffd'), 'truncation must preserve UTF-8 code points')
  }
})
