import { authNewsBudgetMs, authNewsTypingMs, createDeadline } from './catalyst-gui-deadline.mjs'

export async function assertAuthNewsControls(request, sessionId, openNews, budget = createDeadline(authNewsBudgetMs)) {
  const checks = []
  const passed = name => checks.push({ name, status: 'passed' })
  const path = (element, command) => `/session/${sessionId}/element/${encodeURIComponent(element)}/${command}`
  async function find(id) {
    const payload = await request('POST', `/session/${sessionId}/element`, { using: 'accessibility id', value: id })
    const element = payload.value?.ELEMENT ?? payload.value?.['element-6066-11e4-a52e-4f735466cecf']
    if (!element) throw new Error(`${id}: missing element id`)
    return element
  }
  async function attribute(element, name) {
    const payload = await request('GET', path(element, `attribute/${name}`))
    return payload.value?.value ?? payload.value
  }
  async function until(description, action, timeout = 15_000) {
    const deadline = Date.now() + timeout
    let last
    while (Date.now() < deadline) {
      budget.remaining()
      try { if (await action()) { budget.remaining(); return } } catch (error) { last = error; budget.remaining() }
      await budget.pause(Math.min(400, Math.max(1, deadline - Date.now())))
    }
    throw new Error(`${description} did not settle: ${last ?? 'predicate remained false'}`)
  }
  const visible = async id => {
    const element = await find(id)
    const payload = await request('GET', path(element, 'displayed'))
    const value = payload.value
    return value === true || value === 'true' || value === 1 || value === '1'
  }
  async function hidden(id) {
    try { return !await visible(id) }
    catch (error) {
      if (/no such element/i.test(String(error))) return true
      throw error
    }
  }
  async function text(id) {
    const element = await find(id)
    return String(await attribute(element, 'value') || await attribute(element, 'label') || '')
  }
  async function enter(id, value) {
    const element = await find(id)
    await request('POST', path(element, 'click'), {})
    await request('POST', path(element, 'value'), { text: value }, authNewsTypingMs)
  }
  async function click(id) { await request('POST', path(await find(id), 'click'), {}) }

  await until('anonymous Session', async () => await visible('auth-session-heading') && /signed out/i.test(await text('auth-session-label')), 75_000)
  passed('anonymous-session-visible')
  await enter('auth-email-address', 'fixture@example.test')
  await click('auth-send-email-code')
  await until('accepted OTP request', () => visible('auth-resend-email-code'))
  passed('email-otp-request-accepted')
  await enter('auth-one-time-code', 'ABCDEFGHJK')
  await click('auth-verify-email-code')
  await until('authenticated Session', async () => /signed in as alice/i.test(await text('auth-session-label')))
  passed('email-otp-session-authenticated')
  if (typeof openNews !== 'function') throw new Error('Production news navigation callback required')
  await openNews()
  await until('canonical news row', async () => await visible('news-item-title-item-1') && await text('news-item-title-item-1') === 'Test Article')
  passed('news-fixture-row-visible')
  if (!await hidden('news-related-title-item-peer')) throw new Error('Related article was visible before expansion')
  await click('story-related-articles')
  await until('expanded related article', () => visible('news-related-title-item-peer'))
  if (await text('news-related-title-item-peer') !== 'Related cluster article') throw new Error('Wrong related article')
  passed('related-article-expanded')
  await click('story-related-articles')
  await until('collapsed related article', () => hidden('news-related-title-item-peer'))
  if (!await visible('news-item-title-item-1')) throw new Error('Primary article disappeared after collapse')
  passed('related-article-collapsed')
  return checks
}
