import { execFileSync } from 'node:child_process'
import { lstat, readFile } from 'node:fs/promises'
import { fail, requiredString } from './paired-contract-policy.mjs'

export function git(root, args) {
  return execFileSync('git', ['-C', root, ...args], { encoding: 'utf8' }).trim()
}

export async function readJson(path, label) {
  try {
    const info = await lstat(path)
    if (!info.isFile() || info.isSymbolicLink()) fail(`${label} must be a regular file`)
    return JSON.parse(await readFile(path, 'utf8'))
  } catch (error) {
    fail(`cannot read ${label}: ${error.message}`)
  }
}

export function decodeSnapshot() {
  const encoded = requiredString(process.env.PAIR_SNAPSHOT_BASE64, 'captured pair snapshot')
  const bytes = Buffer.from(encoded, 'base64url')
  if (bytes.toString('base64url') !== encoded) fail('captured pair snapshot encoding is invalid')
  try {
    return JSON.parse(bytes.toString('utf8'))
  } catch (error) {
    fail(`captured pair snapshot is invalid: ${error.message}`)
  }
}
