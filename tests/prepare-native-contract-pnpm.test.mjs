import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'
import { jobBlock } from './workflow-job-block.mjs'

const cachePin = '55cc8345863c7cc4c66a329aec7e433d2d1c52a9'
const mainMiss =
  "github.event_name == 'push' && github.ref == 'refs/heads/main' && steps.STEP.outputs.cache-hit != 'true'"

const read = relativePath => readFile(new URL(`../${relativePath}`, import.meta.url), 'utf8')

const step = (source, name) => {
  const marker = `- name: ${name}\n`
  const start = source.indexOf(marker)
  assert.ok(start >= 0, name)
  const rest = source.slice(start + marker.length)
  const next = rest.search(/\n\s+- (?:name:|uses:)/u)
  return next === -1 ? source.slice(start) : source.slice(start, start + marker.length + next)
}

const savesOn = (eventName, ref, cacheHit) =>
  eventName === 'push' && ref === 'refs/heads/main' && cacheHit !== 'true'

test('native candidate setup provisions pnpm for the harness', async () => {
  const action = await read('.github/actions/prepare-native-contract/action.yml')
  const setupIndex = action.indexOf('name: Set up pnpm')
  const installIndex = action.indexOf('name: Install candidate tooling')

  assert.ok(setupIndex >= 0 && setupIndex < installIndex)
  assert.match(
    action.slice(setupIndex, installIndex),
    /uses: pnpm\/action-setup@[0-9a-f]{40}[\s\S]*?version: [^\s]+/u,
  )
  assert.match(action.slice(installIndex), /run: pnpm install --frozen-lockfile/u)
})

test('shared pnpm stores restore always and save only a trusted main miss', async () => {
  const [action, workflow] = await Promise.all([
    read('.github/actions/prepare-native-contract/action.yml'),
    read('.github/workflows/native-contract-tests.yml'),
  ])
  const stores = [
    {
      source: action,
      restore: 'Restore candidate pnpm store',
      save: 'Save candidate pnpm store from main',
      stepId: 'pnpm-store',
      key: "${{ runner.os }}-pnpm-clients-${{ hashFiles('candidate-clients/pnpm-lock.yaml') }}",
      restoreKey: '${{ runner.os }}-pnpm-clients-',
    },
    {
      source: jobBlock(workflow, 'produce'),
      restore: 'Restore Vouchington pnpm store',
      save: 'Save Vouchington pnpm store from main',
      stepId: 'vouchington-pnpm-store',
      key: "${{ runner.os }}-pnpm-vouchington-${{ hashFiles('filaments/pnpm-lock.yaml') }}",
      restoreKey: '${{ runner.os }}-pnpm-vouchington-',
    },
  ]

  for (const store of stores) {
    const restore = step(store.source, store.restore)
    const save = step(store.source, store.save)
    const installAt = store.source.indexOf('pnpm install --frozen-lockfile')
    const saveAt = store.source.indexOf(`name: ${store.save}`)
    const expectedIf =
      store.stepId === 'pnpm-store'
        ? `\${{ inputs.skip-candidate-install != 'true' && ${mainMiss.replaceAll('STEP', store.stepId)} }}`
        : `\${{ ${mainMiss.replaceAll('STEP', store.stepId)} }}`

    assert.match(
      restore,
      new RegExp(`id: ${store.stepId}\\n\\s+uses: actions/cache/restore@${cachePin}`, 'u'),
    )
    if (store.stepId === 'pnpm-store') {
      assert.match(restore, /if: \$\{\{ inputs\.skip-candidate-install != 'true' \}\}/u)
    } else {
      assert.doesNotMatch(restore, /\n\s+if:/u)
    }
    assert.match(restore, new RegExp(`path: \\$\\{\\{ runner\\.temp \\}\\}/pnpm-store`, 'u'))
    assert.match(
      restore,
      new RegExp(`key: ${store.key.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&')}`, 'u'),
    )
    assert.match(
      restore,
      new RegExp(
        `restore-keys:[\\s\\S]*${store.restoreKey.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&')}`,
        'u',
      ),
    )
    assert.ok(installAt > store.source.indexOf(`name: ${store.restore}`) && installAt < saveAt)
    assert.match(save, new RegExp(`if: ${expectedIf.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&')}`, 'u'))
    assert.equal(save.includes(expectedIf), true)
    assert.match(save, /continue-on-error: true/u)
    assert.match(save, new RegExp(`uses: actions/cache/save@${cachePin}`, 'u'))
    assert.match(
      save,
      new RegExp(
        `key: \\$\\{\\{ steps\\.${store.stepId}\\.outputs\\.cache-primary-key \\}\\}`,
        'u',
      ),
    )
    assert.doesNotMatch(save, /pull_request|head_ref|ref_name|workflow_dispatch|schedule/u)
    assert.doesNotMatch(store.source, /uses: actions\/cache@/u)
  }

  const cases = [
    ['pull_request merge ref', 'pull_request', 'refs/pull/448/merge', 'false', false],
    ['fork pull request', 'pull_request', 'refs/pull/9/merge', 'false', false],
    ['pull_request_target on main', 'pull_request_target', 'refs/heads/main', 'false', false],
    ['workflow_dispatch on main', 'workflow_dispatch', 'refs/heads/main', 'false', false],
    ['schedule on main', 'schedule', 'refs/heads/main', 'false', false],
    ['push to another branch', 'push', 'refs/heads/main-lookalike', 'false', false],
    ['trusted main miss', 'push', 'refs/heads/main', 'false', true],
    ['trusted main prefix miss', 'push', 'refs/heads/main', '', true],
    ['trusted main primary hit', 'push', 'refs/heads/main', 'true', false],
  ]
  for (const [label, eventName, ref, cacheHit, expected] of cases) {
    assert.equal(savesOn(eventName, ref, cacheHit), expected, label)
  }
})
