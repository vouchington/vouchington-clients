import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, it } from 'node:test'

// vouchington-machines registers the `vouchington-tooling` MCP server, plugins, and marketplaces
// per machine and pre-approves its tools. No tracked repository file does.
const root = resolve(import.meta.dirname, '..')
const removedConfigs = [
  '.mcp.json',
  '.cursor/mcp.json',
  '.claude/settings.json',
  '.codex/config.toml',
  '.grok/config.toml',
]
const harnessFiles = [
  'AGENTS.md',
  '.claude/README.md',
  '.codex/README.md',
  '.cursor/README.md',
  '.cursor/cli.json',
  '.cursor/permissions.json',
  '.grok/README.md',
]

function read(path) {
  return readFileSync(resolve(root, path), 'utf8')
}

describe('machine-owned agent host configuration', () => {
  it('keeps no repository-level MCP registration, plugin, marketplace, or settings file', () => {
    for (const path of [
      ...removedConfigs,
      '.cursor/hooks.json',
      '.cursor/sandbox.json',
      '.grok/sandbox.toml',
      '.grok/hooks',
    ])
      assert.equal(existsSync(resolve(root, path)), false, `${path} must stay absent`)
  })

  it('keeps no agent-blackboard registration, credential, or tool grant in harness files', () => {
    for (const path of harnessFiles) {
      const text = read(path)
      // AGENTS.md keeps the fail-closed client-credential rule; host config must not wire it.
      const retired =
        path === 'AGENTS.md' ? /agent-blackboard/u : /agent-blackboard|AGENT_BLACKBOARD/u
      assert.doesNotMatch(text, retired, `${path} names it`)
      assert.doesNotMatch(text, /enabledMcpjsonServers|extraKnownMarketplaces|enabledPlugins/u)
    }
  })

  it('keeps Cursor approvals limited to native-client shell tools', () => {
    assert.deepEqual(JSON.parse(read('.cursor/cli.json')), {
      permissions: {
        allow: ['Shell(no-mistakes)', 'Shell(swift)', 'Shell(xcodebuild)', 'Shell(dotnet)'],
        deny: ['Shell(sudo)'],
      },
    })
    const permissions = JSON.parse(read('.cursor/permissions.json'))
    assert.deepEqual(Object.keys(permissions), ['autoRun'])
    assert.equal(
      permissions.autoRun.block_instructions.some(rule => rule.includes('~/')),
      false,
    )
  })

  it('requires fail-closed, explicit journaling through the machine-registered server', () => {
    const instructions = read('AGENTS.md')
    assert.match(instructions, /machine-registered `vouchington-tooling` MCP server/u)
    assert.match(instructions, /`vouchington-workflow:blackboard`/u)
    assert.match(instructions, /Session ids.*must\s+be\s+explicit/isu)
    assert.match(instructions, /fail closed/iu)
  })

  it('points machine defaults and provisioning at the ownership contract', () => {
    for (const path of [
      '.claude/README.md',
      '.codex/README.md',
      '.cursor/README.md',
      '.grok/README.md',
    ])
      assert.match(read(path), /vouchington-machines\/blob\/main\/docs\/agent-config\.md/u)
  })

  it('documents user-scope plugin provisioning and reads AGENTS.md without a CLAUDE fallback', () => {
    const claude = read('.claude/README.md')
    assert.match(claude, /vouchington-workflow@vouchington/u)
    assert.match(claude, /vouchington-testing@vouchington/u)
    assert.match(claude, /pr-shepherd@jonathanong/u)
    assert.match(claude, /canonical plugin is unavailable, stop/iu)
    assert.doesNotMatch(claude, /--scope project/u)
    const codex = read('.codex/README.md')
    assert.match(codex, /vouchington-testing@vouchington/u)
    assert.match(codex, /Do not add a CLAUDE\.md fallback/u)
  })

  it('keeps native test overlays dependent on canonical testing skills', () => {
    for (const [name, canonical, guidance] of [
      [
        'swift-test-authoring',
        'vouchington-testing:swift-test-authoring',
        'swift-clients/AGENTS.md',
      ],
      [
        'dotnet-test-authoring',
        'vouchington-testing:dotnet-test-authoring',
        'dotnet-clients/AGENTS.md',
      ],
    ]) {
      const skill = read(`.agents/skills/${name}/SKILL.md`)
      const escapeRegExp = value => value.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&')
      assert.match(skill, new RegExp(escapeRegExp(canonical), 'u'))
      assert.match(skill, new RegExp(escapeRegExp(guidance), 'u'))
      assert.match(skill, /stop and report the missing prerequisite/iu)
    }
  })

  it('keeps client-owned architecture links local and Vouchington-owned links explicit', () => {
    const swift = read('swift-clients/AGENTS.md')
    const dotnet = read('dotnet-clients/AGENTS.md')
    for (const instructions of [swift, dotnet]) {
      assert.match(
        instructions,
        /github\.com\/vouchington\/vouchington\/blob\/main\/docs\/overview\/architecture\/pagination\.md/u,
      )
      assert.match(
        instructions,
        /github\.com\/vouchington\/vouchington\/blob\/main\/docs\/overview\/architecture\/native-clients\.md/u,
      )
    }
    assert.match(dotnet, /docs\/overview\/architecture\/dotnet-deep-linking\.md/u)
    assert.match(
      dotnet,
      /github\.com\/vouchington\/vouchington\/blob\/main\/docs\/runbooks\/native-tls-pinning\.md/u,
    )
    assert.equal(
      existsSync(
        resolve(root, 'docs/overview/architecture/reference-native-clients-current-footprint.md'),
      ),
      true,
    )
    assert.equal(
      existsSync(resolve(root, 'docs/overview/architecture/dotnet-deep-linking.md')),
      true,
    )
  })

  it('ignores provider worktree state without ignoring checked-in configuration', () => {
    const gitignore = read('.gitignore')
    for (const path of [
      '.claude/worktrees/',
      '.codex/worktrees/',
      '.cursor/worktrees/',
      '.grok/worktrees/',
      '.worktrees/',
    ])
      assert.match(gitignore, new RegExp(`^${path.replaceAll('.', '\\.')}$`, 'mu'))
  })
})
