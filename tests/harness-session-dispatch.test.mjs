import { mkdtempSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import assert from 'node:assert/strict'
import test from 'node:test'

import { dispatchHarnessSession } from '../scripts/harness-session-dispatch.mjs'

const sessionId = 'sess-0123abcd'
const sessionUrl = `https://harness.example.com/sessions/${sessionId}`

function jsonResponse(body, status = 200) {
  return new Response(JSON.stringify(body), {
    headers: { 'content-type': 'application/json' },
    status,
  })
}

function fakeFetch(...responses) {
  const calls = []
  const impl = async (...args) => {
    calls.push(args)
    const response = responses[calls.length - 1]
    if (!response) throw new Error(`fakeFetch called more times (${calls.length}) than stubbed`)
    return response
  }
  impl.calls = calls
  return impl
}

const baseEnvironment = {
  HARNESS_API_KEY: 'hns_secret',
  HARNESS_CONCURRENCY_ID: 'filaments-fix-123',
  HARNESS_DISPATCH_ENABLED: 'true',
  HARNESS_FALLBACKS: '[{"providerId":"prov-grok"},{"providerId":"prov-codex"}]',
  HARNESS_METADATA: '{"issueNumber":123}',
  HARNESS_PRIORITY: '20',
  HARNESS_PROMPT: 'Fix the issue',
  HARNESS_QUEUE_TTL_SECONDS: '3600',
  HARNESS_REF: 'refs/heads/main',
  HARNESS_REPOSITORY_ID: 'repo-filaments',
  HARNESS_REQUIRED_LABELS: '["filaments"]',
  HARNESS_TARGET: '{"providerId":"prov-cursor"}',
  HARNESS_TIMEOUT: '6300',
  HARNESS_URL: 'https://harness.example.com',
}

test('fresh dispatch: posts the documented session schema and writes outputs', async () => {
  const output = join(mkdtempSync(join(tmpdir(), 'harness-dispatch-')), 'output')
  const summary = join(mkdtempSync(join(tmpdir(), 'harness-summary-')), 'summary')
  const fetchImplementation = fakeFetch(
    jsonResponse({ created: true, id: sessionId, url: sessionUrl }, 201),
  )

  const result = await dispatchHarnessSession(
    { ...baseEnvironment, GITHUB_OUTPUT: output, GITHUB_STEP_SUMMARY: summary },
    fetchImplementation,
  )
  assert.deepEqual(result, { created: true, id: sessionId, url: sessionUrl })

  assert.equal(fetchImplementation.calls.length, 1)
  const [url, init] = fetchImplementation.calls[0]
  assert.equal(url, 'https://harness.example.com/api/v1/sessions')
  assert.equal(init.method, 'POST')
  const body = JSON.parse(String(init.body))
  assert.deepEqual(
    {
      concurrencyId: body.concurrencyId,
      fallbacks: body.fallbacks,
      metadata: body.metadata,
      priority: body.priority,
      prompt: body.prompt,
      queueTtlSeconds: body.queueTtlSeconds,
      ref: body.ref,
      repositoryId: body.repositoryId,
      requiredLabels: body.requiredLabels,
      source: body.source,
      target: body.target,
      timeout: body.timeout,
    },
    {
      concurrencyId: 'filaments-fix-123',
      fallbacks: [{ providerId: 'prov-grok' }, { providerId: 'prov-codex' }],
      metadata: { issueNumber: 123 },
      priority: 20,
      prompt: 'Fix the issue',
      queueTtlSeconds: 3600,
      ref: 'refs/heads/main',
      repositoryId: 'repo-filaments',
      requiredLabels: ['filaments'],
      source: 'webhook',
      target: { providerId: 'prov-cursor' },
      timeout: 6300,
    },
  )
  assert.equal(
    readFileSync(output, 'utf8'),
    `session-id=${sessionId}\nsession-url=${sessionUrl}\ncreated=true\n`,
  )
  const summaryText = readFileSync(summary, 'utf8')
  assert.ok(summaryText.includes(`[${sessionId}](${sessionUrl})`))
  assert.ok(summaryText.includes('`prov-cursor` → `prov-grok` → `prov-codex`'))
  assert.ok(!summaryText.includes('hns_secret'))
  assert.ok(!summaryText.includes('Fix the issue'))
})

test('fresh dispatch: resolves name-based targets through catalog fetches and formats every route kind', async () => {
  const summary = join(mkdtempSync(join(tmpdir(), 'harness-summary-')), 'summary')
  const fetchImplementation = fakeFetch(
    jsonResponse({ items: [{ id: 'prov-claude-1', name: 'claude' }] }),
    jsonResponse({ items: [{ id: 'cmd-codex-print-1', name: 'codex-print' }] }),
    jsonResponse({ created: true, id: sessionId, url: sessionUrl }, 201),
  )

  await dispatchHarnessSession(
    {
      ...baseEnvironment,
      GITHUB_STEP_SUMMARY: summary,
      HARNESS_FALLBACKS: '[{"commandName":"codex-print"},{"commandId":"cmd-existing"}]',
      HARNESS_TARGET: '{"providerName":"claude"}',
    },
    fetchImplementation,
  )

  assert.equal(fetchImplementation.calls[0][0], 'https://harness.example.com/api/v1/providers?limit=100')
  assert.equal(fetchImplementation.calls[1][0], 'https://harness.example.com/api/v1/commands?limit=100')
  const body = JSON.parse(String(fetchImplementation.calls[2][1].body))
  assert.deepEqual(body.target, { providerId: 'prov-claude-1' })
  assert.deepEqual(body.fallbacks, [
    { commandId: 'cmd-codex-print-1' },
    { commandId: 'cmd-existing' },
  ])
  assert.ok(readFileSync(summary, 'utf8').includes('`claude` → `codex-print` → `cmd-existing`'))
})

test('fresh dispatch: fails closed when a provider or command name resolves to more than one entry', async () => {
  const ambiguousProviderFetch = fakeFetch(
    jsonResponse({
      items: [
        { id: 'prov-claude-1', name: 'claude' },
        { id: 'prov-claude-2', name: 'claude' },
      ],
    }),
  )

  await assert.rejects(
    dispatchHarnessSession(
      { ...baseEnvironment, HARNESS_TARGET: '{"providerName":"claude"}' },
      ambiguousProviderFetch,
    ),
    err => err.code === 'AMBIGUOUS_PROVIDER_NAME',
  )

  const ambiguousCommandFetch = fakeFetch(
    jsonResponse({
      items: [
        { id: 'cmd-codex-print-1', name: 'codex-print' },
        { id: 'cmd-codex-print-2', name: 'codex-print' },
      ],
    }),
  )

  await assert.rejects(
    dispatchHarnessSession(
      { ...baseEnvironment, HARNESS_TARGET: '{"commandName":"codex-print"}' },
      ambiguousCommandFetch,
    ),
    err => err.code === 'AMBIGUOUS_COMMAND_NAME',
  )
})

test('fresh dispatch: omits the fallbacks field and route entry when none are configured', async () => {
  const fetchImplementation = fakeFetch(
    jsonResponse({ created: true, id: sessionId, url: sessionUrl }, 201),
  )

  await dispatchHarnessSession(
    { ...baseEnvironment, HARNESS_FALLBACKS: undefined },
    fetchImplementation,
  )

  const body = JSON.parse(String(fetchImplementation.calls[0][1].body))
  assert.deepEqual(body.fallbacks, [])
})

test('fresh dispatch: reports a deduplicated create without a provider route', async () => {
  const summary = join(mkdtempSync(join(tmpdir(), 'harness-summary-')), 'summary')
  const fetchImplementation = fakeFetch(
    jsonResponse({ created: false, id: sessionId, url: sessionUrl }, 200),
  )

  const result = await dispatchHarnessSession(
    { ...baseEnvironment, GITHUB_STEP_SUMMARY: summary },
    fetchImplementation,
  )
  assert.deepEqual(result, { created: false, id: sessionId, url: sessionUrl })

  const summaryText = readFileSync(summary, 'utf8')
  assert.ok(summaryText.includes('- Created: no'))
  assert.ok(summaryText.includes('retained from the existing session'))
})

const closedBeforeTransportCases = [
  ['a disabled dispatch', { HARNESS_DISPATCH_ENABLED: 'false' }, 'DISPATCH_DISABLED'],
  ['a missing HARNESS_URL', { HARNESS_URL: undefined }, 'MISSING_ENVIRONMENT_VALUE'],
  [
    'a HARNESS_URL with a path',
    { HARNESS_URL: 'https://harness.example.com/base' },
    'INVALID_HARNESS_URL',
  ],
  ['an oversized prompt', { HARNESS_PROMPT: 'x'.repeat(65_537) }, 'PROMPT_TOO_LARGE'],
  ['a blank concurrency id', { HARNESS_CONCURRENCY_ID: '   ' }, 'INVALID_CONCURRENCY_ID'],
  ['a missing HARNESS_TARGET', { HARNESS_TARGET: undefined }, 'MISSING_ENVIRONMENT_VALUE'],
  ['a malformed HARNESS_TARGET', { HARNESS_TARGET: '{"providerId":1}' }, 'INVALID_TARGET'],
  ['a non-array HARNESS_FALLBACKS', { HARNESS_FALLBACKS: '{}' }, 'INVALID_FALLBACKS'],
  ['a non-JSON HARNESS_METADATA', { HARNESS_METADATA: '{' }, 'INVALID_METADATA'],
  [
    'a HARNESS_QUEUE_TTL_SECONDS above the cap',
    { HARNESS_QUEUE_TTL_SECONDS: String(30 * 24 * 60 * 60 + 1) },
    'INVALID_QUEUE_TTL_SECONDS',
  ],
  [
    'a missing HARNESS_REPOSITORY_ID',
    { HARNESS_REPOSITORY_ID: undefined },
    'MISSING_ENVIRONMENT_VALUE',
  ],
]

for (const [name, override, code] of closedBeforeTransportCases) {
  test(`fresh dispatch: fails closed before transport for ${name}`, async () => {
    const fetchImplementation = fakeFetch()

    await assert.rejects(
      dispatchHarnessSession({ ...baseEnvironment, ...override }, fetchImplementation),
      err => err.code === code,
    )
    assert.equal(fetchImplementation.calls.length, 0)
  })
}

const resumeEnvironment = {
  HARNESS_API_KEY: 'hns_secret',
  HARNESS_CONCURRENCY_ID: 'filaments-fix-123',
  HARNESS_DISPATCH_ENABLED: 'true',
  HARNESS_PRIORITY: '20',
  HARNESS_PROMPT: 'Continue the fix',
  HARNESS_REPOSITORY_ID: 'repo-filaments',
  HARNESS_RESUME_SESSION_ID: sessionId,
  HARNESS_TIMEOUT: '6300',
  HARNESS_URL: 'https://harness.example.com',
}

test('resume: resumes without a dedupe pre-check and reports the resumed result', async () => {
  const summary = join(mkdtempSync(join(tmpdir(), 'harness-summary-')), 'summary')
  const fetchImplementation = fakeFetch(
    jsonResponse({ created: false, id: sessionId, url: sessionUrl }, 200),
  )

  const result = await dispatchHarnessSession(
    { ...resumeEnvironment, GITHUB_STEP_SUMMARY: summary },
    fetchImplementation,
  )
  assert.deepEqual(result, { created: false, id: sessionId, url: sessionUrl })

  assert.equal(fetchImplementation.calls.length, 1)
  const [url, init] = fetchImplementation.calls[0]
  assert.equal(url, `https://harness.example.com/api/v1/sessions/${sessionId}/resume`)
  assert.equal(init.method, 'POST')
  const resumeBody = JSON.parse(String(init.body))
  assert.deepEqual(resumeBody, {
    concurrencyId: 'filaments-fix-123',
    priority: 20,
    prompt: 'Continue the fix',
    timeout: 6300,
  })
  assert.ok(readFileSync(summary, 'utf8').includes('retained from the existing session'))
})

test('resume: takes the create path when HARNESS_RESUME_SESSION_ID is empty', async () => {
  const fetchImplementation = fakeFetch(
    jsonResponse({ created: true, id: sessionId, url: sessionUrl }, 201),
  )

  await dispatchHarnessSession(
    { ...baseEnvironment, HARNESS_RESUME_SESSION_ID: '' },
    fetchImplementation,
  )

  assert.equal(fetchImplementation.calls.length, 1)
  const [url, init] = fetchImplementation.calls[0]
  assert.equal(url, 'https://harness.example.com/api/v1/sessions')
  assert.equal(init.method, 'POST')
})

test('resume: fails closed for a malformed resume session id before transport', async () => {
  const fetchImplementation = fakeFetch()

  await assert.rejects(
    dispatchHarnessSession(
      { ...resumeEnvironment, HARNESS_RESUME_SESSION_ID: 'sess-123' },
      fetchImplementation,
    ),
    err => err.code === 'INVALID_RESUME_SESSION_ID',
  )
  assert.equal(fetchImplementation.calls.length, 0)
})
