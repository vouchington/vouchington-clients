import { createHash } from 'node:crypto'
import { lstat, readdir, readFile } from 'node:fs/promises'
import { join } from 'node:path'
import { artifactPath, fail } from './contract-artifact-validation.mjs'

export async function info(path, label) {
  let result
  try {
    result = await lstat(path)
  } catch (error) {
    if (error.code === 'ENOENT') return undefined
    fail(`cannot inspect ${label}: ${error.message}`)
  }
  if (result.isSymbolicLink()) fail(`symbolic link found at ${label}`)
  return result
}
export async function realDirectory(path, label) {
  const result = await info(path, label)
  if (!result) fail(`missing directory at ${label}`)
  if (!result.isDirectory()) fail(`expected directory at ${label}`)
}
export async function descendantDirectory(root, child, label) {
  await realDirectory(root, `${label} root`)
  let current = root
  for (const component of child.split('/')) {
    current = join(current, component)
    await realDirectory(current, `${label}/${component}`)
  }
  return current
}
export async function treeFiles(root, label) {
  await realDirectory(root, label)
  const files = []
  async function walk(directory, prefix = '') {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name),
        child = prefix ? `${prefix}/${entry.name}` : entry.name
      if (entry.isSymbolicLink()) fail(`symbolic link found at ${label}/${child}`)
      else if (entry.isDirectory()) await walk(path, child)
      else if (entry.isFile()) files.push(child)
      else fail(`unsupported filesystem entry at ${label}/${child}`)
    }
  }
  await walk(root)
  return files.sort()
}
export async function fileRecord(root, path) {
  const contents = await readFile(join(root, ...path.split('/')))
  return {
    path,
    size: contents.byteLength,
    sha256: createHash('sha256').update(contents).digest('hex'),
  }
}
export async function artifactEntries(root) {
  await realDirectory(root, 'artifact root')
  const files = [],
    directories = []
  async function walk(directory, prefix = '') {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const child = prefix ? `${prefix}/${entry.name}` : entry.name,
        path = join(directory, entry.name)
      if (entry.isSymbolicLink()) fail(`symbolic link found at artifact path ${child}`)
      else if (entry.isDirectory()) {
        directories.push(child)
        await walk(path, child)
      } else if (entry.isFile()) files.push(child)
      else fail(`unsupported filesystem entry at artifact path ${child}`)
    }
  }
  await walk(root)
  return { directories: directories.sort(), files: files.sort() }
}
export async function verifyFiles(root, manifest) {
  const expected = new Set(['manifest.json', ...manifest.files.map(record => record.path)])
  const { files, directories } = await artifactEntries(root)
  for (const path of files) if (!expected.has(path)) fail(`unexpected artifact path: ${path}`)
  for (const path of expected) if (!files.includes(path)) fail(`missing artifact path: ${path}`)
  const expectedDirectories = new Set()
  for (const record of manifest.files) {
    const components = artifactPath(record.path).split('/')
    for (let index = 1; index < components.length; index += 1)
      expectedDirectories.add(components.slice(0, index).join('/'))
  }
  for (const path of directories)
    if (!expectedDirectories.has(path)) fail(`unexpected artifact directory: ${path}`)
  for (const record of manifest.files) {
    const actual = await fileRecord(root, record.path)
    if (actual.size !== record.size) fail(`size mismatch for ${record.path}`)
    if (actual.sha256 !== record.sha256) fail(`hash mismatch for ${record.path}`)
  }
}
