import { cp, lstat, mkdir, readdir, readFile } from 'node:fs/promises'
import { isAbsolute, join, relative, resolve, sep } from 'node:path'
import { prefixedFailure } from './prefixed-failure.mjs'

const fail = prefixedFailure('Native contract staging failed')

export function contractPathComponents(path) {
  return path.split('/')
}

export function absoluteDirectoryPath(value, label) {
  if (typeof value !== 'string' || value.includes('\0') || value.trim() === '')
    fail(`${label} is required`)
  if (!isAbsolute(value.trim())) fail(`${label} must be an absolute path`)
  const path = resolve(value.trim())
  if (path === resolve(sep)) fail(`${label} must not be the filesystem root`)
  return path
}

export async function info(path, label) {
  try {
    const result = await lstat(path)
    if (result.isSymbolicLink()) fail(`symbolic link found at ${label}`)
    return result
  } catch (error) {
    if (error.code === 'ENOENT') return undefined
    throw error
  }
}

export async function realDirectory(path, label) {
  const result = await info(path, label)
  if (!result) fail(`missing directory at ${label}`)
  if (!result.isDirectory()) fail(`expected directory at ${label}`)
}

export async function descendantInfo(root, child, label) {
  await realDirectory(root, `${label} root`)
  let current = root
  for (const component of contractPathComponents(child)) {
    current = join(current, component)
    const result = await info(current, `${label}/${component}`)
    if (!result) return undefined
    if (!result.isDirectory()) fail(`expected directory at ${label}/${component}`)
  }
  return true
}

export async function tree(root, label) {
  await realDirectory(root, label)
  async function walk(directory) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name)
      if (entry.isSymbolicLink()) fail(`symbolic link found at ${label}/${entry.name}`)
      if (entry.isDirectory()) await walk(path)
      else if (!entry.isFile()) fail(`unsupported filesystem entry at ${label}/${entry.name}`)
    }
  }
  await walk(root)
}

export async function declaredPaths(configurationPath, expectedPaths) {
  let configuration
  try {
    configuration = JSON.parse(await readFile(configurationPath, 'utf8'))
  } catch (error) {
    fail(`cannot read contract configuration: ${error.message}`)
  }
  if (JSON.stringify(configuration.paths) !== JSON.stringify(expectedPaths))
    fail('contract configuration paths do not match the client contract')
  return configuration.paths
}

function within(root, path) {
  const child = relative(root, path)
  return child !== '' && child !== '..' && !child.startsWith(`..${sep}`) && !isAbsolute(child)
}

export async function emptyOutputRoot(root) {
  await realDirectory(root, 'stage output root')
  if ((await readdir(root)).length !== 0) fail('stage output root must be empty')
}

export async function copyDirectory(sourceRoot, destinationRoot, path) {
  const source = join(sourceRoot, ...contractPathComponents(path))
  const destination = join(destinationRoot, ...contractPathComponents(path))
  await tree(source, `Filaments contract ${path}`)
  await mkdir(resolve(destination, '..'), { recursive: true })
  await cp(source, destination, { recursive: true, dereference: false, errorOnExist: true })
  await tree(destination, `staged contract ${path}`)
}

export async function assertOnlyDeclaredPaths(root, declared) {
  async function walk(directory, prefix = '') {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const child = prefix ? `${prefix}/${entry.name}` : entry.name
      if (entry.isSymbolicLink()) fail(`symbolic link found at staged contract ${child}`)
      const permitted = declared.some(
        path => path === child || path.startsWith(`${child}/`) || child.startsWith(`${path}/`),
      )
      if (!permitted) fail(`unexpected staged contract path ${child}`)
      if (entry.isDirectory()) await walk(join(directory, entry.name), child)
      else if (!entry.isFile()) fail(`unsupported filesystem entry at staged contract ${child}`)
    }
  }
  await walk(root)
}

export function pathsOverlap(root, path) {
  return path === root || within(root, path) || within(path, root)
}
