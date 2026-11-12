// Child-process fake for the CLI orchestrator; this never counts as GUI evidence.
import { createServer } from 'node:http'
import { appendFileSync } from 'node:fs'
let requested = false
let authenticated = false
let expanded = false
const values = {}
const log = value => appendFileSync(process.env.CATALYST_FAKE_LOG, `${JSON.stringify(value)}\n`)
if (process.argv[2] === '--open') {
  log({ open: process.argv.slice(3) })
  process.exit(0)
}
log({ port: Number(process.argv[process.argv.indexOf('--port') + 1]) })
const server = createServer(async (req, res) => {
  let text = ''
  for await (const chunk of req) text += chunk
  const body = text ? JSON.parse(text) : {}
  log({ method: req.method, path: req.url, body })
  let value = null
  let status = 200
  if (req.url === '/status') value = { ready: true }
  else if (req.url === '/session') value = { sessionId: 'fake-session' }
  else if (req.method === 'DELETE') {
    value = null
  } else if (req.url.endsWith('/source')) {
    status = 500
    value = { error: 'diagnostic failure' }
  } else if (req.url.endsWith('/element')) value = { ELEMENT: body.value }
  else {
    const [, id, command] = req.url.match(/\/element\/([^/]+)\/(.*)/u) ?? []
    if (command === 'value') values[id] = body.text
    if (command === 'click') {
      if (id === 'auth-send-email-code') requested = true
      if (id === 'auth-verify-email-code') authenticated = true
      if (id === 'story-related-articles') expanded = !expanded
    }
    if (command === 'displayed')
      value =
        id === 'news-related-title-item-peer'
          ? expanded
          : id === 'auth-resend-email-code'
            ? requested
            : true
    if (command === 'attribute/value')
      value =
        id === 'auth-session-label'
          ? authenticated
            ? 'Signed in as alice'
            : 'Signed out'
          : id === 'news-item-title-item-1'
            ? 'Test Article'
            : id === 'news-related-title-item-peer'
              ? process.env.CATALYST_FAKE_FAIL === 'assertion'
                ? 'Wrong article'
                : 'Related cluster article'
              : ''
  }
  res.writeHead(status, { 'content-type': 'application/json' })
  res.end(JSON.stringify({ value }))
})
server.listen(Number(process.argv[process.argv.indexOf('--port') + 1]), '127.0.0.1')
process.on('SIGTERM', () => {
  log({ stopped: true })
  server.close(() => process.exit(0))
})
