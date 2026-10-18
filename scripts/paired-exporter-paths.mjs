import { lstat, realpath } from 'node:fs/promises'
import { relative, sep, isAbsolute } from 'node:path'

function fail(message) {
  throw new Error(`Isolated native exporter failed: ${message}`)
}

export async function directory(value, label) {
  if (typeof value !== 'string' || value.trim() === '') fail(`${label} is required`)
  const suppliedInfo = await lstat(value)
  if (suppliedInfo.isSymbolicLink() || !suppliedInfo.isDirectory())
    fail(`${label} must be a real directory`)
  const path = await realpath(value)
  const info = await lstat(path)
  if (info.isSymbolicLink() || !info.isDirectory()) fail(`${label} must be a real directory`)
  if (/[\n\r,]/u.test(path)) fail(`${label} contains an unsupported mount character`)
  return path
}

export function overlaps(left, right) {
  const path = relative(left, right)
  return path === '' || (path !== '..' && !path.startsWith(`..${sep}`) && !isAbsolute(path))
}
