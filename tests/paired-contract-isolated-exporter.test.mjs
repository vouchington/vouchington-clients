import assert from 'node:assert/strict'
import { spawn, spawnSync } from 'node:child_process'
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { once } from 'node:events'
import test from 'node:test'
import { pairedExporterOutputPaths } from '../scripts/paired-exporter-runtime-evidence.mjs'
import { runIsolatedNativeExporter } from '../scripts/run-isolated-native-exporter.mjs'
import {
  assertRequiredExporterInputs,
  shouldSkipExporterIntegration,
} from './paired-contract-isolated-exporter-test-policy.mjs'

const runner = fileURLToPath(
  new URL('../scripts/run-isolated-native-exporter.mjs', import.meta.url),
)
const dockerAvailable =
  spawnSync('docker', ['info', '--format', '{{.ServerVersion}}'], {
    encoding: 'utf8',
    timeout: 5_000,
  }).status === 0
const packageBundleRoot = process.env.PAIRED_EXPORTER_PACKAGE_BUNDLE_ROOT
const required = process.env.PAIRED_ISOLATION_TEST_REQUIRED === 'true'

test('keeps the Docker integration opt-in and required mode fail-closed', async () => {
  assert.equal(shouldSkipExporterIntegration({ required: false }), true)
  assert.equal(shouldSkipExporterIntegration({ required: true }), false)
  assert.throws(
    () =>
      assertRequiredExporterInputs({
        required: true,
        dockerAvailable: false,
        bundleRoot: packageBundleRoot,
      }),
    /needs Docker/u,
  )
  assert.throws(
    () =>
      assertRequiredExporterInputs({
        required: true,
        dockerAvailable: true,
        bundleRoot: undefined,
      }),
    /pinned bundle/u,
  )
})

test(
  'runs candidate exporter without runner environment, network, host files, process visibility, or source writes',
  { skip: shouldSkipExporterIntegration({ required }) },
  async t => {
    assertRequiredExporterInputs({ required, dockerAvailable, bundleRoot: packageBundleRoot })
    assert.ok(
      dockerAvailable,
      'the required isolated exporter test needs an available Docker daemon',
    )
    const parent = await mkdtemp(join(tmpdir(), 'paired-export-isolation-'))
    const producerRoot = join(parent, 'producer')
    const consumerRoot = join(parent, 'consumer')
    const outputRoot = join(parent, 'export')
    const symlinkOutputRoot = join(parent, 'symlink-export')
    const runtimeEvidencePath = join(parent, 'runtime-evidence.json')
    const symlinkRuntimeEvidencePath = join(parent, 'symlink-runtime-evidence.json')
    const hostOnlyFile = join(parent, 'host-only-secret')
    assert.ok(
      packageBundleRoot,
      'the isolation test requires the exact SRI-verified package bundle',
    )
    const nonce = `host-process-${process.pid}-${Date.now()}`
    const hostSentinel = spawn(
      process.execPath,
      ['-e', `setInterval(() => {}, 1000); // ${nonce}`],
      {
        stdio: 'ignore',
      },
    )
    await once(hostSentinel, 'spawn')
    t.after(async () => {
      hostSentinel.kill('SIGTERM')
      await rm(parent, { recursive: true, force: true })
    })
    await Promise.all([
      mkdir(join(producerRoot, 'dev'), { recursive: true }),
      mkdir(consumerRoot),
      mkdir(outputRoot),
      mkdir(symlinkOutputRoot),
      writeFile(hostOnlyFile, 'host-only-secret-value\n'),
    ])
    const exporter = `
      import assert from 'node:assert/strict';
      import { access, mkdir, readFile, writeFile } from 'node:fs/promises';
      import { dirname, join } from 'node:path';
      const secret = 'runner-secret-test-value';
      assert.ok(secret);
      assert.ok(!Object.values(process.env).includes(secret), 'runner environment leaked');
      assert.ok(!process.env.GITHUB_TOKEN, 'GitHub token leaked');
      assert.ok(!process.env.ACTIONS_RUNTIME_TOKEN, 'Actions runtime token leaked');
      await assert.rejects(access('/var/run/docker.sock'));
      await assert.rejects(readFile(${JSON.stringify(hostOnlyFile)}));
      let hostProcessVisible = false;
      try {
        hostProcessVisible = (await readFile('/proc/${hostSentinel.pid}/cmdline')).includes(${JSON.stringify(nonce)});
      } catch {}
      assert.equal(hostProcessVisible, false, 'host process is visible');
      await assert.rejects(fetch('https://example.com', { signal: AbortSignal.timeout(1200) }));
      await assert.rejects(writeFile('/producer/.container-write-probe', 'nope'));
      const output = process.argv[process.argv.indexOf('--output-root') + 1];
      const paths = ${JSON.stringify(pairedExporterOutputPaths)};
      for (const path of paths) {
        const target = join(output, ...path.split('/'));
        await mkdir(dirname(target), { recursive: true });
        await writeFile(target, path + '\\n');
      }
      // --output-root --consumer-root
    `
    await writeFile(join(producerRoot, 'dev/native-localization.mts'), exporter)
    const result = spawnSync(
      process.execPath,
      [
        runner,
        '--producer-root',
        producerRoot,
        '--consumer-root',
        consumerRoot,
        '--output-root',
        outputRoot,
        '--package-bundle-root',
        packageBundleRoot,
        '--runtime-evidence-path',
        runtimeEvidencePath,
      ],
      {
        encoding: 'utf8',
        timeout: 30_000,
        env: {
          PATH: process.env.PATH ?? '/usr/bin:/bin',
          PAIRED_ISOLATION_TEST_SENTINEL: 'runner-secret-test-value',
          GITHUB_TOKEN: 'runner-github-token-sentinel',
          ACTIONS_RUNTIME_TOKEN: 'runner-actions-runtime-sentinel',
        },
      },
    )
    assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`)
    assert.equal(
      await readFile(join(outputRoot, pairedExporterOutputPaths.at(-1)), 'utf8'),
      `${pairedExporterOutputPaths.at(-1)}\n`,
    )
    assert.equal(await readFile(hostOnlyFile, 'utf8'), 'host-only-secret-value\n')
    const runtimeEvidence = JSON.parse(await readFile(runtimeEvidencePath, 'utf8'))
    assert.equal(runtimeEvidence.image.nodeVersion, 'v26.12.0')
    assert.equal(runtimeEvidence.image.gitVersion, 'git version 2.39.5')
    assert.equal(runtimeEvidence.packages.length, 10)

    await writeFile(
      join(producerRoot, 'dev/native-localization.mts'),
      `
        import { mkdir, symlink, writeFile } from 'node:fs/promises';
        import { dirname, join } from 'node:path';
        const output = process.argv[process.argv.indexOf('--output-root') + 1];
        const paths = ${JSON.stringify(pairedExporterOutputPaths)};
        for (const [index, path] of paths.entries()) {
          const target = join(output, ...path.split('/'));
          await mkdir(dirname(target), { recursive: true });
          if (index === 0) await symlink('/etc/passwd', target);
          else await writeFile(target, 'fixture');
        }
      `,
    )
    const symlinkResult = spawnSync(
      process.execPath,
      [
        runner,
        '--producer-root',
        producerRoot,
        '--consumer-root',
        consumerRoot,
        '--output-root',
        symlinkOutputRoot,
        '--package-bundle-root',
        packageBundleRoot,
        '--runtime-evidence-path',
        symlinkRuntimeEvidencePath,
      ],
      {
        encoding: 'utf8',
        timeout: 30_000,
        env: { PATH: process.env.PATH ?? '/usr/bin:/bin' },
      },
    )
    assert.notEqual(symlinkResult.status, 0, 'candidate symlink output was accepted')
    assert.match(symlinkResult.stderr, /symbolic link/u)

    const timeoutOutputRoot = join(parent, 'timeout-export')
    await mkdir(timeoutOutputRoot)
    await writeFile(join(producerRoot, 'dev/native-localization.mts'), 'while (true) {}\n')
    await assert.rejects(
      runIsolatedNativeExporter({
        producerRoot,
        consumerRoot,
        outputRoot: timeoutOutputRoot,
        packageBundleRoot,
        executionTimeoutMs: 15_000,
      }),
      /timed out/u,
    )
    const leftoverContainers = spawnSync(
      'docker',
      ['ps', '-aq', '--filter', `name=paired-native-export-${process.pid}`],
      { encoding: 'utf8', timeout: 10_000 },
    )
    assert.equal(leftoverContainers.status, 0, leftoverContainers.stderr)
    assert.equal(leftoverContainers.stdout.trim(), '', 'timed-out candidate container remains')
  },
)
