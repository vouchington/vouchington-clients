import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { copyFile, mkdtemp, mkdir, readFile, rm, stat, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'

const script = new URL('../dotnet-clients/tooling/catalyst-compile-state.sh', import.meta.url)
const sourceHelper = new URL(
  '../dotnet-clients/tooling/catalyst-source-snapshot.py',
  import.meta.url,
)

const run = (command, args, cwd) => spawnSync(command, args, { cwd, encoding: 'utf8' })

const invoke = (mode, artifacts, state, candidate, revision, attempt = '1') =>
  run(
    'bash',
    [
      script.pathname,
      mode,
      artifacts,
      state,
      candidate,
      revision,
      '39700146375',
      attempt,
      'maccatalyst-arm64',
      '10.0.401',
      '10.0.401',
      'Xcode 26.6',
    ],
    candidate,
  )

it('rehydrates only matching exact-run managed outputs and byte-identical source metadata', async () => {
  const temp = await mkdtemp(join(tmpdir(), 'catalyst-compile-state-'))
  const candidate = join(temp, 'candidate')
  const consumer = join(temp, 'consumer')
  const artifacts = join(temp, 'artifacts')
  const restored = join(temp, 'restored')
  const state = join(temp, 'state')
  try {
    const app = join(artifacts, 'obj', 'Voucha.Client.App')
    const core = join(artifacts, 'obj', 'Voucha.Client.Core')
    const appBin = join(artifacts, 'bin', 'Voucha.Client.App')
    const coreBin = join(artifacts, 'bin', 'Voucha.Client.Core')
    await Promise.all([
      mkdir(join(app, 'release_net10.0-maccatalyst_maccatalyst-arm64'), { recursive: true }),
      mkdir(core, { recursive: true }),
      mkdir(appBin, { recursive: true }),
      mkdir(coreBin, { recursive: true }),
      mkdir(join(candidate, 'dotnet-clients', 'tooling'), { recursive: true }),
    ])
    await Promise.all([
      writeFile(join(app, 'project.assets.json'), '{}'),
      writeFile(join(core, 'project.assets.json'), '{}'),
      writeFile(
        join(app, 'release_net10.0-maccatalyst_maccatalyst-arm64', 'Voucha.Client.App.dll'),
        'app',
      ),
      writeFile(join(coreBin, 'Voucha.Client.Core.dll'), 'core'),
      writeFile(join(appBin, 'not-managed-compile-state.txt'), 'copied by CoreBuild'),
      writeFile(join(candidate, '.editorconfig'), 'root = true\n'),
      writeFile(join(candidate, 'global.json'), '{}'),
      writeFile(join(candidate, 'dotnet-clients', 'input.cs'), 'source'),
      copyFile(
        sourceHelper,
        join(candidate, 'dotnet-clients', 'tooling', 'catalyst-source-snapshot.py'),
      ),
    ])
    assert.equal(run('git', ['init', '-q'], candidate).status, 0)
    assert.equal(run('git', ['add', '-A'], candidate).status, 0)
    assert.equal(
      run(
        'git',
        [
          '-c',
          'user.name=Test',
          '-c',
          'user.email=test@example.invalid',
          'commit',
          '-qm',
          'fixture',
        ],
        candidate,
      ).status,
      0,
    )
    const revision = run('git', ['rev-parse', 'HEAD'], candidate).stdout.trim()
    assert.equal(invoke('pack', artifacts, state, candidate, revision).status, 0)
    const packedPaths = run('tar', ['-tzf', join(state, 'compile-state.tar.gz')], temp).stdout
    assert.match(packedPaths, /bin\/Voucha\.Client\.Core\/Voucha\.Client\.Core\.dll/u)
    assert.doesNotMatch(packedPaths, /bin\/Voucha\.Client\.App/u)
    assert.equal(run('git', ['clone', '-q', candidate, consumer], temp).status, 0)
    const producerInput = join(candidate, 'dotnet-clients', 'input.cs')
    const consumerInput = join(consumer, 'dotnet-clients', 'input.cs')
    const producerMtime = (await stat(producerInput)).mtimeMs

    const alterArchive = `
import io, sys, tarfile
source, destination, kind = sys.argv[1:]
with tarfile.open(source, 'r:gz') as archive:
    entries = [(member, archive.extractfile(member).read()) for member in archive]
if kind == 'missing':
    entries.pop()
elif kind == 'duplicate':
    entries.append(entries[0])
elif kind == 'link':
    entries[-1][0].type = tarfile.SYMTYPE
    entries[-1][0].linkname = '../../outside'
    entries[-1] = (entries[-1][0], b'')
else:
    member = tarfile.TarInfo('../escape' if kind == 'escape' else 'dotnet-clients/extra.cs')
    entries.append((member, b'x'))
with tarfile.open(destination, 'w:gz', format=tarfile.PAX_FORMAT) as archive:
    for member, data in entries:
        member.size = len(data)
        archive.addfile(member, io.BytesIO(data))
`
    for (const kind of ['missing', 'extra', 'escape', 'duplicate', 'link']) {
      const altered = join(temp, `${kind}.tar.gz`)
      assert.equal(
        run(
          'python3',
          ['-c', alterArchive, join(state, 'source-state.tar.gz'), altered, kind],
          temp,
        ).status,
        0,
      )
      assert.notEqual(
        run('python3', [sourceHelper.pathname, 'overlay', consumer, altered, revision], consumer)
          .status,
        0,
      )
      assert.equal(await readFile(join(consumer, 'dotnet-clients', 'input.cs'), 'utf8'), 'source')
    }
    const linkedConsumer = join(temp, 'linked-consumer')
    assert.equal(run('git', ['clone', '-q', candidate, linkedConsumer], temp).status, 0)
    const linkedInput = join(linkedConsumer, 'dotnet-clients', 'input.cs')
    await rm(linkedInput)
    await symlink(join(candidate, 'dotnet-clients', 'input.cs'), linkedInput)
    assert.notEqual(
      run(
        'python3',
        [
          sourceHelper.pathname,
          'overlay',
          linkedConsumer,
          join(state, 'source-state.tar.gz'),
          revision,
        ],
        linkedConsumer,
      ).status,
      0,
    )

    assert.notEqual(invoke('unpack', restored, state, consumer, revision, '2').status, 0)
    assert.equal(invoke('unpack', restored, state, consumer, revision).status, 0)
    assert.equal(
      await readFile(
        join(
          restored,
          'obj',
          'Voucha.Client.App',
          'release_net10.0-maccatalyst_maccatalyst-arm64',
          'Voucha.Client.App.dll',
        ),
        'utf8',
      ),
      'app',
    )
    assert.equal(await readFile(consumerInput, 'utf8'), await readFile(producerInput, 'utf8'))
    assert.ok(Math.abs((await stat(consumerInput)).mtimeMs - producerMtime) < 1000)
    assert.equal(
      run(
        'git',
        ['diff', '--exit-code', 'HEAD', '--', 'dotnet-clients', '.editorconfig', 'global.json'],
        consumer,
      ).status,
      0,
    )

    const buildArchive = join(state, 'compile-state.tar.gz')
    const buildManifest = join(state, 'compile-state.manifest')
    const originalArchive = await readFile(buildArchive)
    const originalManifest = await readFile(buildManifest, 'utf8')
    const alterBuildArchive = `
import io, sys, tarfile
source, destination, kind = sys.argv[1:]
with tarfile.open(source, 'r:gz') as archive:
    entries = [(member, archive.extractfile(member).read() if member.isfile() else None) for member in archive]
if kind in ('dot', 'double', 'parent', 'out-of-scope'):
    name = {'dot': 'obj/./alias', 'double': 'obj//alias', 'parent': 'obj/../alias',
            'out-of-scope': 'bin/Voucha.Client.App/rogue'}[kind]
    entries.append((tarfile.TarInfo(name), b'alias'))
else:
    parent = (tarfile.TarInfo('obj/collision'), b'parent')
    child = (tarfile.TarInfo('obj/collision/child'), b'child')
    entries.extend((parent, child) if kind == 'file-first' else (child, parent))
with tarfile.open(destination, 'w:gz', format=tarfile.PAX_FORMAT) as archive:
    for member, data in entries:
        if data is not None:
            member.size = len(data)
        archive.addfile(member, io.BytesIO(data) if data is not None else None)
`
    for (const kind of ['dot', 'double', 'parent', 'out-of-scope', 'file-first', 'child-first']) {
      const altered = join(temp, `${kind}-build.tar.gz`)
      await writeFile(buildArchive, originalArchive)
      assert.equal(
        run('python3', ['-c', alterBuildArchive, buildArchive, altered, kind], temp).status,
        0,
      )
      const bytes = await readFile(altered)
      await writeFile(buildArchive, bytes)
      const sha = createHash('sha256').update(bytes).digest('hex')
      await writeFile(buildManifest, originalManifest.replace(/^sha256=.*$/mu, `sha256=${sha}`))
      const rejected = invoke('unpack', join(temp, `${kind}-restore`), state, consumer, revision)
      assert.notEqual(rejected.status, 0, `${kind}: ${rejected.stderr}`)
      assert.match(
        rejected.stderr,
        /managed archive (path is not canonical|file is an ancestor|contains output outside)/u,
      )
      assert.equal(await readFile(consumerInput, 'utf8'), 'source')
    }
    await writeFile(buildArchive, originalArchive)
    await writeFile(buildManifest, originalManifest)

    await writeFile(consumerInput, 'different')
    assert.notEqual(
      invoke('unpack', join(temp, 'wrong-source'), state, consumer, revision).status,
      0,
    )
    assert.equal(await readFile(consumerInput, 'utf8'), 'different')

    await writeFile(join(state, 'compile-state.tar.gz'), 'corrupted')
    assert.notEqual(
      invoke('unpack', join(temp, 'corrupt-output'), state, candidate, revision).status,
      0,
    )
  } finally {
    await rm(temp, { recursive: true, force: true })
  }
})
