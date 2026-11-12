import assert from 'node:assert/strict'

const headingId = 'notification-preferences-heading'
const fieldNames = {
  engagement: 'is_engagement_emails_enabled',
  moderation: 'is_moderation_emails_enabled',
}

// Hosted CI exercises native switches only. Picker/time/day gestures remain
// manual acceptance for #60; do not open their native sheets in this lane. The persisted report is
// mandatory: an accepted key/click alone never establishes that a setting changed.
// Mac2 commands: https://appium.github.io/appium-mac2-driver/latest/reference/execute-methods/
export async function assertNotificationControls(request, sessionId, options = {}) {
  if (typeof options.readEvidence !== 'function') throw new Error('readEvidence is required for persisted notification acceptance')
  assert.ok(typeof options.expectedAnnouncement === 'string' && options.expectedAnnouncement.length > 0, 'expectedAnnouncement from generated localization is required')
  const now = options.now ?? Date.now
  const sleep = options.sleep ?? (ms => new Promise(resolve => setTimeout(resolve, ms)))
  const deadline = now() + (options.timeoutMs ?? 140_000)
  let phase = 'readiness'
  let phaseDeadline = Math.min(deadline, now() + 75_000)
  let operationDeadline = deadline
  const assertDeadline = () => {
    if (now() >= deadline) throw new Error('notification session exceeded its shared deadline')
    if (now() >= phaseDeadline) throw new Error(`notification ${phase} phase exceeded its deadline`)
  }
  const beginPhase = (name, budget) => {
    phase = name
    phaseDeadline = Math.min(deadline, now() + budget)
    assertDeadline()
  }
  const checks = []
  const passed = name => checks.push({ name, status: 'passed' })
  const command = async (method, path, body) => {
    assertDeadline()
    const remaining = Math.min(deadline, phaseDeadline, operationDeadline) - now()
    if (remaining <= 0) throw new Error(`notification ${phase} request deadline expired`)
    const response = await request(method, `/session/${sessionId}${path}`, body, {
      signal: AbortSignal.timeout(Math.max(1, Math.ceil(remaining))),
    })
    assertDeadline()
    if (response?.value?.error) throw new Error(`Mac2 ${response.value.error}: ${response.value.message ?? path}`)
    return response?.value
  }
  const poll = async (description, read, accept, budget = 10_000) => {
    const end = Math.min(deadline, phaseDeadline, operationDeadline, now() + budget)
    const previous = operationDeadline
    operationDeadline = end
    try {
      while (now() < end) {
        const result = await read()
        if (now() >= end) break
        if (accept(result)) return result
        const remaining = Math.max(0, end - now())
        if (remaining === 0) break
        await sleep(Math.min(250, remaining))
      }
      throw new Error(`${description} did not settle before the deadline`)
    } finally {
      operationDeadline = previous
    }
  }
  const find = async (id, budget = 10_000) => poll(id, async () => {
    try {
      const value = await command('POST', '/element', { using: 'accessibility id', value: id })
      return value?.['element-6066-11e4-a52e-4f735466cecf'] ?? value?.ELEMENT
    } catch (error) {
      // Only absence is transient. Unsupported commands, stale references, and
      // permissions must fail immediately instead of becoming a timeout/pass.
      if (/no such element/iu.test(error.message)) return undefined
      throw error
    }
  }, Boolean, budget)
  const path = (element, suffix) => `/element/${encodeURIComponent(element)}/${suffix}`
  const attr = async (element, name) => {
    const value = await command('GET', path(element, `attribute/${name}`))
    return value && typeof value === 'object' && 'value' in value ? value.value : value
  }
  const visible = element => command('GET', path(element, 'displayed'))
  const ready = async id => {
    const element = await find(id)
    assert.equal(truth(await visible(element)), true, `${id} was not visible`)
    await poll(`${id} enabled`, () => attr(element, 'enabled'), truth)
    return element
  }
  const click = element => command('POST', path(element, 'click'), {})
  const execute = (script, args) => command('POST', '/execute/sync', { script: `macos: ${script}`, args: [args] })
  const report = async () => {
    assertDeadline()
    const value = await options.readEvidence()
    assertDeadline()
    assert.equal(value?.schemaVersion, 2, 'unsupported notification app report')
    assert.ok(value.initialPreferences && Array.isArray(value.patches), 'missing notification app report data')
    return value
  }
  let state
  let patchCount
  const mutate = async (field, expected, action) => {
    const before = await report()
    assert.equal(before.patches.length, patchCount, 'unexpected or stale PATCH history before gesture')
    await action()
    const after = await poll(`${field} persisted PATCH`, report, value => value.patches.length > patchCount)
    assert.equal(after.patches.length, patchCount + 1, `${field} gesture produced multiple PATCH requests`)
    const patch = after.patches.at(-1)
    const key = fieldNames[field]
    assert.deepEqual(patch.fields, [key], `${field} PATCH must contain exactly its API field`)
    assert.deepEqual(patch.before, state, `${field} PATCH started from stale persisted state`)
    const expectedState = { ...state, [key]: expected }
    assert.deepEqual(patch.after, expectedState, `${field} PATCH changed an unrelated field or failed to persist`)
    state = patch.after
    patchCount += 1
  }
  const toggle = async field => {
    const id = `notification-${field}`
    const element = await ready(id)
    const before = switchState(await attr(element, 'value'))
    assert.equal(before, state[fieldNames[field]], `${field} displayed state differs from persisted state`)
    await mutate(field, !before, () => click(element))
    await poll(`${field} native switch`, async () => ({
      enabled: truth(await attr(element, 'enabled')), value: switchState(await attr(element, 'value')),
    }), value => value.enabled && value.value === !before)
  }
  const visibility = async (ids, expected) => {
    for (const id of ids) {
      // Hidden controls may remain in the tree or disappear entirely.
      const values = await command('POST', '/elements', { using: 'accessibility id', value: id })
      assert.ok(Array.isArray(values), 'Mac2 elements response must be an array')
      const shown = await Promise.all(values.map(value => visible(value['element-6066-11e4-a52e-4f735466cecf'] ?? value.ELEMENT)))
      assert.equal(shown.some(truth), expected, `${id} schedule visibility differs`)
    }
  }

  const schedule = expected => visibility(['notification-cadence', 'notification-time', 'notification-timezone'], expected)

  const heading = await find(headingId, 75_000)
  assert.equal(truth(await visible(heading)), true, 'notification heading was not visible')
  passed('heading-exposed')
  const assertDispatches = value => {
    assert.ok(Array.isArray(value.activations) && value.activations.length > 0, 'missing canonical route activation')
    for (const activation of value.activations) {
      assert.equal(activation.canonicalPath, '/my/notification-settings')
      const dispatches = value.accessibilityDispatches.filter(item => item.activationId === activation.activationId)
      assert.equal(dispatches.filter(item => item.kind === 'focus' && item.automationId === headingId && item.succeeded === true).length, 1)
      const announcements = dispatches.filter(item => item.kind === 'announcement')
      assert.equal(announcements.length, 1, 'exactly one announcement dispatch is required per activation')
      assert.equal(announcements[0].text, options.expectedAnnouncement, 'announcement dispatch must match generated localization')
    }
  }
  const initial = await poll('initial notification accessibility dispatch', report, value => value.accessibilityDispatches?.some(item => item.kind === 'announcement'))
  assertDispatches(initial)
  assert.equal(initial.notificationRouteActive, true, 'initial notification route must be active')
  assert.ok(typeof initial.initialPreferences.moderation_email_timezone === 'string' && initial.initialPreferences.moderation_email_timezone.length > 0, 'canonical fixture must provide a saved timezone')
  assert.equal(initial.patches.length, 0, 'notification load must not mutate saved preferences')
  state = initial.initialPreferences
  patchCount = 0
  beginPhase('mutations', 45_000)
  await toggle('engagement')
  passed('engagement-mutation-persisted')
  if (!state.is_moderation_emails_enabled) await toggle('moderation')
  await schedule(true)
  await toggle('moderation')
  await schedule(false)
  await toggle('moderation')
  await schedule(true)
  passed('moderation-visibility-and-hidden-preservation')
  beginPhase('departure', 10_000)
  await execute('deepLink', { url: 'voucha://my/preferences', ...(options.bundleId ? { bundleId: options.bundleId } : {}) })
  // Settings routes share a native page: Mac2 displayed reports existence,
  // not whether the notification section is logically active. Read the app's
  // applied-route audit instead of repeatedly snapshotting a retained heading.
  await poll('departure from notification settings', report, value => value.notificationRouteActive === false)
  beginPhase('return', 10_000)
  await execute('deepLink', { url: 'voucha://my/notification-settings', ...(options.bundleId ? { bundleId: options.bundleId } : {}) })
  const activated = await poll('canonical notification route activation', report, value => {
    const latest = value.activations?.at(-1)
    return value.notificationRouteActive === true && value.activations.length > initial.activations.length && value.accessibilityDispatches?.some(item => item.activationId === latest.activationId && item.kind === 'announcement')
  })
  assert.equal(truth(await visible(await find(headingId))), true, 'notification heading was not visible after return')
  assert.equal(activated.activations.length, initial.activations.length + 1, 'deep link must activate the canonical route exactly once')
  assertDispatches(activated)
  await sleep(250)
  assertDeadline()
  const final = await report()
  assertDispatches(final)
  assert.equal(final.patches.length, patchCount, 'route activation unexpectedly mutated preferences')
  assertDeadline()
  passed('canonical-route-focus-and-announcement-dispatched')
  return checks
}

function truth(value) {
  return value === true || value === 1 || ['true', '1', 'yes'].includes(String(value ?? '').toLowerCase())
}

function switchState(value) {
  if (value === false || value === 0 || ['false', '0', 'off', 'no'].includes(String(value ?? '').toLowerCase())) return false
  if (truth(value) || String(value).toLowerCase() === 'on') return true
  throw new Error(`Mac2 did not expose a boolean switch value: ${String(value)}`)
}
