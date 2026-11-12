import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  authNewsBudgetMs,
  authNewsCleanupMs,
  authNewsTypingMs,
  createDeadline,
} from '../dotnet-clients/tooling/catalyst-gui-deadline.mjs'
import { assertAuthNewsControls } from '../dotnet-clients/tooling/catalyst-gui-auth-news-session.mjs'

describe('Catalyst auth/news shared deadline', () => {
  it('caps later phases by elapsed startup time and reserves teardown inside three minutes', () => {
    let now = 0
    const budget = createDeadline(authNewsBudgetMs, () => now)
    assert.equal(budget.remaining(20_000), 20_000)
    now += 20_000
    assert.equal(budget.remaining(110_000), 110_000)
    now += 100_000
    assert.equal(budget.remaining(75_000), 30_000)
    assert.equal(budget.remaining(100_000, 10_000), 20_000)
    assert.equal(budget.remaining(10_000), 10_000)
    now += 30_000
    assert.throws(() => budget.remaining(), /overall deadline/u)
    const cleanup = createDeadline(authNewsCleanupMs, () => now)
    assert.equal(cleanup.remaining(), 10_000)
    assert.ok(authNewsBudgetMs + authNewsCleanupMs < 180_000)
  })

  it('clips the typing cap to shared time remaining and includes in-flight deadline context', async () => {
    let now = 0
    const budget = createDeadline(authNewsBudgetMs, () => now)
    assert.equal(authNewsTypingMs, 30_000)
    assert.equal(budget.remaining(authNewsTypingMs), 30_000)
    now = 125_000
    assert.equal(budget.remaining(authNewsTypingMs), 25_000)
    await assert.rejects(
      createDeadline(5, undefined, () => ' during POST /element/email/value (cap 5ms)').run(
        () => new Promise(() => {}),
      ),
      /overall deadline exceeded during POST \/element\/email\/value \(cap 5ms\)/u,
    )
  })

  it('rejects a stuck operation with its overall watchdog', async () => {
    const budget = createDeadline(5)
    await assert.rejects(
      budget.run(() => new Promise(() => {})),
      /overall deadline/u,
    )
  })

  it('accepts timely completion and rejects completion after the deadline', async () => {
    let now = 0
    const budget = createDeadline(100, () => now)
    assert.equal(await budget.run(async () => 'ready'), 'ready')
    await assert.rejects(
      budget.run(async () => {
        now = 101
        return 'late'
      }),
      /overall deadline/u,
    )
  })

  it('bounds polling sleeps and refuses work after expiration', async () => {
    await createDeadline(100).pause(1)
    let now = 0
    const short = createDeadline(1, () => now).pause(100)
    now = 2
    await assert.rejects(short, /overall deadline/u)
    const expired = createDeadline(0)
    await assert.rejects(
      expired.run(() => assert.fail('must not start')),
      /overall deadline/u,
    )
  })

  it('does not restart the overall budget when anonymous UI polling fails', async () => {
    let now = 0
    const budget = createDeadline(100, () => now)
    let calls = 0
    await assert.rejects(
      assertAuthNewsControls(
        async () => {
          calls++
          now = 101
          throw new Error('not visible yet')
        },
        'session',
        () => assert.fail('must not navigate'),
        budget,
      ),
      /overall deadline/u,
    )
    assert.equal(calls, 1)
  })
})
