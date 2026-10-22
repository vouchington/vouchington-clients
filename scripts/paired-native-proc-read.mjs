import { open } from 'node:fs/promises'

function abortError() {
  const error = new Error('lineage read aborted')
  error.name = 'AbortError'
  return error
}

export async function readBoundedFile(path, maxBytes, signal) {
  if (!Number.isSafeInteger(maxBytes) || maxBytes < 1) throw abortError()
  if (signal?.aborted) throw abortError()
  const handle = await open(path, 'r')
  try {
    if (signal?.aborted) throw abortError()
    const buffer = Buffer.alloc(maxBytes)
    const { bytesRead } = await handle.read({
      buffer,
      offset: 0,
      length: maxBytes,
      position: 0,
      signal,
    })
    return buffer.subarray(0, bytesRead)
  } finally {
    await handle.close()
  }
}

export async function runBounded(work, milliseconds) {
  const controller = new AbortController()
  let timer
  const timeout = new Promise(resolve => {
    timer = setTimeout(() => {
      controller.abort()
      resolve({ timedOut: true })
    }, milliseconds)
    timer.unref?.()
  })
  const scan = Promise.resolve()
    .then(() => work(controller.signal))
    .then(value => ({ timedOut: false, value }))
    .catch(error => ({ timedOut: false, error }))
  try {
    const outcome = await Promise.race([scan, timeout])
    if (outcome.timedOut) return { timedOut: true }
    if (outcome.error) throw outcome.error
    return { value: outcome.value }
  } finally {
    clearTimeout(timer)
  }
}
