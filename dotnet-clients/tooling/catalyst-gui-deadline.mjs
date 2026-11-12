import { setTimeout as delay } from 'node:timers/promises'

export const authNewsBudgetMs = 150_000
export const authNewsCleanupMs = 10_000
export const authNewsTypingMs = 30_000

// One monotonic deadline is shared by startup, requests, navigation, and UI polling.
export function createDeadline(budgetMs, now = () => performance.now(), describe = () => '') {
  const expires = now() + budgetMs
  const controller = new AbortController()
  function remaining(cap = Infinity, reserve = 0) {
    if (controller.signal.aborted) throw controller.signal.reason
    const value = Math.ceil(expires - now() - reserve)
    if (value <= 0) throw new Error(`Catalyst auth/news overall deadline exceeded${describe()}`)
    return Math.min(value, cap)
  }
  async function run(action) {
    let timer
    try {
      const timeoutMs = remaining()
      const timeout = new Promise((_, reject) => {
        timer = setTimeout(() => {
          const error = new Error(`Catalyst auth/news overall deadline exceeded${describe()}`)
          controller.abort(error)
          reject(error)
        }, timeoutMs)
      })
      const result = await Promise.race([action(), timeout])
      remaining()
      return result
    } catch (error) {
      controller.abort(error)
      throw error
    } finally { clearTimeout(timer) }
  }
  async function pause(ms) {
    await delay(remaining(ms), undefined, { signal: controller.signal })
    remaining()
  }
  return { remaining, run, pause, signal: controller.signal }
}
