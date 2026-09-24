import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { lstat, readFile } from 'node:fs/promises'
import { basename, join, resolve } from 'node:path'
import { describe, it } from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
const actionSetupStep = /^\s*(?:- )?uses: pnpm\/action-setup@/mu
const setupNodeStep = /^\s*(?:- )?uses: actions\/setup-node@/mu
const pnpmCommand = /\bpnpm (?:install|run|exec|dlx)\b/u
const latestOfMajor = /^latest-(\d+)$/u

function trackedFiles() {
  return execFileSync('git', ['ls-files', '-z'], { cwd: repositoryRoot, encoding: 'utf8' })
    .split('\0')
    .filter(Boolean)
}

async function readTracked(filter) {
  const files = await Promise.all(
    trackedFiles()
      .filter(filter)
      .sort()
      .map(async path => {
        const fullPath = join(repositoryRoot, path)
        // Symlinks point at tracked files that are read on their own.
        if (!(await lstat(fullPath)).isFile()) return []
        return [[path, await readFile(fullPath, 'utf8')]]
      }),
  )
  return files.flat()
}

const readActionsYaml = () =>
  readTracked(path => path.startsWith('.github/') && /\.ya?ml$/u.test(path))

function stepLists(source) {
  const lines = source.split('\n')
  return lines.flatMap((line, index) => {
    const header = line.match(/^(\s*)steps:\s*$/u)
    if (!header) return []
    const steps = []
    for (const next of lines.slice(index + 1)) {
      if (next.trim() === '') continue
      const indent = next.search(/\S/u)
      if (indent <= header[1].length) break
      if (indent === header[1].length + 2 && next.trimStart().startsWith('- ')) steps.push([next])
      else steps.at(-1)?.push(next)
    }
    return [steps.map(step => step.join('\n'))]
  })
}

function withInputs(step) {
  const lines = step.split('\n')
  const start = lines.findIndex(line => /^\s*with:\s*$/u.test(line))
  assert.notEqual(start, -1, `pnpm/action-setup needs a block \`with:\`:\n${step}`)
  const indent = lines[start].search(/\S/u)
  const inputs = {}
  for (const line of lines.slice(start + 1)) {
    if (line.search(/\S/u) <= indent) break
    const input = line.match(/^\s*([\w-]+):\s*(.*)$/u)
    assert.ok(input, `unparseable pnpm/action-setup input: ${line}`)
    inputs[input[1]] = input[2]
  }
  return inputs
}

async function pnpmSetupSteps() {
  const steps = []
  for (const [path, source] of await readActionsYaml()) {
    for (const list of stepLists(source)) {
      for (const step of list.filter(item => actionSetupStep.test(item))) steps.push([path, step])
    }
  }
  assert.ok(steps.length > 0, 'expected at least one pnpm/action-setup step')
  return steps
}

async function compositesWithPnpmSetup() {
  const composites = await readTracked(path =>
    /^\.github\/actions\/[^/]+\/action\.ya?ml$/u.test(path),
  )
  return new Set(
    composites
      .filter(([, source]) => actionSetupStep.test(source))
      .map(([path]) => `./${path.split('/').slice(0, 3).join('/')}`),
  )
}

describe('pnpm 12 without a pinned version', () => {
  it('passes pnpm/action-setup only the latest release of one major', async () => {
    for (const [path, step] of await pnpmSetupSteps()) {
      assert.match(
        step,
        /uses: pnpm\/action-setup@[0-9a-f]{40} # v\d+\.\d+\.\d+$/mu,
        `${path} must pin pnpm/action-setup to a commit with its release tag`,
      )
      const inputs = withInputs(step)
      assert.deepEqual(Object.keys(inputs), ['version'], `${path} passes extra action inputs`)
      // A bare major would keep the action's bundled pnpm; `latest-<major>` self-updates.
      assert.match(inputs.version, latestOfMajor, `${path} must pass \`latest-<major>\``)
    }
  })

  it('uses the same action release and pnpm major at every call site', async () => {
    const steps = await pnpmSetupSteps()
    const refs = new Set(steps.map(([, step]) => step.match(/pnpm\/action-setup@[0-9a-f]{40}/u)[0]))
    const majors = new Set(
      steps.map(([, step]) => withInputs(step).version.match(latestOfMajor)?.[1]),
    )
    assert.equal(refs.size, 1, `pnpm/action-setup refs diverge: ${[...refs].join(', ')}`)
    assert.equal(majors.size, 1, `pnpm majors diverge: ${[...majors].join(', ')}`)
  })

  it('sets up Node before pnpm and pnpm before any job runs it', async () => {
    const composites = await compositesWithPnpmSetup()
    const setsUpPnpm = step =>
      actionSetupStep.test(step) ||
      [...composites].some(composite => step.includes(`uses: ${composite}\n`))

    for (const [path, source] of await readActionsYaml()) {
      for (const steps of stepLists(source)) {
        assert.ok(steps.length > 0, `${path} has an unparsed steps list`)
        steps.forEach((step, index) => {
          const earlier = steps.slice(0, index)
          if (actionSetupStep.test(step)) {
            assert.ok(
              earlier.some(item => setupNodeStep.test(item)),
              `${path} sets up pnpm before Node:\n${step}`,
            )
          } else if (pnpmCommand.test(step)) {
            assert.ok(earlier.some(setsUpPnpm), `${path} runs pnpm before setting it up:\n${step}`)
          }
        })
      }
    }
  })

  it('declares no package-manager pin in any package.json', async () => {
    for (const [path, source] of await readTracked(path => basename(path) === 'package.json')) {
      const manifest = JSON.parse(source)
      assert.equal(manifest.packageManager, undefined, `${path} pins packageManager`)
      assert.equal(manifest.devEngines?.packageManager, undefined, `${path} pins devEngines`)
      assert.equal(manifest.engines?.pnpm, undefined, `${path} pins engines.pnpm`)
    }
  })

  it('never installs or runs an exact pnpm release', async () => {
    for (const [path, source] of await readTracked(() => true)) {
      assert.doesNotMatch(source, /\bpnpm@\d/u, `${path} names an exact pnpm release`)
    }
  })

  it('never bootstraps pnpm through Corepack', async () => {
    const executable = path => !path.startsWith('tests/') && !path.endsWith('.md')
    for (const [path, source] of await readTracked(executable)) {
      assert.doesNotMatch(source, /\bcorepack\b/iu, `${path} uses Corepack`)
    }
  })
})
