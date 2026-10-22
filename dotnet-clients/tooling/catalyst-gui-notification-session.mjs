const engagementId = 'notification-engagement'
const newsDigestId = 'notification-news-digest'
const headingId = 'notification-preferences-heading'

export async function assertNotificationControls(request, sessionId) {
  await findElement(request, sessionId, headingId, 45_000)
  const engagement = await findElement(request, sessionId, engagementId, 15_000)
  await waitUntil(request, sessionId, engagement, 'enabled', isEnabled, 15_000)
  const before = switchState(await attribute(request, sessionId, engagement, 'value'))
  if (before === undefined) throw new Error('Mac2 did not expose the engagement switch value')
  await request('POST', elementPath(sessionId, engagement, 'click'), {})
  const after = await waitForSwitchState(request, sessionId, engagement, !before, 15_000)
  if (after === before) throw new Error(`engagement switch stayed ${before}`)
  const news = await findElement(request, sessionId, newsDigestId, 10_000)
  await waitUntil(request, sessionId, news, 'enabled', isEnabled, 10_000)
}

function elementPath(sessionId, element, command) {
  return `/session/${sessionId}/element/${encodeURIComponent(element)}/${command}`
}

async function findElement(request, sessionId, accessibilityId, timeoutMs) {
  const deadline = Date.now() + timeoutMs
  let lastError
  while (Date.now() < deadline) {
    try {
      const found = await request('POST', `/session/${sessionId}/element`, {
        using: 'accessibility id',
        value: accessibilityId,
      })
      const id = elementId(found)
      if (id) return id
    } catch (error) {
      lastError = error
    }
    await delay(500)
  }
  throw new Error(`${accessibilityId} was not exposed to the Mac2 session: ${lastError ?? 'missing element'}`)
}

async function waitUntil(request, sessionId, element, name, predicate, timeoutMs) {
  const deadline = Date.now() + timeoutMs
  let latest
  while (Date.now() < deadline) {
    latest = await attribute(request, sessionId, element, name)
    if (predicate(latest)) return latest
    await delay(500)
  }
  throw new Error(`${name} stayed ${JSON.stringify(latest)}`)
}

async function waitForSwitchState(request, sessionId, element, expected, timeoutMs) {
  const deadline = Date.now() + timeoutMs
  let latest
  while (Date.now() < deadline) {
    latest = switchState(await attribute(request, sessionId, element, 'value'))
    if (latest === expected) return latest
    await delay(500)
  }
  throw new Error(`engagement switch stayed ${latest}; expected ${expected}`)
}

async function attribute(request, sessionId, element, name) {
  const payload = await request('GET', elementPath(sessionId, element, `attribute/${name}`))
  const value = payload.value
  if (value && typeof value === 'object' && 'value' in value) return value.value
  return value
}

function elementId(payload) {
  const value = payload.value
  if (!value || typeof value !== 'object') return undefined
  return value.ELEMENT ?? value['element-6066-11e4-a52e-4f735466cecf']
}

function isEnabled(value) {
  if (value === true || value === 1) return true
  const text = String(value ?? '').trim().toLowerCase()
  return text === 'true' || text === '1' || text === 'yes'
}

function switchState(value) {
  if (value === true || value === 1) return true
  if (value === false || value === 0) return false
  const text = String(value ?? '').trim().toLowerCase()
  if (['1', 'true', 'on', 'yes'].includes(text)) return true
  if (['0', 'false', 'off', 'no'].includes(text)) return false
  return undefined
}

function delay(ms) {
  return new Promise(resolve => {
    setTimeout(resolve, ms)
  })
}
