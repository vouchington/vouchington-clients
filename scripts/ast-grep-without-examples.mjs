import { spawnSync } from 'node:child_process'
import { cp, mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'

const examplesMarker = /^examples:\s*$/m

// ast-grep rejects the top-level `examples:` field that `vouchington ast-grep-examples` reads, so
// scans run against a copy of the rule tree with the examples block removed.
export function stripExamples(source) {
  const match = examplesMarker.exec(source)
  return match ? source.slice(0, match.index) : source
}

export async function writeScanConfig(root, directory) {
  const rulesDirectory = join(directory, 'rules')
  await cp(join(root, 'ast-grep-rules'), rulesDirectory, { recursive: true })
  for (const file of await readdir(rulesDirectory)) {
    const path = join(rulesDirectory, file)
    await writeFile(path, stripExamples(await readFile(path, 'utf8')))
  }
  const config = (await readFile(join(root, 'sgconfig.yml'), 'utf8')).replace(
    /^ruleDirs:\n(?: {2}- .*\n)+/m,
    `ruleDirs:\n  - ${JSON.stringify(rulesDirectory)}\n`,
  )
  const configPath = join(directory, 'sgconfig.yml')
  await writeFile(configPath, config)
  return configPath
}

export async function scanWithoutExamples(astGrep, args, root = resolve('.')) {
  const directory = await mkdtemp(join(tmpdir(), 'voucha-ast-grep-scan-'))
  try {
    const configPath = await writeScanConfig(root, directory)
    return spawnSync(astGrep, ['scan', '--config', configPath, ...args], {
      cwd: root,
      encoding: 'utf8',
      stdio: 'pipe',
    })
  } finally {
    await rm(directory, { recursive: true, force: true })
  }
}
