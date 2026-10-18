import { spawn } from 'node:child_process'

export function runDocker(args, { timeoutMs, stdoutLimit, stderrLimit }) {
  return new Promise((resolve, reject) => {
    const child = spawn('docker', args, {
      env: { PATH: process.env.PATH ?? '/usr/bin:/bin' },
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    const stdout = []
    const stderr = []
    let stdoutBytes = 0
    let stderrBytes = 0
    let timedOut = false
    const timeout = setTimeout(() => {
      timedOut = true
      child.kill('SIGKILL')
    }, timeoutMs)
    child.stdout.on('data', chunk => {
      stdoutBytes += chunk.length
      if (stdoutBytes > stdoutLimit) child.kill('SIGKILL')
      else stdout.push(chunk)
    })
    child.stderr.on('data', chunk => {
      if (stderrBytes < stderrLimit) {
        const remaining = stderrLimit - stderrBytes
        const accepted = chunk.subarray(0, remaining)
        stderr.push(accepted)
        stderrBytes += accepted.length
      }
    })
    child.once('error', error => {
      clearTimeout(timeout)
      reject(error)
    })
    child.once('close', (code, signal) => {
      clearTimeout(timeout)
      if (timedOut) reject(new Error('container execution timed out'))
      else if (stdoutBytes > stdoutLimit)
        reject(new Error('container response exceeded its byte limit'))
      else if (code !== 0)
        reject(
          new Error(
            `container execution failed (${signal ? `signal ${signal}` : `exit ${code}`}): ${
              Buffer.concat(stderr).toString('utf8').trim() || 'no diagnostic'
            }`,
          ),
        )
      else resolve(Buffer.concat(stdout, stdoutBytes))
    })
  })
}

export function removeOwnedContainer(name) {
  return new Promise((resolve, reject) => {
    const child = spawn('docker', ['rm', '--force', name], {
      env: { PATH: process.env.PATH ?? '/usr/bin:/bin' },
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    const stdout = []
    const stderr = []
    let outputBytes = 0
    const timeout = setTimeout(() => child.kill('SIGKILL'), 10_000)
    for (const stream of [child.stdout, child.stderr]) {
      stream.on('data', chunk => {
        outputBytes += chunk.length
        if (outputBytes <= 4096) (stream === child.stdout ? stdout : stderr).push(chunk)
        else child.kill('SIGKILL')
      })
    }
    child.once('error', error => {
      clearTimeout(timeout)
      reject(error)
    })
    child.once('close', (code, signal) => {
      clearTimeout(timeout)
      const message = Buffer.concat(stderr).toString('utf8').trim()
      if (code === 0 || /No such container/u.test(message)) resolve()
      else
        reject(
          new Error(
            `owned container cleanup failed (${signal ?? `exit ${code}`}): ${message || Buffer.concat(stdout).toString('utf8').trim() || 'no diagnostic'}`,
          ),
        )
    })
  })
}
