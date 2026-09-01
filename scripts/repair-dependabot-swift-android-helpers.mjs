import { createHash } from 'node:crypto'

export function assertion(condition, message) {
  if (!condition) throw new Error(message)
}

export function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex')
}

async function responseBytes(response, url) {
  assertion(response?.ok !== false, `failed to download ${url}`)
  if (typeof response.arrayBuffer === 'function')
    return new Uint8Array(await response.arrayBuffer())
  if (typeof response.text === 'function') return new TextEncoder().encode(await response.text())
  throw new Error(`download response for ${url} has no body reader`)
}

export async function fetchBytes(url, fetchImpl = globalThis.fetch) {
  assertion(typeof fetchImpl === 'function', 'a fetch implementation is required')
  const response = await fetchImpl(url, { headers: { accept: 'application/octet-stream' } })
  return responseBytes(response, url)
}
