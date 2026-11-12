import { copyFileSync, mkdirSync, readFileSync, readdirSync } from 'node:fs'
import { join, resolve } from 'node:path'

const assemblies = ['Voucha.Client.App', 'Voucha.Client.Core']
const outputPaths = [
  'dotnet-clients/src/Voucha.Client.App/bin/Release/net10.0-windows10.0.26100.0/win-x64',
  'dotnet-clients/src/Voucha.Client.Core/bin/Release/net10.0',
]

function findPairs(directory, pairs) {
  let entries
  try {
    entries = readdirSync(directory, { withFileTypes: true })
  } catch (error) {
    if (error.code === 'ENOENT') return
    throw error
  }
  for (const entry of entries) {
    const path = join(directory, entry.name)
    if (entry.isSymbolicLink()) throw new Error(`Symbolic link in build output: ${path}`)
    if (entry.isDirectory()) findPairs(path, pairs)
    if (!entry.isFile()) continue
    const assembly = assemblies.find(
      name => entry.name === `${name}.pdb` || entry.name === `${name}.dll`,
    )
    if (!assembly) continue
    const pair = pairs.get(join(directory, assembly)) ?? { assembly }
    pair[entry.name.endsWith('.pdb') ? 'pdb' : 'dll'] = path
    pairs.set(join(directory, assembly), pair)
  }
}

export function collectWindowsSymbols(root, destination) {
  const pairs = new Map()
  for (const path of outputPaths) findPairs(join(root, path), pairs)
  const collected = new Map()
  for (const pair of pairs.values()) {
    if (!pair.pdb || !pair.dll) throw new Error(`Missing DLL/PDB pair for ${pair.assembly}`)
    const contents = { pdb: readFileSync(pair.pdb), dll: readFileSync(pair.dll) }
    const previous = collected.get(pair.assembly)
    if (previous && (!contents.pdb.equals(previous.pdb) || !contents.dll.equals(previous.dll))) {
      throw new Error(`Conflicting build outputs for ${pair.assembly}`)
    }
    collected.set(pair.assembly, { ...contents, pair })
  }
  for (const assembly of assemblies) {
    if (!collected.has(assembly)) throw new Error(`Missing Windows Release outputs for ${assembly}`)
  }
  mkdirSync(destination, { recursive: false })
  for (const [assembly, { pair }] of collected) {
    for (const extension of ['pdb', 'dll']) {
      copyFileSync(pair[extension], join(destination, `${assembly}.${extension}`))
    }
  }
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  if (process.argv.length !== 4)
    throw new Error('Usage: sentry-windows-symbols.mjs ROOT DESTINATION')
  collectWindowsSymbols(resolve(process.argv[2]), resolve(process.argv[3]))
}
