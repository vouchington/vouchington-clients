import { execFileSync } from 'node:child_process'
import { stat } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const expectedHooksPath = '.husky/_'

async function requireFile(path, description, executable = false) {
  let file
  try {
    file = await stat(path)
  } catch {
    throw new Error(`${description} is missing: ${path}`)
  }
  if (!file.isFile()) throw new Error(`${description} is not a file: ${path}`)
  if (executable && process.platform !== 'win32' && !(file.mode & 0o111)) {
    throw new Error(`${description} is not executable: ${path}`)
  }
}

export async function verifyHookInstall(directory = process.cwd()) {
  let configuredPath
  try {
    configuredPath = execFileSync('git', ['config', '--get', 'core.hooksPath'], {
      cwd: directory,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
    }).trim()
  } catch {
    throw new Error(`core.hooksPath is unavailable; expected ${expectedHooksPath}`)
  }
  if (configuredPath.replaceAll('\\', '/') !== expectedHooksPath) {
    throw new Error(`core.hooksPath must be ${expectedHooksPath}; found ${configuredPath}`)
  }

  await requireFile(join(directory, '.husky/pre-push'), 'tracked pre-push hook')
  await requireFile(join(directory, expectedHooksPath, 'h'), 'Husky hook loader')
  await requireFile(join(directory, expectedHooksPath, 'pre-push'), 'pre-push wrapper', true)
}

async function prepare() {
  if (process.env.HUSKY === '0') return
  const { default: husky } = await import('husky')
  const result = husky()
  if (result) throw new Error(`Husky hook setup failed: ${result.trim()}`)
  await verifyHookInstall()
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  prepare().catch(error => {
    process.stderr.write(`${error.message}\n`)
    process.exitCode = 1
  })
}
