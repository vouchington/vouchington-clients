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
