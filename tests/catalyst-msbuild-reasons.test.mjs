import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtemp, mkdir, rm, utimes, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'

const helper = new URL('../dotnet-clients/tooling/catalyst-msbuild-reasons.py', import.meta.url)
  .pathname

const fakeBuild = String.raw`
import re, sys
fifo = next(re.match(r'-flp:LogFile=([^;]+);Verbosity=diagnostic', arg).group(1)
            for arg in sys.argv if arg.startswith('-flp:'))
project = sys.argv[1]
with open(fifo, 'w') as stream:
    stream.write(f'Target "CoreCompile: (TargetId:108)" in file "sdk.targets" from project "{project}" (target "Compile" depends on it):\n')
    stream.write('Building target "CoreCompile" completely.\n')
    stream.write('Input file "input.cs" is newer than output file "obj/output.dll".\n')
    stream.write('x' * 20000 + '\n')
    stream.write('Input file "wrong.cs" is newer than output file "obj/output.dll".\n')
    stream.write(f'Target "XamlC: (TargetId:109)" in file "maui.targets" from project "{project}" (target "Build" depends on it):\n')
    stream.write('Building target "XamlC" completely.\n')
    stream.write('Output file "obj/XamlC.stamp" does not exist.\n')
print('Original build output')
sys.exit(int(sys.argv[2]))
`

it('streams bounded target reasons with precompile mtimes and preserves the build exit code', async () => {
  const root = await mkdtemp(join(tmpdir(), 'catalyst-msbuild-reasons-'))
  try {
    await mkdir(join(root, 'obj'))
    await writeFile(join(root, 'Probe.csproj'), '<Project/>')
    await writeFile(join(root, 'input.cs'), 'newer')
    await writeFile(join(root, 'obj/output.dll'), 'older')
    await utimes(join(root, 'obj/output.dll'), 1_700_000_000, 1_700_000_000)
    await utimes(join(root, 'input.cs'), 1_700_000_001, 1_700_000_001)
    const run = spawnSync(
      'python3',
      [helper, '--root', root, '--', 'python3', '-c', fakeBuild, join(root, 'Probe.csproj'), '17'],
      {
        cwd: root,
        encoding: 'utf8',
        env: { ...process.env, RUNNER_TEMP: root, PYTHONDONTWRITEBYTECODE: '1' },
        timeout: 10_000,
      },
    )
    assert.equal(run.status, 17, run.stderr)
    assert.match(run.stdout, /Original build output/u)
    const records = run.stdout
      .split('\n')
      .filter(line => line.startsWith('{'))
      .map(JSON.parse)
    assert.equal(records.length, 2, run.stdout)
    assert.deepEqual(
      records.map(record => record.target),
      ['CoreCompile', 'XamlC'],
    )
    assert.equal(records[0].project, 'repo/Probe.csproj')
    assert.equal(records[0].input, 'repo/input.cs')
    assert.equal(records[0].output, 'repo/obj/output.dll')
    assert.equal(records[0].input_newer_ns, 1_000_000_000)
    assert.equal(records[1].reason, 'missing-output')
    assert.equal(records[1].output_stat, null)
    assert.doesNotMatch(run.stdout, /wrong\.cs/u)
    assert.doesNotMatch(run.stdout, /x{100}/u)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

it('caps diagnostic bytes, redacts unknown paths, and keeps draining after parser errors', async () => {
  const root = await mkdtemp(join(tmpdir(), 'catalyst-msbuild-limits-'))
  try {
    const exercise = String.raw`
import importlib.util, sys
spec = importlib.util.spec_from_file_location('reasons', sys.argv[1])
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.MAX_BYTES = 32
parser = module.ReasonParser(module.Path(sys.argv[2]), module.Path('/nonexistent-sdk'))
parser.feed(b'x' * 64)
assert parser.truncated and not parser.records
_, label, known = module.classify('/secret/credential.txt', sys.argv[2], module.Path(sys.argv[2]), module.Path('/nonexistent-sdk'))
assert label.startswith('other/sha256:') and not known and 'credential' not in label
def fail(self, data):
    raise ValueError('parser failure')
module.ReasonParser.feed = fail
raise SystemExit(module.run(module.Path(sys.argv[2]), ['python3', '-c', sys.argv[3], sys.argv[2] + '/Probe.csproj', '0']))
`
    const run = spawnSync('python3', ['-c', exercise, helper, root, fakeBuild], {
      cwd: root,
      encoding: 'utf8',
      env: { ...process.env, RUNNER_TEMP: root, PYTHONDONTWRITEBYTECODE: '1' },
      timeout: 10_000,
    })
    assert.equal(run.status, 0, run.stderr)
    assert.match(run.stdout, /Original build output/u)
    assert.match(run.stderr, /MSBuild diagnostic reader error: ValueError/u)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})
