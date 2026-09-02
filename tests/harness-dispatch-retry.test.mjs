import assert from 'node:assert/strict'
import test from 'node:test'

import { AutoHarnessError, AutoHarnessRequestTimeoutError } from 'auto-harness-client'
import { HarnessDispatchError } from 'auto-harness-client/actions'

import { isRetryableDispatchError, withSingleRetry } from '../scripts/harness-dispatch-retry.mjs'

function harnessError(status, retryAfter) {
  return new AutoHarnessError('boom', { code: 'HTTP_ERROR', retryAfter, status })
}

// Lets a rejected operation() call propagate through withSingleRetry's catch block up to its
// `setTimeout` registration before a fake timer is ticked — without this, tick() races the
// still-pending microtask chain and the timer it needs to fire is never registered.
async function flushMicrotasks() {
  await Promise.resolve()
  await Promise.resolve()
}

function operationSequence(...results) {
  let call = 0
  const calls = []
  const fn = async () => {
    calls.push(call)
    const result = results[call]
    call += 1
    if (result.error) throw result.error
    return result.value
  }
  fn.calls = calls
  return fn
}

for (const status of [408, 429, 500, 502, 503, 504]) {
  test(`isRetryableDispatchError treats status ${status} as retryable`, () => {
    assert.equal(isRetryableDispatchError(harnessError(status)), true)
  })
}

for (const status of [400, 401, 403, 404, 422]) {
  test(`isRetryableDispatchError treats status ${status} as non-retryable`, () => {
    assert.equal(isRetryableDispatchError(harnessError(status)), false)
  })
}

test('isRetryableDispatchError treats a request timeout as retryable', () => {
  assert.equal(isRetryableDispatchError(new AutoHarnessRequestTimeoutError(30_000)), true)
})

test('isRetryableDispatchError treats a fetch network TypeError as retryable', () => {
  assert.equal(isRetryableDispatchError(new TypeError('fetch failed')), true)
})

test('isRetryableDispatchError treats a HarnessDispatchError as non-retryable', () => {
  assert.equal(isRetryableDispatchError(new HarnessDispatchError('CODE', 'message')), false)
})

test('withSingleRetry retries once and returns the second attempt result on a retryable error', async () => {
  const operation = operationSequence({ error: harnessError(500) }, { value: 'ok' })

  assert.equal(await withSingleRetry(operation), 'ok')
  assert.equal(operation.calls.length, 2)
})

test('withSingleRetry does not retry a non-retryable 4xx error', async () => {
  const operation = operationSequence({ error: harnessError(400) })

  await assert.rejects(withSingleRetry(operation), err => err.status === 400)
  assert.equal(operation.calls.length, 1)
})

test('withSingleRetry does not retry a HarnessDispatchError', async () => {
  const error = new HarnessDispatchError('CODE', 'message')
  const operation = operationSequence({ error })

  await assert.rejects(withSingleRetry(operation), err => err === error)
  assert.equal(operation.calls.length, 1)
})

test('withSingleRetry exhausts its single retry and surfaces the last error', async () => {
  const operation = operationSequence({ error: harnessError(500) }, { error: harnessError(502) })

  await assert.rejects(withSingleRetry(operation), err => err.status === 502)
  assert.equal(operation.calls.length, 2)
})

test('withSingleRetry honors Retry-After instead of the fixed delay', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] })
  const operation = operationSequence({ error: harnessError(429, '2') }, { value: 'ok' })

  const result = withSingleRetry(operation)
  await flushMicrotasks()
  t.mock.timers.tick(2_000)

  assert.equal(await result, 'ok')
})

test('withSingleRetry honors an HTTP-date Retry-After value', async t => {
  t.mock.timers.enable({ apis: ['setTimeout', 'Date'], now: new Date('2024-01-01T00:00:00.000Z') })
  const retryAfter = new Date('2024-01-01T00:00:03.000Z').toUTCString()
  const operation = operationSequence({ error: harnessError(503, retryAfter) }, { value: 'ok' })

  const result = withSingleRetry(operation)
  await flushMicrotasks()
  t.mock.timers.tick(3_000)

  assert.equal(await result, 'ok')
})

test('withSingleRetry falls back to the fixed delay for an unparseable Retry-After value', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] })
  const operation = operationSequence(
    { error: harnessError(503, 'not-a-valid-value') },
    { value: 'ok' },
  )

  const result = withSingleRetry(operation)
  await flushMicrotasks()
  t.mock.timers.tick(500)

  assert.equal(await result, 'ok')
})
