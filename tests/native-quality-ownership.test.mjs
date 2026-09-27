import assert from 'node:assert/strict'
import { lstat, readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'
const csharpRules = [
  'cs-core-no-platform-types',
  'cs-native-no-raw-markdown-authoring',
  'cs-navigation-catalog-no-hardcoded-labels',
  'cs-no-direct-presentation-formatting',
  'cs-no-hardcoded-display-copy',
  'cs-no-hardcoded-presentation-state',
  'cs-no-localhost-native-defaults',
  'cs-no-raw-local-llm-handler',
  'cs-omnisearch-no-hardcoded-copy',
]

describe('native quality ownership', () => {
  it('keeps the extracted native rule, skill, and agent surfaces discoverable', async () => {
    const config = await readFile(new URL('../sgconfig.yml', import.meta.url), 'utf8')
    assert.match(config, /CSharp:/)
    assert.match(config, /Html:/)
    for (const id of [...csharpRules, 'xaml-native-no-raw-markdown-label']) {
      await lstat(new URL(`../ast-grep-rules/${id}.yml`, import.meta.url))
    }
    for (const skill of ['swift-test-authoring', 'dotnet-test-authoring']) {
      const skillPath = new URL(`../.agents/skills/${skill}/SKILL.md`, import.meta.url)
      assert.match(await readFile(skillPath, 'utf8'), /vouchington-clients/)
      assert.ok(
        (await lstat(new URL(`../.claude/skills/${skill}`, import.meta.url))).isSymbolicLink(),
      )
      const agent = await readFile(
        new URL(`../.codex/agents/${skill}.toml`, import.meta.url),
        'utf8',
      )
      assert.match(agent, /gpt-5\.6-terra/)
    }
    const noMistakes = await readFile(new URL('../.no-mistakes.yml', import.meta.url), 'utf8')
    assert.match(noMistakes, /csharp-max-lines-per-file/)
    assert.match(noMistakes, /csharp-no-async-void-delegate/)
    assert.match(noMistakes, /test_plan:\n  swift:/)
    assert.match(
      noMistakes,
      /packages:\n      - swift-clients\/core\n      - swift-clients\/test-support/,
    )
    assert.match(noMistakes, /\n  dotnet:/)

    const pkg = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'))
    assert.equal(pkg.scripts['test:plan:swift'], 'no-mistakes tests plan swift --format commands')
    assert.equal(pkg.scripts['test:plan:dotnet'], 'no-mistakes tests plan dotnet --format commands')
    const workspace = await readFile(new URL('../pnpm-workspace.yaml', import.meta.url), 'utf8')
    assert.match(workspace, /no-mistakes: true/)
    assert.doesNotMatch(workspace, /set this to true or false/)

    const instructions = await readFile(new URL('../AGENTS.md', import.meta.url), 'utf8')
    assert.match(instructions, /pnpm run test:plan:swift/)
    assert.match(instructions, /pnpm run test:plan:dotnet/)
    const qualityDocs = await readFile(
      new URL('../docs/development/native-quality.md', import.meta.url),
      'utf8',
    )
    assert.match(qualityDocs, /https:\/\/github\.com\/vouchington\/vouchington\/blob\/main\//)
    assert.match(qualityDocs, /native-ci-test-placement\.md/)
    const placement = await readFile(
      new URL('../docs/development/native-ci-test-placement.md', import.meta.url),
      'utf8',
    )
    assert.match(placement, /Portable \.NET/)
    assert.match(placement, /Forbidden dual-OS/)
    assert.match(instructions, /native-ci-test-placement\.md/)
  })
})
