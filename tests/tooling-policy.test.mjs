import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import { describe, it } from 'node:test'

const root = resolve(import.meta.dirname, '..')

async function text(path) {
  return readFile(join(root, path), 'utf8')
}

describe('repository tooling policy', () => {
  it('keeps pre-push limited to remote-main freshness and linting', async () => {
    const hook = await text('.husky/pre-push')
    const commands = hook
      .split('\n')
      .map(line => line.trim())
      .filter(line => line && !line.startsWith('#'))

    assert.deepEqual(commands, [
      'pnpm exec vouchington require-up-to-date --remote origin --branch main',
      'pnpm run lint',
    ])
    assert.doesNotMatch(hook, /\b(?:build|test|coverage|periphery|rebase|merge)\b/u)
  })

  it('defines one complete lint aggregate without tests or builds', async () => {
    const pkg = JSON.parse(await text('package.json'))
    for (const script of [
      'lint',
      'lint:portable',
      'lint:ast-grep',
      'lint:complexity',
      'lint:dependencies',
      'lint:gitleaks',
      'lint:github-actions',
      'lint:shell',
      'lint:swift',
      'lint:dotnet',
      'test:coverage',
    ]) {
      assert.equal(typeof pkg.scripts[script], 'string', `missing ${script}`)
    }
    assert.doesNotMatch(pkg.scripts.lint, /\b(?:build|test|coverage|periphery)\b/u)
    assert.match(pkg.scripts['test:coverage'], /--test-coverage-lines=70/u)
    assert.match(pkg.scripts['test:coverage'], /--test-coverage-branches=70/u)
    assert.match(pkg.scripts['test:coverage'], /--test-coverage-functions=80/u)
  })

  it('pins external lint tools once through mise', async () => {
    const mise = await text('.mise.toml')
    for (const pin of [
      'actionlint = "1.7.12"',
      'shellcheck = "0.11.0"',
      '"github:boyter/scc" = "3.7.0"',
      'zizmor = "1.26.1"',
      '"aqua:gitleaks/gitleaks" = "8.30.1"',
      '"aqua:lycheeverse/lychee" = "0.24.2"',
    ]) {
      assert.match(mise, new RegExp(pin.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&'), 'u'))
    }
  })

  it('enforces the shared SCC complexity guard', async () => {
    const complexity = await text('scripts/scc-complexity.mjs')
    assert.match(complexity, /vouchington-tooling\/scc-complexity/u)
    assert.match(complexity, /checkSccComplexity/u)
    assert.match(complexity, /\.github,dev/u)
  })

  it('uses default Gitleaks rules without a baseline or allowlist', async () => {
    const config = await text('.gitleaks.toml')
    assert.match(config, /useDefault = true/u)
    assert.doesNotMatch(config, /allowlists|baselinePath/u)
  })

  it('enables the migrated no-mistakes rules', async () => {
    const config = await text('.no-mistakes.yml')
    for (const rule of [
      'lockfile-allowlist',
      'package-json-registry-only',
      'pnpm-overrides-ban',
      'pnpm-release-age-policy',
      'test-no-dependency-pins',
      'github-actions-composite-step-schema',
      'swift-no-raw-print',
      'swift-viewmodel-main-actor',
    ]) {
      assert.match(config, new RegExp(`rule: ${rule}(?:\\n|$)`, 'u'), `missing ${rule}`)
    }
  })

  it('feeds every new static job into the existing validate aggregate', async () => {
    const workflow = await text('.github/workflows/validate.yml')
    for (const job of ['tooling-lint', 'gitleaks', 'swift-lint']) {
      assert.match(workflow, new RegExp(`^  ${job}:`, 'mu'), `missing ${job}`)
      assert.match(workflow, new RegExp(`needs: \\[[^\\]]*${job}`, 'u'), `${job} is not required`)
    }
    assert.match(workflow, /name: validate/u)
  })
})
