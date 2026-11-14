import { spawn } from 'node:child_process'
import { chmod, mkdir, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'

export async function writeTree(root, files) {
  for (const [path, contents] of Object.entries(files)) {
    const file = join(root, path)
    await mkdir(dirname(file), { recursive: true })
    await writeFile(file, contents)
  }
}

export async function writeExecutable(path, contents) {
  await writeFile(path, contents)
  await chmod(path, 0o755)
}

export async function writeBashExecutable(path, contents) {
  await writeExecutable(path, `#!/bin/bash\nset -euo pipefail\n${contents}\n`)
}

// Captures a child process that pipes stdout and stderr. A start failure
// rejects; the later close event from that failure does not also resolve.
export function captureSpawn(command, args, options) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, options)
    let stdout = ''
    let stderr = ''
    let failed = false
    child.stdout.setEncoding('utf8').on('data', chunk => {
      stdout += chunk
    })
    child.stderr.setEncoding('utf8').on('data', chunk => {
      stderr += chunk
    })
    child.on('error', error => {
      failed = true
      reject(error)
    })
    child.on('close', code => {
      if (!failed) resolve({ code, stdout, stderr })
    })
  })
}
