import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import test from 'node:test'

const root = new URL('../', import.meta.url)
const read = path => readFileSync(new URL(path, root), 'utf8')
const yamlFiles = dir =>
  readdirSync(new URL(dir, root), { recursive: true })
    .filter(file => /\.ya?ml$/u.test(file))
    .map(file => `${dir}/${file}`)
const composites = yamlFiles('.github/actions')
// The mise steps of a composite, from the directory lookup through the main-only save.
const miseSteps = file => {
  const action = read(file)
  const start = action.indexOf('- name: Locate the mise binary directory')
  const endMarker = 'key: ${{ steps.mise-bin.outputs.cache-primary-key }}'
  const end = action.indexOf(endMarker)
  assert.ok(start >= 0 && end > start, file)
  return action.slice(start, end + endMarker.length)
}
const miseComposites = [
  '.github/actions/setup-mise/action.yml',
  '.github/actions/setup-mise-toolchain/action.yml',
  '.github/actions/setup-swift-native/action.yml',
]

test('composite actions never call another local action through a ./ path', () => {
  assert.ok(composites.length > 5)
  for (const file of composites) {
    // ./ resolves against GITHUB_WORKSPACE, which is not the composite's checkout (trusted-clients).
    assert.doesNotMatch(read(file), /uses:\s*\.\//u, file)
  }
})

test('mise-action is only used by the three mise setup composites, at one pin and version', () => {
  const workflows = yamlFiles('.github/workflows')
  for (const file of [...workflows, ...composites].filter(file => !miseComposites.includes(file))) {
    assert.doesNotMatch(read(file), /jdx\/mise-action/u, file)
  }
  const pins = new Set()
  const versions = new Set()
  for (const file of miseComposites) {
    const action = miseSteps(file)
    assert.equal(action.match(/jdx\/mise-action@/gu)?.length, 1, file)
    pins.add(action.match(/uses: (jdx\/mise-action@[0-9a-f]{40} # v\d+\.\d+\.\d+)/u)?.[1])
    versions.add(
      action.match(/version: '(\d{4}\.\d+\.\d+)'/u)?.[1] ??
        read(file).match(/default: '(\d{4}\.\d+\.\d+)'/u)?.[1],
    )
    const restore = action.indexOf('actions/cache/restore@')
    assert.ok(restore > 0 && restore < action.indexOf('jdx/mise-action@'), file)
    assert.match(action, /cache: false\n\s+install: false/u, file)
    assert.match(action, /SHASUMS256-\*/u, file)
    assert.doesNotMatch(action, /retry/iu, file)
  }
  assert.equal(pins.size, 1)
  assert.equal(versions.size, 1)
  const version = [...versions][0]
  assert.ok(version)
  for (const file of miseComposites) {
    assert.match(
      miseSteps(file),
      new RegExp(
        `key: mise-bin-v1-\\$\\{\\{ runner\\.os \\}\\}-\\$\\{\\{ runner\\.arch \\}\\}-(\\$\\{\\{ inputs\\.version \\}\\}|${version.replaceAll('.', '\\.')})`,
        'u',
      ),
      file,
    )
  }
})

test('the mise binary cache is written only from main pushes', () => {
  for (const file of miseComposites) {
    const action = miseSteps(file)
    assert.equal(action.match(/actions\/cache\/save@/gu)?.length, 1, file)
    assert.match(
      action,
      /if: github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.mise-bin\.outputs\.cache-hit != 'true'/u,
      file,
    )
    assert.doesNotMatch(action, /uses: actions\/cache@/u, file)
    const check = action.indexOf('id: mise-complete')
    assert.ok(check > 0 && check < action.indexOf('actions/cache/save@'), file)
    assert.match(action, /SHASUMS256-\$MISE_VERSION\.txt\.minisig/u, file)
    assert.match(action, /::warning::Not saving the mise binary cache/u, file)
    assert.match(
      action,
      /steps\.mise-complete\.outputs\.complete == 'true'\n\s+continue-on-error: true\n\s+uses: actions\/cache\/save@/u,
      file,
    )
  }
})

test('every steps.<id> reference in a composite resolves to an earlier step id', () => {
  for (const file of composites) {
    const action = read(file)
    for (const match of action.matchAll(/\bsteps\.([\w-]+)\./gu)) {
      const defined = action.slice(0, match.index).match(new RegExp(`^\\s+id: ${match[1]}$`, 'mu'))
      assert.ok(defined, `${file} references steps.${match[1]} before any step defines that id`)
    }
  }
})
