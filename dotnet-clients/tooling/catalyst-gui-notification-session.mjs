import assert from 'node:assert/strict'

const headingId = 'notification-preferences-heading'
const fieldNames = {
  engagement: 'is_engagement_emails_enabled',
  news: 'news_digest_frequency',
  moderation: 'is_moderation_emails_enabled',
  community: 'community_digest_frequency',
  cadence: 'moderation_email_cadence',
  days: 'moderation_email_days_of_week',
  time: 'moderation_email_time_of_day',
  timezone: 'moderation_email_timezone',
}

// These are native keyboard gestures, not fixture commands. The persisted report is
// mandatory: an accepted key/click alone never establishes that a setting changed.
// Mac2 commands: https://appium.github.io/appium-mac2-driver/latest/reference/execute-methods/
export async function assertNotificationControls(request, sessionId, options = {}) {
  if (typeof options.readEvidence !== 'function') throw new Error('readEvidence is required for persisted notification acceptance')
  assert.ok(typeof options.expectedAnnouncement === 'string' && options.expectedAnnouncement.length > 0, 'expectedAnnouncement from generated localization is required')
  const now = options.now ?? Date.now
  const sleep = options.sleep ?? (ms => new Promise(resolve => setTimeout(resolve, ms)))
  const deadline = now() + (options.timeoutMs ?? 140_000)
  const checks = []
  const passed = name => checks.push({ name, status: 'passed' })
  const command = async (method, path, body) => {
    if (now() >= deadline) throw new Error('notification session exceeded its shared deadline')
    const response = await request(method, `/session/${sessionId}${path}`, body)
    if (response?.value?.error) throw new Error(`Mac2 ${response.value.error}: ${response.value.message ?? path}`)
    return response?.value
  }
  const poll = async (description, read, accept, budget = 10_000) => {
    const end = Math.min(deadline, now() + budget)
    while (now() < end) {
      const result = await read()
      if (accept(result)) return result
      await sleep(Math.min(250, end - now()))
    }
    throw new Error(`${description} did not settle before the deadline`)
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
    const value = await options.readEvidence()
    assert.equal(value?.schemaVersion, 1, 'unsupported notification app report')
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
  const select = async (field, choices, target) => {
    const element = await ready(`notification-${field === 'news' || field === 'community' ? `${field}-digest` : field}`)
    const current = choices.indexOf(state[fieldNames[field]])
    const next = choices.indexOf(target)
    assert.ok(current >= 0 && next >= 0 && current !== next, `unsupported ${field} native picker value`)
    await mutate(field, target, async () => {
      await click(element)
      await execute('keys', { keys: [
        ...Array(Math.abs(next - current)).fill(next > current ? 'XCUIKeyboardKeyDownArrow' : 'XCUIKeyboardKeyUpArrow'),
        'XCUIKeyboardKeyReturn',
      ] })
    })
    await poll(`${field} enabled after selection`, () => attr(element, 'enabled'), truth)
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
  const daysVisibility = expected => visibility(Array.from({ length: 7 }, (_, index) => `notification-day-${index + 1}`), expected)

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
  state = initial.patches.at(-1)?.after ?? initial.initialPreferences
  patchCount = initial.patches.length
  // Startup timezone initialization is allowed, but each later gesture is audited.
  await toggle('engagement')
  passed('engagement-mutation-persisted')
  const digestChoices = ['none', 'daily', 'weekly']
  await select('news', digestChoices, state.news_digest_frequency === 'daily' ? 'weekly' : 'daily')
  passed('news-digest-mutation-persisted')
  await select('community', digestChoices, state.community_digest_frequency === 'daily' ? 'weekly' : 'daily')
  passed('community-digest-mutation-persisted')
  if (!state.is_moderation_emails_enabled) await toggle('moderation')
  await schedule(true)
  await toggle('moderation')
  await schedule(false)
  await toggle('moderation')
  await schedule(true)
  passed('moderation-visibility-and-hidden-preservation')
  const cadenceChoices = ['daily', 'selected_days', 'weekly']
  if (state.moderation_email_cadence !== 'daily') await select('cadence', cadenceChoices, 'daily')
  await daysVisibility(false)
  await select('cadence', cadenceChoices, 'selected_days')
  await daysVisibility(true)
  passed('cadence-mutation-persisted')
  const days = state.moderation_email_days_of_week
  const day = [1, 2, 3, 4, 5, 6, 7].find(value => !days.includes(value)) ?? days[0]
  const changedDays = days.includes(day) ? days.filter(value => value !== day) : [...days, day].sort((a, b) => a - b)
  assert.ok(changedDays.length > 0, 'cannot remove the final selected moderation day')
  await mutate('days', changedDays, async () => click(await ready(`notification-day-${day}`)))
  passed('days-mutation-persisted')
  const time = await ready('notification-time')
  // Do not send text into a date-picker container and mistake a successful HTTP
  // response for editing. Require an actual editable native control.
  const type = await attr(time, 'amType')
  assert.ok(['XCUIElementTypeTextField', 'XCUIElementTypeTimeField'].includes(type), `unsupported native time control ${String(type)}; inspect the Mac2 tree before adding a control adapter`)
  const targetTime = state.moderation_email_time_of_day === '09:30' ? '10:30' : '09:30'
  await mutate('time', targetTime, async () => {
    await click(time)
    await execute('keys', { elementId: time, keys: [{ key: 'a', modifierFlags: 16 }, targetTime, 'XCUIKeyboardKeyReturn'] })
  })
  passed('time-mutation-persisted')
  const zone = state.moderation_email_timezone === 'UTC' ? 'America/Los_Angeles' : 'UTC'
  await mutate('timezone', zone, async () => {
    await click(await ready('notification-timezone'))
    // Timezone names are protocol values, not translated labels. Query the actual
    // open native menu rather than guessing its runtime-generated item ordering.
    const candidates = await command('POST', '/elements', { using: 'xpath', value: `//*[@label="${zone}"]` })
    assert.ok(Array.isArray(candidates), 'Mac2 timezone menu response must be an array')
    const shown = []
    for (const candidate of candidates) {
      const element = candidate['element-6066-11e4-a52e-4f735466cecf'] ?? candidate.ELEMENT
      if (truth(await visible(element))) shown.push(element)
    }
    assert.equal(shown.length, 1, 'timezone must expose exactly one visible native menu option')
    assert.equal(truth(await attr(shown[0], 'enabled')), true, 'timezone menu option was disabled')
    await click(shown[0])
  })
  passed('timezone-mutation-persisted')
  await execute('deepLink', { url: 'voucha://my/preferences', ...(options.bundleId ? { bundleId: options.bundleId } : {}) })
  await poll('departure from notification settings', async () => {
    const values = await command('POST', '/elements', { using: 'accessibility id', value: headingId })
    assert.ok(Array.isArray(values), 'Mac2 heading query must return an array')
    return Promise.all(values.map(value => visible(value['element-6066-11e4-a52e-4f735466cecf'] ?? value.ELEMENT)))
  }, values => values.every(value => !truth(value)))
  await execute('deepLink', { url: 'voucha://my/notification-settings', ...(options.bundleId ? { bundleId: options.bundleId } : {}) })
  const activated = await poll('canonical notification route activation', report, value => {
    const latest = value.activations?.at(-1)
    return value.activations.length > initial.activations.length && value.accessibilityDispatches?.some(item => item.activationId === latest.activationId && item.kind === 'announcement')
  })
  assert.equal(truth(await visible(await find(headingId))), true, 'notification heading was not visible after return')
  assert.equal(activated.activations.length, initial.activations.length + 1, 'deep link must activate the canonical route exactly once')
  assertDispatches(activated)
  await sleep(250)
  assertDispatches(await report())
  assert.equal((await report()).patches.length, patchCount, 'route activation unexpectedly mutated preferences')
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
