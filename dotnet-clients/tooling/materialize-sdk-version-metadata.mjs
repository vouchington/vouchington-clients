import { spawnSync } from 'node:child_process'
import { lstatSync, mkdtempSync, readFileSync, realpathSync, rmSync, statSync } from 'node:fs'
import { join, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

export const sdkVersion = '10.0.401'
export const refPackVersion = '10.0.12'
export const sdkArchiveUrl =
  'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-osx-arm64.tar.gz'
export const sdkArchiveSha512 =
  '69f64eb00dc045398755c440b152225d544301a345a146a16e86a56a0c52b7c94b2c331520e976dbb821f18d31930aafbd25bb85961e3517e0665414ce0cbcff'
export const sdkArchiveBytes = 230_753_980
const pythonHelper = fileURLToPath(new URL('./materialize-sdk-version-metadata.py', import.meta.url))

export function materializeSdkVersion({ dotnetRoot, archivePath, archiveSha512 = sdkArchiveSha512 }) {
  const result = spawnSync('python3', ['-I', pythonHelper, dotnetRoot, archivePath, archiveSha512], {
    encoding: 'utf8', timeout: 190_000, maxBuffer: 16 * 1024,
  })
  if (result.error || result.status !== 0) {
    throw new Error(`verified .NET SDK materialization failed: ${result.error?.message ?? result.stderr.trim()}`)
  }
  return result.stdout.trim()
}

function assertPinnedCandidate() {
  const candidate = process.cwd()
  const sdk = JSON.parse(readFileSync(join(candidate, 'global.json'), 'utf8')).sdk.version
  const mise = readFileSync(join(candidate, '.mise.toml'), 'utf8')
  if (sdk !== sdkVersion || !new RegExp(`^dotnet\\s*=\\s*"${sdkVersion.replaceAll('.', '\\.')}"$`, 'mu').test(mise)) {
    throw new Error('candidate .NET SDK pins do not match the verified archive')
  }
  if (process.platform !== 'darwin' || process.arch !== 'arm64') {
    throw new Error('verified SDK archive is for macOS ARM64 only')
  }
}

function checkedJobRoot(dotnetRoot) {
  const dataDir = process.env.MISE_DATA_DIR
  const runnerTemp = process.env.RUNNER_TEMP
  if (!dataDir || !runnerTemp) throw new Error('job-scoped mise and runner directories are required')
  const data = realpathSync(dataDir)
  const temp = realpathSync(runnerTemp)
  const root = realpathSync(dotnetRoot)
  if (!data.startsWith(`${temp}${sep}`) || !root.startsWith(`${data}${sep}`) ||
      !lstatSync(root).isDirectory()) {
    throw new Error('dotnet root is outside the job-scoped mise installation')
  }
  return { root, temp }
}

function downloadOfficialArchive(destination) {
  const result = spawnSync('curl', [
    '--fail', '--location', '--silent', '--show-error', '--retry', '2',
    '--connect-timeout', '10', '--max-time', '180', '--max-filesize', '300000000',
    '--output', destination, sdkArchiveUrl,
  ], { encoding: 'utf8', timeout: 190_000, maxBuffer: 16 * 1024 })
  if (result.error || result.status !== 0 || statSync(destination).size !== sdkArchiveBytes) {
    throw new Error(`official .NET SDK archive download failed or size changed: ${result.error?.message ?? result.stderr.trim()}`)
  }
}

function main() {
  if (process.argv.length !== 3) throw new Error('usage: materialize-sdk-version-metadata.mjs DOTNET_ROOT')
  assertPinnedCandidate()
  const { root, temp } = checkedJobRoot(process.argv[2])
  const scratch = mkdtempSync(join(temp, 'verified-dotnet-sdk-'))
  try {
    const archive = join(scratch, 'sdk.tar.gz')
    downloadOfficialArchive(archive)
    process.stdout.write(`${materializeSdkVersion({ dotnetRoot: root, archivePath: archive })}\n`)
  } finally {
    rmSync(scratch, { recursive: true, force: true })
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  try { main() } catch (error) { process.stderr.write(`${error.message}\n`); process.exitCode = 1 }
}
