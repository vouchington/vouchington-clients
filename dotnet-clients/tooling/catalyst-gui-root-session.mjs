const rootControlIds = ['auth-session-heading', 'auth-email-address', 'auth-send-email-code']

export async function assertRootShellControls(request, sessionId) {
  // Match the existing notification preflight's initial readiness budget.
  await request('POST', `/session/${sessionId}/timeouts`, { implicit: 75_000 })
  for (const accessibilityId of rootControlIds) {
    const found = await request('POST', `/session/${sessionId}/element`, {
      using: 'accessibility id',
      value: accessibilityId,
    })
    const element = found.value?.ELEMENT ?? found.value?.['element-6066-11e4-a52e-4f735466cecf']
    if (!element) throw new Error(`root control ${accessibilityId} was not exposed to Mac2`)
    const path = `/session/${sessionId}/element/${encodeURIComponent(element)}`
    const displayed = await request('GET', `${path}/displayed`)
    if (displayed.value !== true) throw new Error(`root control ${accessibilityId} was not visible`)
    if (accessibilityId === 'auth-session-heading')
      await request('POST', `/session/${sessionId}/timeouts`, { implicit: 10_000 })
    if (accessibilityId === 'auth-send-email-code') {
      const enabled = await request('GET', `${path}/enabled`)
      if (enabled.value !== true) throw new Error('root email sign-in action was not enabled')
    }
  }
}
