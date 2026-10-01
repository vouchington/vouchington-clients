import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'

const USAGE = 'Usage: github-collaborators.mjs <owner/repo> <authors.json> [--allow-bots]'

// The legacy `permission` field maps maintain to write and triage to read.
const TRUSTED_PERMISSIONS = new Set(['admin', 'write'])

export function isTrustedPermission(permission) {
  return TRUSTED_PERMISSIONS.has(permission)
}

function isDefinitiveNotFound(error) {
  return typeof error?.stderr === 'string' && /HTTP 404/.test(error.stderr)
}

export function fetchPermissionWithGh(repository, login) {
  try {
    return execFileSync(
      'gh',
      ['api', `repos/${repository}/collaborators/${login}/permission`, '--jq', '.permission'],
      { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] },
    ).trim()
  } catch (error) {
    // Non-collaborators of a private repository and GitHub App bots have no permission record.
    if (isDefinitiveNotFound(error)) return 'none'
    throw error
  }
}

/**
 * Split GitHub authors into collaborators with write access and everyone else.
 * Any lookup failure other than a definitive 404 throws, so callers fail closed.
 */
export function classifyAuthors(authors, { allowBots = false, fetchPermission }) {
  if (!Array.isArray(authors)) throw new Error('authors must be an array')
  const trusted = new Set()
  const untrusted = new Set()
  const seen = new Set()
  for (const author of authors) {
    const login = author?.login
    if (typeof login !== 'string' || login === '') throw new Error('author login is missing')
    if (seen.has(login)) continue
    seen.add(login)
    if (author.type === 'Bot') {
      ;(allowBots ? trusted : untrusted).add(login)
    } else if (isTrustedPermission(fetchPermission(login))) {
      trusted.add(login)
    } else {
      untrusted.add(login)
    }
  }
  return { trusted: [...trusted].sort(), untrusted: [...untrusted].sort() }
}

export function runCollaboratorsCli(argv = process.argv, fetchPermission = fetchPermissionWithGh) {
  const [repository, authorsPath, ...flags] = argv.slice(2)
  if (!/^[\w-][\w.-]*\/[\w-][\w.-]*$/u.test(repository ?? '') || !authorsPath) {
    throw new Error(USAGE)
  }
  if (flags.some(flag => flag !== '--allow-bots')) throw new Error(USAGE)
  const authors = JSON.parse(readFileSync(authorsPath, 'utf8'))
  return JSON.stringify(
    classifyAuthors(authors, {
      allowBots: flags.includes('--allow-bots'),
      fetchPermission: login => fetchPermission(repository, login),
    }),
  )
}

if (import.meta.main) {
  try {
    process.stdout.write(runCollaboratorsCli())
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`)
    process.exitCode = 1
  }
}
