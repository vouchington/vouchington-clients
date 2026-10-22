import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import {
  existsSync,
  lstatSync,
  mkdirSync,
  mkdtempSync,
  readlinkSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  packState,
  restoreState,
} from '../swift-clients/apps/android/tooling/android-state-handoff.mjs'

const swift = spawnSync('swift', ['--version'], { encoding: 'utf8' })

test(
  'same-path SwiftPM build stays incremental after an exact-state round trip',
  {
    skip: process.platform !== 'darwin' || swift.status !== 0,
  },
  async t => {
    const root = mkdtempSync(join(tmpdir(), 'android-state-swiftpm-'))
    t.after(() => rmSync(root, { recursive: true, force: true }))
    const checkoutRoot = join(root, 'candidate-clients')
    const packageRoot = join(checkoutRoot, 'swift-clients/apps/android')
    const cacheRoot = join(root, 'gradle-build-cache')
    const artifactRoot = join(root, 'artifact')
    const scratch = join(packageRoot, '.build')
    mkdirSync(join(packageRoot, 'Sources/CodecProbe'), { recursive: true })
    mkdirSync(join(packageRoot, 'Android/.gradle'), { recursive: true })
    mkdirSync(cacheRoot)
    writeFileSync(
      join(packageRoot, 'Package.swift'),
      `// swift-tools-version: 5.9\nimport PackageDescription\nlet package = Package(name: "CodecProbe", products: [.executable(name: "CodecProbe", targets: ["CodecProbe"])], targets: [.executableTarget(name: "CodecProbe")])\n`,
    )
    writeFileSync(
      join(packageRoot, 'Sources/CodecProbe/main.swift'),
      'print((0..<1000).reduce(0, +))\n',
    )
    const build = () => {
      const result = spawnSync(
        'swift',
        [
          'build',
          '--disable-sandbox',
          '--package-path',
          packageRoot,
          '--scratch-path',
          scratch,
          '--verbose',
        ],
        { encoding: 'utf8', maxBuffer: 8 * 1024 * 1024, timeout: 120_000 },
      )
      assert.equal(result.status, 0, result.stderr)
      return `${result.stdout}\n${result.stderr}`
    }
    assert.match(build(), /Build complete!/u)
    const repeat = build()
    assert.doesNotMatch(repeat, /builtin-SwiftDriver --/u)
    const executable = [
      join(scratch, 'out/Products/Debug/CodecProbe'),
      join(scratch, 'debug/CodecProbe'),
    ].find(existsSync)
    assert.ok(executable, 'SwiftPM did not produce the probe executable')
    const executableTime = statSync(executable, { bigint: true }).mtimeNs

    // Xcode-backed SwiftPM uses a convenience alias not present in the pinned
    // Android SwiftPM layout. It is irrelevant to the incremental object graph.
    const debug = join(scratch, 'debug')
    try {
      if (lstatSync(debug).isSymbolicLink() && readlinkSync(debug) === 'out/Products/Debug') {
        rmSync(debug)
      }
    } catch (error) {
      if (error.code !== 'ENOENT') throw error
    }
    const identity = { revision: 'a'.repeat(40), run: '1', attempt: '1', gradle: '9.4.1' }
    await packState({ packageRoot, checkoutRoot, cacheRoot, artifactRoot, identity })
    rmSync(scratch, { recursive: true })
    rmSync(join(packageRoot, 'Android/.gradle'), { recursive: true })
    rmSync(cacheRoot, { recursive: true })
    await restoreState({ packageRoot, checkoutRoot, cacheRoot, artifactRoot, identity })
    const restored = build()
    assert.doesNotMatch(restored, /builtin-SwiftDriver --/u)
    assert.equal(statSync(executable, { bigint: true }).mtimeNs, executableTime)
  },
)
